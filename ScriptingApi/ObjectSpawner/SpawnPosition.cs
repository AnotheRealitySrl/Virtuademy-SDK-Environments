using System;

using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.ObjectSpawner
{
    [Serializable]
    public class SpawnPosition
    {
        [SerializeField]
        private int fovAngle = 90;
        [SerializeField]
        private int maxFov = 180;
        [SerializeField]
        private int rayCount = 2;
        [SerializeField]
        private float viewDistance = 50f;
        [SerializeField]
        private float startingAngle = 0;
        [SerializeField]
        private Vector3 originOffset = Vector3.zero;
        [SerializeField]
        private LayerMask layerMask;

        public int FovAngle { get => fovAngle; }
        public int RayCount { get => rayCount; }
        public float ViewDistance { get => viewDistance; }
        public float StartingAngle { get => startingAngle; }
        public Vector3 OriginOffset { get => originOffset; }
        public LayerMask LayerMask { get => layerMask; }
        public int MaxFov { get => maxFov; set => maxFov = value; }
    }
}