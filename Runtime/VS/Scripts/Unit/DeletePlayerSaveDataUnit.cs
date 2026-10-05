using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Player Save Data: Delete Data")]
    [UnitSurtitle("Player Save Data")]
    [UnitShortTitle("Delete Player Save Data")]
    [UnitCategory("Virtuademy\\Flow")]
    public class DeletePlayerSaveDataUnit : Unit
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


        protected override void Definition()
        {
            Key = ValueInput<string>(nameof(Key), string.Empty);

            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                IVirtuademyFramework.Current.SaveData.Delete(
                    f.GetValue<string>(Key));
                return OutputTrigger;
            });

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }
    }
}
