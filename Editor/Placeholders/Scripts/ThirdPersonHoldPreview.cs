using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Virtuademy.SDK.Environments;

namespace Virtuademy.Environments.ScriptingApi.Placeholders.Editor
{
    /// <summary>
    /// An edit-time mannequin holding a SpawnableObject the way the application holds it in third person on desktop, web
    /// and mobile (2026-10-29): the right hand at the hold offset from the chest (the item's own with Custom Third Person
    /// Hold, else the platform's default), the arm bent by an analytic two-bone solve with the elbow out and back, as the
    /// application's arm rig does, and the item gripped by its right-hand pivot (the one the VR hand holds it by). The
    /// mannequin is placed around the item, which never moves.
    /// </summary>
    /// <remarks>
    /// The mannequin is built from Unity primitives, ours to ship: no model, no licence, no IK package. Its objects are
    /// <see cref="HideFlags.DontSave"/>, so neither the scene nor the prefab keeps them, and it goes when the inspector
    /// closes, before a domain reload, when the scene or the prefab is saved, and when the item is gone. It is placed again
    /// when the inspector changes a field and when the item or its pivot moves (ObjectChangeEvents): no frame loop. An
    /// approximation: the application uses the avatar's own proportions and fits a tool longer than 0.5 m to the hand.
    /// </remarks>
    public sealed class ThirdPersonHoldPreview
    {
        // The application's default hold (ToolSystemWebGL.holdHandOffset in the app), from the chest, x to the side of the
        // holding arm, y up, z forward; and the elbow's hint (HeldToolPose)
        public static readonly Vector3 DefaultHoldOffset = new Vector3(0.2f, 0.15f, 0.4f);
        private static readonly Vector3 ElbowHintOffset = new Vector3(0.35f, -0.25f, -0.2f);
        // The fingers level, a little in towards the body (HeldToolPose.FingerDirection, mirrored for the right hand)
        private static readonly Vector3 FingerDirection = new Vector3(-0.15f, 0f, 1f);

        /// <summary>The mannequin's height, in metres, at which the hold offset is drawn as written.</summary>
        public const float ReferenceHeight = 1.75f;

        /// <summary>
        /// The height of the avatar to preview, for this editor session only: the application proportions the hold offset
        /// to each avatar's height (offset x height / ReferenceHeight, HeldToolPose); the item keeps its own size.
        /// </summary>
        public static float PreviewHeight { get; set; } = ReferenceHeight;

        // A 1.75 m mannequin, feet at its origin, facing +z
        private static readonly Vector3 Chest = new Vector3(0f, 1.35f, 0f);
        private static readonly Vector3 RightShoulder = new Vector3(0.18f, 1.42f, 0f);
        private const float UpperArm = 0.28f;
        private const float Forearm = 0.27f;
        // The grip point in the palm, along the fingers from the wrist (the application's palm centre)
        private const float PalmDepth = 0.07f;

        private readonly SpawnableObjectPlaceholder item;
        private GameObject root;

        public ThirdPersonHoldPreview(SpawnableObjectPlaceholder item)
        {
            this.item = item;
        }

        public bool IsShown => root != null;

        public void Show()
        {
            if (item == null || root != null)
            {
                return;
            }
            AssemblyReloadEvents.beforeAssemblyReload += Hide;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            Refresh();
        }

        public void Hide()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Hide;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            PrefabStage.prefabSaving -= OnPrefabSaving;
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
            root = null;
        }

