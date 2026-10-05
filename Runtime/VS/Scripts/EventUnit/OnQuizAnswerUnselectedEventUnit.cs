using Virtuademy.Environments.ScriptingApi.Placeholders;

using Unity.VisualScripting;


using SPACS.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Quiz Answer: On Quiz Answer Unselected")]
    [UnitSurtitle("Quiz Answer")]
    [UnitShortTitle("On Quiz Answer Unselected")]
    [UnitCategory("Events\\Virtuademy")]
    public class OnQuizAnswerUnselectedEventUnit : AwaitableEventUnit<(QuizPlaceholder, QuizAnswer)>
    {
        [DoNotSerialize]
        public ValueOutput Quiz { get; private set; }
        public ValueOutput Answer { get; private set; }

        protected override void Definition()
        {
            base.Definition();
            // Setting the value on our port.
            Quiz = ValueOutput<QuizPlaceholder>(nameof(Quiz));
            Answer = ValueOutput<QuizAnswer>(nameof(Answer));
        }

        // Setting the value on our port.
        protected override void AssignArguments(Flow flow, (QuizPlaceholder, QuizAnswer) data)
        {
            flow.SetValue(Quiz, data.Item1);
            flow.SetValue(Answer, data.Item2);
        }

        public override EventHook GetHook(GraphReference reference)
        {
            return new EventHook("QuizPlaceholder" + this.ToString().Split("Unit")[0]);
        }
    }
}
