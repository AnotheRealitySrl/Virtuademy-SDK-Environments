using Virtuademy.Environments.ScriptingApi.Interaction;
using Virtuademy.Environments.ScriptingApi.Placeholders;
using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Manipulable: Release Manipulable")]
    [UnitSurtitle("Manipulable")]
    [UnitShortTitle("Release Manipulable")]
    [UnitCategory("Virtuademy\\Flow")]
    public class ReleaseManipulableUnit : Unit
    {
        [DoNotSerialize]
        public ValueInput Manipulable { get; private set; }

        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput inputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput outputTrigger { get; private set; }

        protected override void Definition()
        {
            inputTrigger = ControlInput(nameof(inputTrigger), Output);
            outputTrigger = ControlOutput("outputTrigger");
            Manipulable = ValueInput<ManipulablePlaceholder>(nameof(Manipulable), null).NullMeansSelf();
            Succession(inputTrigger, outputTrigger);

        }

        private ControlOutput Output(Flow flow)
        {
            flow.GetValue<ManipulablePlaceholder>(Manipulable).gameObject.GetComponent<IManipulable>().ForceGrabRelease();
            return outputTrigger;
        }
    }

}
