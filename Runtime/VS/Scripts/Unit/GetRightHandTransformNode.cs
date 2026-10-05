using Unity.VisualScripting;
using UnityEngine;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy CMUser: Get Character Right Hand")]
    [UnitSurtitle("Character Right Hand")]
    [UnitShortTitle("Get Character Right Hand")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetRightHandTransformNode : Unit
    {

        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput CharacterRightHand { get; private set; }

        //private Transform _characterReference;

        protected override void Definition()
        {
            CharacterRightHand = ValueOutput<Transform>(nameof(CharacterRightHand), (flow) => IVirtuademyGameplay.Current.Player.RightHand);
        }
    }
}
