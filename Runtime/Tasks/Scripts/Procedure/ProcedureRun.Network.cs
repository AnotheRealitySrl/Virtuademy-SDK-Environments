using System;
using System.Collections.Generic;
using System.Globalization;

using SPACS.Tasks;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Tasks;

using static SPACS.Tasks.TaskNode;

namespace Virtuademy.SDK.Environments.Tasks
{
    // The shared procedure (D.12): the same events on every client (TaskNetworkSync), one clock (the network time), the
    // time limit run by the master, and a snapshot the master writes (one room property) and the others apply, so a
    // client joining late, or one that missed an event, gets the room's state.
    public partial class ProcedureRun
    {
        private TaskNetworkSync sync;
        private bool restoring;
        private bool writePending;

        // Runs of the room, counted by the master; a client ignores the snapshot of a run before the one it is in (after
        // a retry, until the master writes the new run's).
        private int runNumber;
        private int minRunNumber;

        /// <summary>The room's network when the task system is shared; null otherwise.</summary>
        internal IProcedureNetwork Network => sync?.Network;

        internal bool IsShared => sync != null;

        /// <summary>Whether this client keeps the snapshot and the timer (single player: always).</summary>
        internal bool IsMaster => sync == null || sync.Network.IsMaster;

        private double Now() => sync != null ? sync.Network.NetworkTime : Time.realtimeSinceStartupAsDouble;

        /// <summary>Raised when the snapshot this client keeps changed (the analytics flags live in it).</summary>
        internal event Action SnapshotApplied;

        /// <summary>Raised on a client that becomes the master during a run (it takes over the timer and the analytics).</summary>
        internal event Action BecameMaster;

        internal void BindNetwork(TaskNetworkSync networkSync)
        {
            sync = networkSync;
            sync.Network.SnapshotChanged += OnSnapshotChanged;
            sync.Network.MasterChanged += OnMasterChanged;
            procedure.ScoreChanged += _ => WriteSnapshotIfMaster();
            // A run already going when the room arrives: the master writes it, the others take the room's.
            if (running)
            {
                if (IsMaster)
                {
                    WriteSnapshotIfMaster();
                }
                else
                {
                    ApplySnapshot();
                }
            }
        }

        private void UnbindNetwork()
        {
            if (sync != null)
            {
                sync.Network.SnapshotChanged -= OnSnapshotChanged;
                sync.Network.MasterChanged -= OnMasterChanged;
            }
        }

        private bool RunsTimer => IsMaster;

        // At a run's start: the master starts the clock and writes it; the others take the master's start.
        private void OnRunStartNetwork()
        {
            if (sync == null)
            {
                return;
            }
            if (IsMaster)
            {
                runNumber++;
                flags.Clear();
                WriteSnapshotIfMaster();
            }
            else
            {
                ApplySnapshot();
            }
        }

        private void StartTimeLimit()
        {
            if (timeLimit != null)
            {
                StopCoroutine(timeLimit);
                timeLimit = null;
            }
            if (procedure.TimeLimitSeconds > 0f && RunsTimer && IsRunning)
            {
                timeLimit = StartCoroutine(TimeLimit(Mathf.Max(0f, RemainingSeconds)));
            }
        }

        private void SendTimeUp()
        {
            sync?.Send(new ProcedureNetworkEvent { Kind = ProcedureNetworkEvent.TimeUp });
        }

        internal void OnRemoteTimeUp()
        {
            if (!finished)
            {
                AddTimeline(null, false, 0f, "Time is up");
                FailCondition(EProcedureFailReason.TimeLimit);
            }
        }

        private void SendHint(Step step)
        {
            sync?.Send(new ProcedureNetworkEvent { Kind = ProcedureNetworkEvent.Hint, TaskIndex = sync.IndexOf(step.Task) });
        }

        /// <summary>A distractor without a task, used here: applied by TaskReports, sent here.</summary>
        internal void ReportDistractorWithoutTask(ProcedureNetworkEvent e)
        {
            e.TaskIndex = -1;
            sync?.Send(e);
        }

        /// <summary>The room's run before this revert is over: its snapshot no longer applies here.</summary>
        private void OnRevertNetwork()
        {
            if (sync != null && !IsMaster)
            {
                minRunNumber = runNumber + 1;
            }
        }

        private void OnMasterChanged()
        {
            if (!IsMaster)
            {
                return;
            }
            // This client takes over: the time limit from what is left, the snapshot, the analytics.
            StartTimeLimit();
            WriteSnapshotIfMaster();
            if (IsRunning || finished)
            {
                BecameMaster?.Invoke();
            }
        }

        #region Snapshot

        // Analytics flags (sent statements) the master keeps in the snapshot, by name.
        private readonly HashSet<string> flags = new();

        internal bool HasFlag(string flag) => flags.Contains(flag);

        internal void SetFlag(string flag)
        {
            if (flags.Add(flag))
            {
                WriteSnapshotIfMaster();
            }
        }

        private void WriteSnapshotIfMaster()
        {
            if (sync == null || !IsMaster || restoring)
            {
                return;
            }
            if (writePending)
            {
                return; // the write in flight will be followed by this state (written again on its answer)
            }
            writePending = true;
            sync.Network.WriteSnapshot(BuildSnapshot(), ok =>
            {
                writePending = false;
                // A conflict (or a change since this write was built) writes again from the current state.
                if (!ok)
                {
                    WriteSnapshotIfMaster();
                }
            });
        }

