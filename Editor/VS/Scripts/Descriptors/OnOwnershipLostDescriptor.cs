using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [Descriptor(typeof(OnOwnershipLostEventUnit))]
    public class OnOwnershipLostDescriptor : UnitDescriptor<OnOwnershipLostEventUnit>
    {
        public OnOwnershipLostDescriptor(OnOwnershipLostEventUnit unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "This event unit will be triggered ONLY by the owner of an object when he loses the ownership on that object.";
        }
    }
}
