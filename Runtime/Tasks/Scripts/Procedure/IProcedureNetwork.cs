using System;
using System.Collections.Generic;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>What a task event carries over the network (a result, a hint, a distractor, the time running out).</summary>
    public sealed class ProcedureNetworkEvent
    {
        public const int Attempt = 0, Complete = 1, Fail = 2, Hint = 3, Distractor = 4, TimeUp = 5;

        /// <summary>One of the constants above.</summary>
        public int Kind;
        /// <summary>The task's index in its task system (the order of its Task components); -1 for none.</summary>
        public int TaskIndex = -1;
        public int Outcome;
        public float Score;
        public string Reason = "";
        public float Penalty;
        public bool CountsAsError;
    }

    /// <summary>A received task event with who sent it.</summary>
    public delegate void ProcedureNetworkEventHandler(ProcedureNetworkEvent e, string actorName);

    /// <summary>
    /// The room a shared task system lives in, as the SDK runtime needs it: the application implements it in multiplayer
    /// (<c>ProcedureRPCManager</c>) and hands it to <see cref="TaskSystemVirtuademy.SetProcedureNetwork"/>. Task events go to
    /// the other clients (the sender applied them first); the snapshot is one versioned room property the master keeps,
    /// read by a client joining late.
    /// </summary>
    public interface IProcedureNetwork
    {
        bool IsMaster { get; }

        /// <summary>Seconds, the same on every client of the room.</summary>
        double NetworkTime { get; }

        /// <summary>Sends <paramref name="e"/> to the other clients (never back to this one).</summary>
        void SendEvent(ProcedureNetworkEvent e);

        event ProcedureNetworkEventHandler EventReceived;

        /// <summary>The snapshot (empty when none was written yet).</summary>
        IReadOnlyDictionary<string, object> Snapshot { get; }

        /// <summary>Writes the snapshot: replaces it when nobody wrote a newer one since it was read; on a conflict the
        /// latest is read again and <paramref name="done"/> answers false, so the writer can try again.</summary>
        void WriteSnapshot(Dictionary<string, object> snapshot, Action<bool> done);

        /// <summary>Raised when the snapshot changes (written by any client).</summary>
        event Action SnapshotChanged;

        /// <summary>Raised when the master changes.</summary>
        event Action MasterChanged;
    }
}
