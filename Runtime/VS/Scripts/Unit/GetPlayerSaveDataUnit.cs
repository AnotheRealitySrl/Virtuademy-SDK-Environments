
using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Player Save Data: Get Data")]
    [UnitSurtitle("Player Save Data")]
    [UnitShortTitle("Get Player Save Data")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetPlayerSaveDataUnit : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput Key { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput Data { get; private set; }

        protected override void Definition()
        {
            Key = ValueInput(nameof(Key), string.Empty);

            Data = ValueOutput(nameof(Data),
                (f) =>
            {
                return IVirtuademyFramework.Current.SaveData.Get(f.GetValue<string>(Key));
            });
        }
    }
}
