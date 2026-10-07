using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Player Save Data: Set Data")]
    [UnitSurtitle("Player Save Data")]
    [UnitShortTitle("Set Player Save Data")]
    [UnitCategory("Virtuademy\\Flow")]
    public class SetPlayerSaveDataUnit : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        public ValueInput Key { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        public ValueInput Value { get; private set; }

        protected override void Definition()
        {
            Key = ValueInput<string>(nameof(Key), string.Empty);

            Value = ValueInput<object>(nameof(Value), null);

            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                IVirtuademyFramework.Current.SaveData.Set(
                    f.GetValue<string>(Key),
                    f.GetValue<object>(Value));
                return OutputTrigger;
            });

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }
    }
}