        private Dictionary<string, object> BuildSnapshot()
        {
            Dictionary<string, object> s = new()
            {
                ["run"] = running,
                ["rn"] = runNumber,
                ["rk"] = runKey,
                ["st"] = startTime,
                ["fin"] = finished,
                ["err"] = errors,
                ["hint"] = hintsUsed,
                ["dp"] = scoring.DistractorPenalty,
                ["fc"] = (int)failCondition,
            };
            for (int i = 0; i < steps.Count; i++)
            {
                ProcedureScoring.TaskScore t = steps[i].Score;
                if (t != null)
                {
                    s["s." + i] = string.Join(",", t.Done ? 1 : 0, t.Failed ? 1 : 0, t.Skipped ? 1 : 0,
                        t.ResultScore.ToString(CultureInfo.InvariantCulture), t.WrongAttempts, t.HintRequests,
                        steps[i].Started ? 1 : 0);
                }
            }
            foreach (string flag in flags)
            {
                s["f." + flag] = true;
            }
            foreach (QuestionElementDriver question in QuestionElementDriver.All)
            {
                int index = sync.IndexOf(question.TaskForSync);
                if (index >= 0)
                {
                    s["q." + index] = string.Join(",", question.DrawOrder());
                }
            }
            return s;
        }

        private void OnSnapshotChanged()
        {
            if (!IsMaster)
            {
                ApplySnapshot();
            }
        }

        /// <summary>The master's state, on a client that is not the master: counters, steps, flags, question draws, and
        /// the tasks it has done that this client has not (a late joiner).</summary>
        private void ApplySnapshot()
        {
            IReadOnlyDictionary<string, object> s = sync.Network.Snapshot;
            if (s == null || !s.TryGetValue("run", out object runObj) || !(runObj is bool runFlag) || !runFlag)
            {
                return;
            }
            if (!running)
            {
                return; // this client's run has not started: its start applies it
            }
            int number = Convert.ToInt32(Get(s, "rn", 0));
            if (number < minRunNumber)
            {
                return; // the run before a retry: the master writes the new one
            }
            runNumber = number;
            restoring = true;
            try
            {
                runKey = (string)Get(s, "rk", runKey);
                startTime = Convert.ToDouble(Get(s, "st", startTime), CultureInfo.InvariantCulture);
                errors = Convert.ToInt32(Get(s, "err", errors));
                hintsUsed = Convert.ToInt32(Get(s, "hint", hintsUsed));
                scoring.DistractorPenalty = Convert.ToSingle(Get(s, "dp", scoring.DistractorPenalty), CultureInfo.InvariantCulture);
                EProcedureFailReason reason = (EProcedureFailReason)Convert.ToInt32(Get(s, "fc", (int)failCondition));
                if (failCondition == EProcedureFailReason.None)
                {
                    failCondition = reason;
                }
                for (int i = 0; i < steps.Count; i++)
                {
                    if (steps[i].Score == null || !(Get(s, "s." + i, null) is string packed))
                    {
                        continue;
                    }
                    string[] p = packed.Split(',');
                    ProcedureScoring.TaskScore t = steps[i].Score;
                    t.Done = p[0] == "1";
                    t.Failed = p[1] == "1";
                    t.Skipped = p[2] == "1";
                    t.ResultScore = float.Parse(p[3], CultureInfo.InvariantCulture);
                    t.WrongAttempts = int.Parse(p[4], CultureInfo.InvariantCulture);
                    t.HintRequests = int.Parse(p[5], CultureInfo.InvariantCulture);
                    steps[i].Started |= p[6] == "1";
                    TaskStatus status = steps[i].Task.Node.Status;
                    if (t.Done && status != TaskStatus.Completed && status != TaskStatus.Failed)
                    {
                        // Done in the room, not here (a late joiner): the node follows; its points are the master's.
                        steps[i].Task.Node.Status = t.Failed ? TaskStatus.Failed : TaskStatus.Completed;
                    }
                }
                flags.Clear();
                foreach (KeyValuePair<string, object> pair in s)
                {
                    if (pair.Key.StartsWith("f.", StringComparison.Ordinal))
                    {
                        flags.Add(pair.Key.Substring(2));
                    }
                    else if (pair.Key.StartsWith("q.", StringComparison.Ordinal) && pair.Value is string order)
                    {
                        Task task = sync.TaskAt(int.Parse(pair.Key.Substring(2), CultureInfo.InvariantCulture));
                        QuestionElementDriver question = QuestionElementDriver.For(task);
                        if (question != null && order.Length > 0)
                        {
                            question.UseDraw(Array.ConvertAll(order.Split(','), x => int.Parse(x, CultureInfo.InvariantCulture)));
                        }
                    }
                }
                scoring.Recompute();
            }
            finally
            {
                restoring = false;
            }
            SnapshotApplied?.Invoke();
            procedure.RaiseScoreChanged();
            CheckAllDone();
        }

        private static object Get(IReadOnlyDictionary<string, object> s, string key, object fallback) =>
            s.TryGetValue(key, out object value) && value != null ? value : fallback;

        #endregion
    }
}
