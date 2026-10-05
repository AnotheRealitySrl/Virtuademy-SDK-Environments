using Unity.VisualScripting;
using UnityEngine;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy CMUser: Get Character Left Hand")]
    [UnitSurtitle("Character Left Hand")]
    [UnitShortTitle("Get Character Left Hand")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetLeftHandTransformNode : Unit
    {

        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput CharacterLeftHand { get; private set; }

        //private Transform _characterReference;

        protected override void Definition()
        {
            CharacterLeftHand = ValueOutput<Transform>(nameof(CharacterLeftHand), (flow) => IVirtuademyGameplay.Current.Player.LeftHand);
        }
    }
}
