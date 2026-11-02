using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi
{
    /// <summary>
    /// One use of a tool, by this player or another (2026.7): what <see cref="IToolsApi.ToolUseStarted"/> and
    /// <see cref="IToolsApi.ToolUseEnded"/> pass. An effect that every player should see (a spray's jet) starts under
    /// <see cref="Aim"/> for every use, and stops at its end; what the use does to the world (a target hit, a use
    /// spent) is done only for <see cref="IsLocal"/> uses, by the player who uses the tool.
    /// </summary>
    public sealed class ToolUse
    {
        public ToolUse(string toolName, bool isLocal, string player, Transform aim)
        {
            ToolName = toolName;
            IsLocal = isLocal;
            Player = player;
            Aim = aim;
        }

        /// <summary>The tool's name (its Pickable's item name).</summary>
        public string ToolName { get; }

        /// <summary>True for this player's own use; false for another player's.</summary>
        public bool IsLocal { get; }

        /// <summary>The name of the player using it.</summary>
        public string Player { get; }

        /// <summary>
        /// Where the use starts and where it points (its forward): this player's held tool model, hand (VR) or camera;
        /// for another player, a point at their avatar's right hand, pointed where they aimed when the use started, and
        /// turning with their avatar. Parent the effect to it, so it follows.
        /// </summary>
        public Transform Aim { get; }
    }
}
