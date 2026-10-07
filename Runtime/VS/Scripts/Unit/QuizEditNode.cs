using Virtuademy.Environments.ScriptingApi.Placeholders;

using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Quiz: Edit")]
    [UnitSurtitle("Quiz")]
    [UnitShortTitle("Edit")]
    [UnitCategory("Virtuademy\\Flow")]
    public class QuizEditNode : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput Quiz { get; private set; }

        protected override void Definition()
        {
            Quiz = ValueInput<QuizPlaceholder>(nameof(QuizPlaceholder), null).NullMeansSelf();

            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                f.GetValue<QuizPlaceholder>(Quiz).VSNode_Edit();
                return OutputTrigger;
            });

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }
    }
}
