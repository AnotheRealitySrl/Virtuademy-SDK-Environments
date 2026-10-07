using Unity.VisualScripting;
using UnityEngine;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy CMUser: Get Character Head Transform")]
    [UnitSurtitle("Character Head Transform")]
    [UnitShortTitle("Get Character Head Transform")]
    [UnitCategory("Virtuademy\\Get")]
    public class GetCharacterHeadNode : Unit
    {

        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueOutput CharacterHeadReference { get; private set; }

        protected override void Definition()
        {
            CharacterHeadReference = ValueOutput<Transform>(nameof(CharacterHeadReference), (flow) => IVirtuademyGameplay.Current.Player.Head);
        }
    }
}
