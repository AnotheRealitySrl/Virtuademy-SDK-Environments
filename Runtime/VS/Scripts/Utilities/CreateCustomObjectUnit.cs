
using System.Collections.Generic;
using System.Linq;

using Unity.VisualScripting;


using SPACS.Utilities;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Custom Object: Create")]
    [UnitSurtitle("Virtuademy Custom Object")]
    [UnitShortTitle("Create")]
    [UnitCategory("Virtuademy\\Create")]
    public class CreateCustomObjectUnit : Unit
    {
        [SerializeAs(nameof(CustomEntriesCount))]
        private int customFieldsCount = 2;

        [DoNotSerialize]
        [Inspectable, UnitHeaderInspectable("Custom fields")]
        public int CustomEntriesCount
        {
            get => customFieldsCount;
            set => customFieldsCount = value;
        }


        [DoNotSerialize]
        public List<ValueInput> CustomFields { get; private set; }

        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput Object { get; private set; }

        protected override void Definition()
        {
            Object = ValueOutput(nameof(Object),
                (flow) =>
                {
                    var customFields = CustomFields.Select((x) =>
                    {
                        return flow.GetConvertedValue(x) as Field;
                    });
                    CustomType customType = new CustomType();
                    foreach (var field in customFields)
                    {
                        customType.Fields[field.name] = field.value;
                    }
                    return customType;
                });

            CustomFields = new List<ValueInput>();

            for (var i = 0; i < CustomEntriesCount; i++)
            {
                var customProperty = ValueInput<Field>("Custom_Field_" + i);
                CustomFields.Add(customProperty);
                Requirement(customProperty, Object);
            }

        }


    }
}