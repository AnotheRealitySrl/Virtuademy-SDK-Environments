using Unity.VisualScripting;
using UnityEngine;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy CMUser: Get Character Transform")]
    [UnitSurtitle("Character Transform")]
    [UnitShortTitle("Get Character Transform")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetPlayerNode : Unit
    {

        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput CharacterTransform { get; private set; }

        //private Transform _characterReference;

        protected override void Definition()
        {
            CharacterTransform = ValueOutput<Transform>(nameof(CharacterTransform), (flow) => IVirtuademyGameplay.Current.Player.Root);
        }
    }
}
