
using Unity.VisualScripting;


using SPACS.Utilities;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Field: Create")]
    [UnitSurtitle("Virtuademy Field")]
    [UnitShortTitle("Create")]
    [UnitCategory("Virtuademy\\Create")]
    public class CreateFieldUnit : Unit
    {
        [DoNotSerialize]
        public ValueInput Name { get; private set; }

        [DoNotSerialize]
        public ValueInput Value { get; private set; }

        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput Field { get; private set; }

        protected override void Definition()
        {
            Name = ValueInput<string>(nameof(Name), null);

            Value = ValueInput<object>(nameof(Value), null);

            Field = ValueOutput(nameof(Field),
                (flow) => new Field((string)flow.GetConvertedValue(Name), flow.GetConvertedValue(Value)));
        }


    }
}