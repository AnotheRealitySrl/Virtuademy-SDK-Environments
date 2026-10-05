using UnityEngine;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class InputFunctionSetter : MonoBehaviour
    {
        public void SetDefaultInputs()
        {
            IVirtuademyGameplay.Current.Player.ApplyDefaultInputSettings();
        }

        public void SetStaticCamera ()
        {
            IVirtuademyGameplay.Current.Player.UseStaticCamera(false);
        }

        public void SetRotationCamera(bool constrainedRotation)
        {
            IVirtuademyGameplay.Current.Player.UseDragRotationCamera(constrainedRotation);
        }
    }
}
