using UnityEngine;
using UnityEngine.UIElements;

using Virtuademy.Environments.ScriptingApi.Tasks;
using Virtuademy.SDK.Environments.UIKit;
using Virtuademy.SDK.Environments.Utilities;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>
    /// The procedure's summary: outcome, score, time, errors, hints, one row per scored task, and (page 2, "What
    /// happened") the timeline; Retry and Exit. A runtime view in <c>ProcedureSummary.prefab</c> (a Virtuademy panel
    /// placed by <see cref="OverlayPanelPlacement"/>: over the screen, in front of the user in VR); its settings are the
    /// Procedure placeholder's. Built once when shown.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class ProcedureSummaryPanel : UIDocumentBinder
    {
        /// <summary>The tallest the list of tasks or of events gets before it scrolls.</summary>
        private const float MaxListHeight = 360f;

        private ProcedureRun run;
        private ProcedureSummary summary;
        private VisualElement panelRoot, content, tasksPage, timelinePage;
        private Label title, body;
        private Button primary, secondary, toggle;
        private bool showingTimeline;
        private bool waiting;

        /// <summary>Shows <paramref name="shown"/> for <paramref name="procedureRun"/>.</summary>
        internal void Show(ProcedureRun procedureRun, ProcedureSummary shown)
        {
            run = procedureRun;
            summary = shown;
            showingTimeline = false;
            waiting = false;
            gameObject.SetActive(true);
            Build();
        }

        internal void Hide() => gameObject.SetActive(false);

        /// <summary>While Retry or Exit waits for the analytics to be sent: the buttons are off and say so.</summary>
        internal void SetWaiting(bool value)
        {
            waiting = value;
            if (primary != null)
            {
                primary.SetEnabled(!value);
                secondary.SetEnabled(!value);
                body.text = value ? PanelEnvironment.Text("Procedure/Summary/Saving", "Saving the results...") : ScoreLine();
            }
        }

        protected override bool BindTo(VisualElement root)
        {
            panelRoot = root.Q<VisualElement>("panel");
            content = root.Q<VisualElement>("content");
            title = root.Q<Label>("title");
            body = root.Q<Label>("body");
            primary = root.Q<Button>("primary");
            secondary = root.Q<Button>("secondary");
            if (panelRoot == null || content == null || title == null || body == null || primary == null || secondary == null)
            {
                Debug.LogWarning("[ProcedureSummary] '" + name + "': the document is not a Virtuademy panel.", this);
                return false;
            }
            PanelEnvironment.IgnoreExceptControls(root);
            primary.clicked += OnRetry;
            secondary.clicked += OnExit;
            WatchForRebuild(panelRoot);
            Build();
            return true;
        }

        protected override void UnbindFromTree()
        {
            if (primary != null)
            {
                primary.clicked -= OnRetry;
                secondary.clicked -= OnExit;
            }
            panelRoot = null;
        }

        private string ScoreLine() => summary == null ? "" :
            PanelEnvironment.Text("Procedure/Summary/Score", "Score") + " " + summary.Score + "/" + summary.MaxScore
            + " (" + Mathf.RoundToInt(summary.Percent) + "%)";

        private static string Time(float seconds)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        private void Build()
        {
            if (panelRoot == null || summary == null)
            {
                return;
            }
            bool passed = summary.Outcome == EProcedureOutcome.Passed;
            panelRoot.EnableInClassList("vd-panel--success", passed);
            panelRoot.EnableInClassList("vd-panel--error", !passed);
            title.text = passed ? PanelEnvironment.Text("Procedure/Summary/Passed", "Passed")
                : summary.Outcome == EProcedureOutcome.Abandoned ? PanelEnvironment.Text("Procedure/Summary/Abandoned", "Not finished")
                : PanelEnvironment.Text("Procedure/Summary/NotPassed", "Not passed") + FailReasonText();
            body.text = waiting ? body.text : ScoreLine();
            primary.text = PanelEnvironment.Text("Procedure/Summary/Retry", "Retry");
            secondary.text = PanelEnvironment.Text("Procedure/Summary/Exit", "Exit");

            // Page 1: the numbers and one row per scored task. Page 2: the timeline.
            content.Q<VisualElement>("proc-pages")?.RemoveFromHierarchy();
            // A list that scrolls past MaxListHeight, so a long timeline never pushes the buttons off the panel (editor
            // round 8: 19 rows covered What happened, Exit and Retry). On the screen that is reference pixels, in a world
            // panel millimetres.
            ScrollView pages = new(ScrollViewMode.Vertical) { name = "proc-pages" };
            pages.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            pages.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            pages.style.maxHeight = MaxListHeight;
            pages.style.flexShrink = 1;
            pages.contentContainer.pickingMode = PickingMode.Ignore;
            tasksPage = new VisualElement { pickingMode = PickingMode.Ignore };
            tasksPage.Add(Line(PanelEnvironment.Text("Procedure/Summary/Time", "Time") + " " + Time(summary.ElapsedSeconds)
                + "   " + PanelEnvironment.Text("Procedure/Errors", "Errors") + " " + summary.Errors
                + "   " + PanelEnvironment.Text("Procedure/Summary/Hints", "Hints") + " " + summary.HintsUsed, true));
            foreach (ProcedureTaskRow row in summary.Tasks)
            {
                string mark = row.State == ETaskState.Failed ? "x" : row.Points >= row.MaxPoints ? "+" : "~";
                tasksPage.Add(Line(mark + "  " + row.Name + "   " + row.Points + "/" + row.MaxPoints, false));
            }
            timelinePage = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (ProcedureTimelineEntry entry in summary.Timeline)
            {
                string line = Time(entry.Seconds) + " | " + (entry.Step.Length > 0 ? entry.Step : "-")
                    + (entry.Actor.Length > 0 ? " | " + entry.Actor : "")
                    + " | " + (entry.Good ? PanelEnvironment.Text("Procedure/Summary/Good", "Good")
                        : PanelEnvironment.Text("Procedure/Summary/Mistake", "Mistake"))
                    + (entry.LostPoints > 0f ? " · -" + Mathf.RoundToInt(entry.LostPoints) : "")
                    + (entry.Reason.Length > 0 ? " | " + entry.Reason : "");
                timelinePage.Add(Line(line, false));
            }
            pages.Add(tasksPage);
            pages.Add(timelinePage);
            content.Add(pages);
            ShowPage();

            toggle?.RemoveFromHierarchy();
            toggle = null;
            if (summary.Timeline.Length > 0)
            {
                toggle = new Button(() => { showingTimeline = !showingTimeline; ShowPage(); }) { name = "proc-toggle" };
                toggle.AddToClassList("vd-button");
                toggle.AddToClassList("vd-button--secondary");
                secondary.parent.Insert(0, toggle);
            }
            ShowPage();
        }

        private void ShowPage()
        {
            if (tasksPage == null)
            {
                return;
            }
            tasksPage.style.display = showingTimeline ? DisplayStyle.None : DisplayStyle.Flex;
            timelinePage.style.display = showingTimeline ? DisplayStyle.Flex : DisplayStyle.None;
            if (toggle != null)
            {
                toggle.text = showingTimeline ? PanelEnvironment.Text("Procedure/Summary/Results", "Results")
                    : PanelEnvironment.Text("Procedure/Summary/WhatHappened", "What happened");
            }
        }

        private string FailReasonText() => summary.FailReason switch
        {
            EProcedureFailReason.MaxErrors => ": " + PanelEnvironment.Text("Procedure/Summary/TooManyErrors", "too many errors"),
            EProcedureFailReason.TimeLimit => ": " + PanelEnvironment.Text("Procedure/Summary/TimeUp", "time is up"),
            EProcedureFailReason.TaskFailed => ": " + PanelEnvironment.Text("Procedure/Summary/StepFailed", "a step failed"),
            _ => "",
        };

        private static Label Line(string text, bool strong)
        {
            Label label = new(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(strong ? "vd-body" : "vd-caption");
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private void OnRetry()
        {
            if (!waiting)
            {
                run?.Retry();
            }
        }

        private void OnExit()
        {
            if (!waiting)
            {
                run?.Exit();
            }
        }
    }
}
