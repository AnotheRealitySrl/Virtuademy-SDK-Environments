using System;
using System.Collections.Generic;
using System.Text;

using Virtuademy.ScriptingApi;

namespace Virtuademy.SDK.Environments.Analytics
{
    /// <summary>Sends analytics and generates experience ids: the platform, a log, or a test's fake.</summary>
    public interface IProcedureAnalyticsSender
    {
        /// <summary>Generates the experience id of <paramref name="key"/>; <paramref name="onReady"/> when it exists.</summary>
        void GenerateId(string key, Action onReady);

        void Send(EAnalyticVerb verb, AnalyticDTO dto, AnalyticSentHandler onSent);
    }

    /// <summary>One-shot timers (a coroutine in the game, a hand-driven clock in tests).</summary>
    public interface IProcedureScheduler
    {
        object After(float seconds, Action action);

        void Cancel(object handle);
    }

    /// <summary>
    /// The analytics of a procedure, one run after another: a run key minted per run (prefix + 8 random letters and
    /// digits, at most 15), its experience id generated before anything is sent (a 10 s timeout mints a new key; the first
    /// answer wins), and a queue sent in order, one statement in flight, a failed one retried after 2, 4, 8, 16 then 30 s:
    /// ExpStart, StepStart when a step starts, exactly one StepComplete per started step, one ExpComplete per run.
    /// Plain C#: the timers and the sending are injected.
    /// </summary>
    public sealed class ProcedureAnalytics
    {
        public const int MaxKeyLength = 15;
        public const float IdTimeoutSeconds = 10f;
        private static readonly float[] Backoff = { 2f, 4f, 8f, 16f, 30f };
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

        private sealed class Run
        {
            public string Key;
            public bool IdReady;
            public int Generation;
            public object IdTimeout;
            public readonly HashSet<string> StartedSteps = new();
            public readonly HashSet<string> CompletedSteps = new();
            public bool Completed;
        }

        private sealed class Item
        {
            public Run Run;
            public EAnalyticVerb Verb;
            public AnalyticDTO Dto;
        }

        private readonly IProcedureAnalyticsSender sender;
        private readonly IProcedureScheduler scheduler;
        private readonly string prefix;
        private readonly string context;
        private readonly Random random;
        private readonly LinkedList<Item> queue = new();
        private Run current;
        private bool inFlight;
        private int failures;
        private object retry;

        /// <summary>Raised when nothing is queued or in flight any more.</summary>
        public event Action Drained;

        /// <summary>Raised for every statement the platform stored.</summary>
        public event Action<EAnalyticVerb, AnalyticDTO> Sent;

        /// <summary>Raised when the current run's experience id exists, with its key.</summary>
        public event Action<string> RunIdReady;

        private readonly HashSet<string> joined = new();

        public ProcedureAnalytics(IProcedureAnalyticsSender sender, IProcedureScheduler scheduler, string keyPrefix,
            string context, int seed = 0)
        {
            this.sender = sender;
            this.scheduler = scheduler;
            prefix = SanitizePrefix(keyPrefix);
            this.context = context ?? "";
            random = seed == 0 ? new Random() : new Random(seed);
        }

        /// <summary>The current run's key ("" before the first run).</summary>
        public string RunKey => current?.Key ?? "";

        public bool IsIdle => queue.Count == 0 && !inFlight;

        public int Queued => queue.Count;

        /// <summary>Letters and digits only, at most 7 (the key is the prefix and 8 random characters).</summary>
        public static string SanitizePrefix(string raw)
        {
            StringBuilder b = new();
            foreach (char c in raw ?? "")
            {
                if (char.IsLetterOrDigit(c) && c < 128)
                {
                    b.Append(char.ToLowerInvariant(c));
                }
                if (b.Length == MaxKeyLength - 8)
                {
                    break;
                }
            }
            return b.Length > 0 ? b.ToString() : "proc";
        }

        private string MintKey()
        {
            char[] tail = new char[8];
            for (int i = 0; i < tail.Length; i++)
            {
                tail[i] = Alphabet[random.Next(Alphabet.Length)];
            }
            return prefix + new string(tail);
        }

        #region Run events

        /// <summary>A run starts: a new key and id, ExpStart queued.</summary>
        public void StartRun()
        {
            current = new Run { Key = MintKey() };
            // Queued first, so ExpStart leads even when the id answers at once.
            Enqueue(current, EAnalyticVerb.ExpStart, new ExperienceStartDTO(current.Key, context));
            GenerateId(current);
        }

        public void StepStarted(string stepId, string description)
        {
            if (current == null || current.Completed || !current.StartedSteps.Add(stepId))
            {
                return;
            }
            Enqueue(current, EAnalyticVerb.StepStart, new ExperienceStepStartDTO(current.Key, stepId, description ?? ""));
        }

