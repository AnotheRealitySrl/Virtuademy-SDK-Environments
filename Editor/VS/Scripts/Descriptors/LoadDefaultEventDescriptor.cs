using Virtuademy.SDK.Environments.VisualScripting;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [Descriptor(typeof(LoadDefaultEventNode))]
    public class LoadDefaultEventDescriptor : UnitDescriptor<LoadDefaultEventNode>
    {
        public LoadDefaultEventDescriptor(LoadDefaultEventNode unit) : base(unit) { }

        protected override string DefinedSummary()
        {
            return "This unit requires a coroutine flow to run properly. This unit will " +
                "load the event that has been set as default. Using this node is " +
                "equivalent to pressing the \"home\" button on the HUD (or on the " +
                "VR tablet).";
        }
    }
}
