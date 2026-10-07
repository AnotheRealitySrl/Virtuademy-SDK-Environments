using Newtonsoft.Json.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace Virtuademy.SDK.Environments.VisualScripting
{

    [UnitTitle("Virtuademy JSON: Serialize")]
    [UnitSurtitle("JSON")]
    [UnitShortTitle("Serialize")]
    [UnitCategory("Virtuademy\\Flow")]

    public class ObjToJSONNode : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }

        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        [DoNotSerialize]
        public ValueInput ObjectToParse { get; private set; }

        [DoNotSerialize]
        public ValueOutput JSONString { get; private set; }

        protected override void Definition()
        {
            ObjectToParse = ValueInput<object>(nameof(ObjectToParse), null);

            JSONString = ValueOutput(nameof(JSONString), (f) => ConvertToJSON(f.GetValue<object>(ObjectToParse)));
        }


        private string ConvertToJSON(object objToConvert)
        {
            string json = JsonUtility.ToJson(objToConvert);
            return json;
        }
    }
}
