using System;
using System.Threading.Tasks;
using UnityEngine.Events;

using Virtuademy.Environments.ScriptingApi.Interaction;

namespace Virtuademy.Environments.ScriptingApi.Interaction
{
    public interface IManipulable : IInteractableBehaviour
    {
        UnityEvent OnGrabManipulableStart { get; set; }
        UnityEvent OnGrabManipulableEnd { get; set; }
        UnityEvent OnRayGrabManipulableStart { get; set; }
        UnityEvent OnRayGrabManipulableEnd { get; set; }
        UnityEvent<EManipulableState> OnCurrentStateChange { get; }

        EManipulableState CurrentInteractionState { get; }
        EManipulationInput CurrentManipulationInput { get; }
        EManipulationMode ManipulationMode { get; }

        public enum EManipulableState
        {
            Idle,
            BeforeManipulating,
            Manipulating
        }

        [Flags]
        public enum EManipulationMode
        {
            Translate = 1,
            Rotate = 2,
            Scale = 4
        }

        [Flags]
        public enum EVRInteraction
        {
            RayInteraction = 1,
            Hands = 2
        }

        public Task Setup();

        public void ForceGrabRelease();
    }
}