        private void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) => Hide();

        private void OnPrefabSaving(GameObject prefab) => Hide();

        // The item or its pivot moved, or a field changed through Undo: placed again; the item gone: hidden
        private void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (item == null)
            {
                Hide();
                return;
            }
            for (int i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) != ObjectChangeKind.ChangeGameObjectOrComponentProperties)
                {
                    continue;
                }
                // Only the item's own hierarchy: the mannequin's changes would place it again forever
                stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out ChangeGameObjectOrComponentPropertiesEventArgs change);
                Object changed = EditorUtility.InstanceIDToObject(change.instanceId);
                Transform changedTransform = changed is Component component ? component.transform
                    : changed is GameObject gameObject ? gameObject.transform : null;
                if (changedTransform != null && changedTransform.IsChildOf(item.transform))
                {
                    Refresh();
                    return;
                }
            }
        }

        /// <summary>Builds the mannequin, or places it again, from the item's offset and pivot.</summary>
        public void Refresh()
        {
            if (item == null)
            {
                Hide();
                return;
            }
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }

            Vector3 offset = item.customThirdPersonHold ? item.thirdPersonHoldOffset : DefaultHoldOffset;
            Vector3 hand = Chest + offset;
            Vector3 hint = Chest + ElbowHintOffset;
            Vector3 elbow = SolveElbow(RightShoulder, hand, hint, out hand);
            Quaternion handRotation = Quaternion.LookRotation(FingerDirection, Vector3.up);
            Vector3 palm = hand + handRotation * new Vector3(0f, 0f, PalmDepth);

            root = new GameObject("ThirdPersonHoldPreview") { hideFlags = HideFlags.DontSave };
            // Into the item's scene: the prefab stage's when the prefab is open, else the scene's
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, item.gameObject.scene);
            BuildBody(root.transform, elbow, hand, handRotation);

            // The palm on the item's grip: its right-hand pivot, else the centre of its meshes
            GripPose(out Vector3 gripPosition, out Quaternion gripRotation);
            // The avatar scaled to the previewed height, the item at its own size, as in the application
            float scale = PreviewHeight / ReferenceHeight;
            root.transform.localScale = Vector3.one * scale;
            Quaternion rotation = gripRotation * Quaternion.Inverse(handRotation);
            root.transform.SetPositionAndRotation(gripPosition - rotation * (palm * scale), rotation);
        }

        private void GripPose(out Vector3 position, out Quaternion rotation)
        {
            Transform pivot = item.rightHandPivot;
            if (pivot != null && pivot.IsChildOf(item.transform))
            {
                position = pivot.position;
                rotation = pivot.rotation;
                return;
            }
            Bounds bounds = new Bounds(item.transform.position, Vector3.zero);
            bool found = false;
            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>())
            {
                if (found)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    found = true;
                }
            }
            position = bounds.center;
            rotation = item.transform.rotation;
        }

        // Two bones, shoulder to elbow to wrist, the elbow on the hint's side; a hand out of reach goes as far as the arm
        private static Vector3 SolveElbow(Vector3 shoulder, Vector3 target, Vector3 hint, out Vector3 hand)
        {
            Vector3 toTarget = target - shoulder;
            float distance = Mathf.Clamp(toTarget.magnitude, 0.01f, UpperArm + Forearm - 0.001f);
            Vector3 direction = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : Vector3.forward;
            hand = shoulder + direction * distance;
            float along = (UpperArm * UpperArm - Forearm * Forearm + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(UpperArm * UpperArm - along * along, 0f));
            Vector3 pole = Vector3.ProjectOnPlane(hint - shoulder, direction);
            pole = pole.sqrMagnitude > 1e-8f ? pole.normalized : Vector3.down;
            return shoulder + direction * along + pole * height;
        }

        private static void BuildBody(Transform parent, Vector3 elbow, Vector3 hand, Quaternion handRotation)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            {
                color = new Color(0.62f, 0.66f, 0.72f),
                hideFlags = HideFlags.DontSave,
            };
            Part(PrimitiveType.Sphere, parent, new Vector3(0f, 1.62f, 0f), Quaternion.identity, new Vector3(0.21f, 0.24f, 0.21f), material);
            Part(PrimitiveType.Capsule, parent, new Vector3(0f, 1.18f, 0f), Quaternion.identity, new Vector3(0.34f, 0.32f, 0.2f), material);
            Part(PrimitiveType.Capsule, parent, new Vector3(0.1f, 0.45f, 0f), Quaternion.identity, new Vector3(0.14f, 0.45f, 0.14f), material);
            Part(PrimitiveType.Capsule, parent, new Vector3(-0.1f, 0.45f, 0f), Quaternion.identity, new Vector3(0.14f, 0.45f, 0.14f), material);
            // The left arm hangs; the right one holds
            Limb(parent, new Vector3(-0.18f, 1.42f, 0f), new Vector3(-0.2f, 0.9f, 0f), 0.09f, material);
            Limb(parent, RightShoulder, elbow, 0.09f, material);
            Limb(parent, elbow, hand, 0.08f, material);
            Part(PrimitiveType.Cube, parent, hand + handRotation * new Vector3(0f, 0f, 0.05f), handRotation,
                new Vector3(0.03f, 0.09f, 0.1f), material);
        }

        private static void Limb(Transform parent, Vector3 from, Vector3 to, float thickness, Material material)
        {
            Vector3 along = to - from;
            Part(PrimitiveType.Capsule, parent, (from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, along),
                new Vector3(thickness, along.magnitude * 0.5f, thickness), material);
        }

        private static void Part(PrimitiveType type, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.hideFlags = HideFlags.DontSave;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
