

using System.Collections.Generic;

using Unity.VisualScripting;
using System;
using System.Reflection;
using System.Linq;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Camera: Set camera mode")]
    [UnitSurtitle("SetCameraMode")]
    [UnitShortTitle("Set Camera Mode")]
    [UnitCategory("Virtuademy\\Flow")]
    public class SetCameraModeNode : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        public ValueInput ConstrainedRotation { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        public ValueInput StaticCamera { get; private set; }


        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        protected override void Definition()
        {
            ConstrainedRotation = ValueInput<bool>(nameof(ConstrainedRotation),false).NullMeansSelf();
            StaticCamera = ValueInput<bool>(nameof(StaticCamera), false).NullMeansSelf();
            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {               
                    bool constrainRotation = f.GetValue<bool>(ConstrainedRotation);
                    if (f.GetValue<bool>(StaticCamera))
                    {
                        IVirtuademyGameplay.Current.Player.UseStaticCamera(constrainRotation);
                    }
                    else
                    {
                        IVirtuademyGameplay.Current.Player.UseFreeCamera(constrainRotation);
                    }
                    return OutputTrigger;
            });
           

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }

    }
}
