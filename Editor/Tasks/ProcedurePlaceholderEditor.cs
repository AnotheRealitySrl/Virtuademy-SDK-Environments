using System.Collections.Generic;
using System.Linq;

using Unity.VisualScripting;

using UnityEditor;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;
using Virtuademy.SDK.Environments.Tasks;

namespace Virtuademy.SDK.Environments.Editor.Tasks
{
    /// <summary>
    /// Inspector of <see cref="ProcedurePlaceholder"/>: the fields, and what will not work as set — not on a task system,
    /// no summary or feedback panels where they are needed, and the analytics conflicting with the legacy kit (a
    /// ControlManager or ExperienceVerbManager graph in the scene also sends experience statements: two runs per session,
    /// and the legacy graph mints its id on every client).
    /// </summary>
    [CustomEditor(typeof(ProcedurePlaceholder))]
    public class ProcedurePlaceholderEditor : UnityEditor.Editor
    {
        private static readonly string[] LegacySenders = { "ControlManager", "ExperienceVerbManager" };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            ProcedurePlaceholder procedure = (ProcedurePlaceholder)target;
            EditorGUILayout.Space();
            foreach ((MessageType type, string text) in Validate(procedure))
            {
                EditorGUILayout.HelpBox(text, type);
            }
        }

        public static List<(MessageType, string)> Validate(ProcedurePlaceholder procedure)
        {
            List<(MessageType, string)> messages = new();
            if (!procedure.TryGetComponent(out TaskSystemVirtuademy _))
            {
                messages.Add((MessageType.Error, "The Procedure placeholder goes on the object of a task system " +
                    "(TasksLogic_Procedure has one). Here it does nothing."));
            }
            if (procedure.ShowSummaryAtEnd && procedure.SummaryPanel == null)
            {
                messages.Add((MessageType.Warning, "Show Summary is on but no Summary Panel is set: nothing shows at the end " +
                    "(the summary is still given by On Procedure Finished)."));
            }
            if (procedure.FeedbackPresentation == EFeedbackPresentation.Panel && (procedure.SuccessPanel == null || procedure.ErrorPanel == null))
            {
                messages.Add((MessageType.Warning, "Feedback Presentation is Panel but a success or error panel is missing: " +
                    "toasts are used instead."));
            }
            if (procedure.PlaySounds && procedure.Sounds == null)
            {
                messages.Add((MessageType.Info, "Play Sounds is on but Sounds is empty: no procedure sound (cues still play theirs)."));
            }
            if (procedure.SendAnalytics && procedure.gameObject.scene.IsValid())
            {
                List<string> legacy = LegacyAnalytics(procedure.gameObject.scene);
                if (legacy.Count > 0)
                {
                    messages.Add((MessageType.Warning, "Send Analytics is on, and the scene also has " + string.Join(", ", legacy) +
                        ", which sends experience statements too: the session would record two runs. Turn one of them off."));
                }
            }
            return messages;
        }

        // Objects of the legacy ControlManager kit: by their name or their graph's.
        private static List<string> LegacyAnalytics(UnityEngine.SceneManagement.Scene scene)
        {
            List<string> found = new();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (ScriptMachine machine in root.GetComponentsInChildren<ScriptMachine>(true))
                {
                    string graph = machine.nest?.macro != null ? machine.nest.macro.name : "";
                    if (LegacySenders.Any(n => machine.name.Contains(n) || graph.Contains(n)))
                    {
                        found.Add(machine.name);
                    }
                }
            }
            return found.Distinct().ToList();
        }
    }
}