        /// <summary>A started step's result (completed or failed); a step not started, or already sent, sends nothing.</summary>
        public void StepCompleted(string stepId, int score, int maxScore, int passingScore, EExperienceOutcome outcome)
        {
            if (current == null || current.Completed || !current.StartedSteps.Contains(stepId) || !current.CompletedSteps.Add(stepId))
            {
                return;
            }
            Enqueue(current, EAnalyticVerb.StepComplete, new ExperienceStepCompleteDTO(current.Key, stepId, score, maxScore,
                passingScore, outcome, EExperienceScoringType.Numeric));
        }

        /// <summary>The run ends: every started step without a result gets one (0, Fail), then ExpComplete, once.</summary>
        public void FinishRun(int score, int maxScore, int passingScore, EExperienceOutcome outcome)
        {
            if (current == null || current.Completed)
            {
                return;
            }
            foreach (string step in current.StartedSteps)
            {
                if (!current.CompletedSteps.Contains(step))
                {
                    current.CompletedSteps.Add(step);
                    Enqueue(current, EAnalyticVerb.StepComplete, new ExperienceStepCompleteDTO(current.Key, step, 0, 0, 0,
                        EExperienceOutcome.Fail, EExperienceScoringType.Numeric));
                }
            }
            current.Completed = true;
            Enqueue(current, EAnalyticVerb.ExpComplete, new ExperienceCompleteDTO(current.Key, score, maxScore, passingScore,
                outcome, EExperienceScoringType.Numeric));
        }

        /// <summary>Joins the team run <paramref name="key"/> (its id is already in the room): one ExpJoin per key.</summary>
        public void JoinRun(string key)
        {
            if (string.IsNullOrEmpty(key) || !joined.Add(key))
            {
                return;
            }
            Run run = current != null && current.Key == key ? current : new Run { Key = key, IdReady = true };
            Enqueue(run, EAnalyticVerb.ExpJoin, new ExperienceJoinDTO(key, context));
        }

        /// <summary>
        /// Takes over the team run <paramref name="key"/> from a master that left: what was sent is known from the room's
        /// flags; ExpStart again if it was not.
        /// </summary>
        public void AdoptRun(string key, Func<string, bool> sent)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            current = new Run { Key = key, IdReady = true, Completed = sent("ec") };
            if (!sent("es"))
            {
                Enqueue(current, EAnalyticVerb.ExpStart, new ExperienceStartDTO(key, context));
            }
        }

        /// <summary>Marks a step as started without sending its StepStart (it was sent by a master that left).</summary>
        public void StepStartedSent(string stepId) => current?.StartedSteps.Add(stepId);

        #endregion

        #region Id

        private void GenerateId(Run run)
        {
            int generation = ++run.Generation;
            run.IdTimeout = scheduler.After(IdTimeoutSeconds, () => OnIdTimeout(run, generation));
            sender.GenerateId(run.Key, () => OnIdReady(run, generation));
        }

        private void OnIdReady(Run run, int generation)
        {
            if (run.IdReady || generation != run.Generation)
            {
                return; // a later key was minted, or this one answered already: the first answer wins
            }
            run.IdReady = true;
            scheduler.Cancel(run.IdTimeout);
            if (run == current)
            {
                RunIdReady?.Invoke(run.Key);
            }
            Pump();
        }

        private void OnIdTimeout(Run run, int generation)
        {
            if (run.IdReady || generation != run.Generation)
            {
                return;
            }
            // No id in time: a new key, given to everything of this run still queued.
            string key = MintKey();
            run.Key = key;
            foreach (Item item in queue)
            {
                if (item.Run == run && item.Dto is ExperienceAnalyticDTO dto)
                {
                    dto.SetKey(key);
                }
            }
            GenerateId(run);
        }

        #endregion

        #region Queue

        private void Enqueue(Run run, EAnalyticVerb verb, AnalyticDTO dto)
        {
            queue.AddLast(new Item { Run = run, Verb = verb, Dto = dto });
            Pump();
        }

        private void Pump()
        {
            if (inFlight || retry != null || queue.Count == 0)
            {
                if (IsIdle)
                {
                    Drained?.Invoke();
                }
                return;
            }
            Item head = queue.First.Value;
            if (!head.Run.IdReady)
            {
                return; // waits for its run's id (or the timeout that mints another)
            }
            inFlight = true;
            sender.Send(head.Verb, head.Dto, (success, error) => OnSent(head, success, error));
        }

        private void OnSent(Item item, bool success, string error)
        {
            inFlight = false;
            if (success)
            {
                failures = 0;
                if (queue.First != null && queue.First.Value == item)
                {
                    queue.RemoveFirst();
                }
                Sent?.Invoke(item.Verb, item.Dto);
                Pump();
                return;
            }
            float delay = Backoff[Math.Min(failures, Backoff.Length - 1)];
            failures++;
            retry = scheduler.After(delay, () =>
            {
                retry = null;
                Pump();
            });
        }

        #endregion
    }
}
