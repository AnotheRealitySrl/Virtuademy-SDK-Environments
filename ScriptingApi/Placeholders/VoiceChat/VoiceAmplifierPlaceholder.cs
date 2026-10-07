using System.Collections.Generic;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public class VoiceAmplifierPlaceholder : SceneComponentPlaceholderNetwork
    {
        public enum AmplifierStates
        {
            NotAmplified = 1,
            Amplified = 2
        }

        [Header("Animation references")]
        [SerializeField] private float volumeAmplified;
        [SerializeField] private float rangeAmplified;
        [SerializeField] private AmplifierStates state = AmplifierStates.NotAmplified;
        [SerializeField] private List<GameObject> connectables;

        public float VolumeAmplified => volumeAmplified;
        public float RangeAmplified => rangeAmplified;
        public AmplifierStates State => state;
        public List<GameObject> Connectables => connectables;
    }
}
