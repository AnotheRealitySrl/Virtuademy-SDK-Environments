using SPACS.Tasks;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;

using static SPACS.Tasks.TaskNode;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>
    /// The logic of a task element: added by the runtime beside its placeholder (never by a creator), it detects the
    /// element's action and reports the result on the element's task (through <see cref="TaskReports"/>, so a procedure
    /// scores it), or without a task shows it on the element's own cue. Event-driven; a TaskReactor leaves it alone.
    /// </summary>
    [AddComponentMenu("")]
    public abstract class ElementDriver : MonoBehaviour, ITaskReactorExempt, IElementSink
    {
        protected ElementPlaceholderBase Element { get; private set; }

        internal void Bind(ElementPlaceholderBase element)
        {
            Element = element;
            element.Sink = this;
            OnBind();
        }

        /// <summary>Subscribes to what the element detects.</summary>
        protected abstract void OnBind();

        /// <summary>The element's task, when it has one.</summary>
        protected Task TaskOf() => Element.Task != null && Element.Task.TryGetComponent(out Task task) ? task : null;

        public void Report(TaskResult result) => Report(result, result.Outcome != ETaskOutcome.Wrong);

        /// <summary>Reports <paramref name="result"/>: completing the task, or as an attempt.</summary>
        protected void Report(TaskResult result, bool completes)
        {
            Task task = TaskOf();
            if (task != null)
            {
                if (!MayReport(task))
                {
                    return;
                }
                if (completes)
                {
                    TaskReports.Complete(task, result);
                }
                else
                {
                    TaskReports.Attempt(task, result);
                }
            }
            else
            {
                ShowOnOwnCue(result.Outcome != ETaskOutcome.Wrong);
            }
            Element.RaiseReported(result);
        }

        /// <summary>Whether the element reports on its task now: while it is active, or while it is locked when the
        /// procedure counts out-of-order actions as errors; always without Report Only When Task Active.</summary>
        protected bool MayReport(Task task)
        {
            if (!Element.ReportOnlyWhenTaskActive)
            {
                return true;
            }
            TaskStatus status = task.Node.Status;
            if (status == TaskStatus.Todo || status == TaskStatus.InProgress)
            {
                return true;
            }
            ProcedureRun procedure = ProcedureOf(task);
            return status == TaskStatus.Locked && procedure != null && procedure.TakesOutOfOrder;
        }

        protected static ProcedureRun ProcedureOf(Task task)
        {
            TaskSystemVirtuademy system = task != null ? task.GetComponentInParent<TaskSystemVirtuademy>(true) : null;
            return system != null ? system.Procedure : null;
        }

        /// <summary>Without a task: the element's own cue shows the result (to everyone, when it is networked).</summary>
        protected void ShowOnOwnCue(bool right)
        {
            if (TryGetComponent(out InteractionCuePlaceholder cue))
            {
                cue.ShowResult(right);
            }
        }
    }
}
