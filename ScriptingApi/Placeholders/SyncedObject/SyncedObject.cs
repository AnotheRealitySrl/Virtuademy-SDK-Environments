using System;
using System.Collections.Generic;

using Unity.VisualScripting;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Marks an object the session keeps in step across clients, and carries the API a script uses
    /// to drive it: ask for the right to move it, give it back, and hear when it changes hands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One client at a time may drive a synced object. <see cref="RequestOwnership"/> asks for that
    /// right and the answer arrives as an event, never as a return value — another client has to
    /// agree, and that takes a round trip. <see cref="OwnershipRequestType"/> decides which of the
    /// three answers this object gives.
    /// </para>
    /// <para>
    /// Nothing here is meaningful outside a multiplayer session, and that is deliberate rather than
    /// unfinished: <see cref="IsOwnedLocally"/> reports true when there is nobody to contend with,
    /// the two requests do nothing, and no event fires. A script written against this runs
    /// unchanged in a single-player world instead of having to branch.
    /// </para>
    /// <para>
    /// <b>The ownership is not implemented here and cannot be.</b> It is network state, and this
    /// assembly has no network — that is the whole reason it is one a creator's script may name.
    /// The platform fills in the wiring below when the object joins a session; until it does, the
    /// object answers as a single-player one.
    /// </para>
    /// </remarks>
    public class SyncedObject : SceneComponentPlaceholderNetwork
    {
        /// <summary>How this object answers a request for ownership.</summary>
        public enum OwnershipRequestEnum
        {
            /// <summary>Ask the current owner, who may refuse.</summary>
            Request,

            /// <summary>Take it without asking.</summary>
            Takeover,

            /// <summary>It never changes hands; a request is an error.</summary>
            Fixed
        }

        [SerializeField] private OwnershipRequestEnum ownershipRequestType;

        [HideInInspector, SerializeField] private bool syncTransform = true;

        // Written by OnValidate below and read by nobody else. They identify the prefab this
        // instance came from and the instance itself, which is how a spawned object is matched
        // back to its authoring counterpart.
        [HideInInspector, SerializeField] private string assetID;
        [HideInInspector, SerializeField] private string instanceID;

        /// <summary>Which of the three answers this object gives to a request. Set in the inspector.</summary>
        public OwnershipRequestEnum OwnershipRequestType => ownershipRequestType;

        /// <summary>Whether the platform keeps this object's transform in step. Set in the inspector.</summary>
        public bool SyncTransform => syncTransform;

        // ------------------------------------------------------------------- what a script calls

        /// <summary>
        /// Asks for the right to drive this object. The answer arrives as <see cref="OwnerChanged"/>
        /// or <see cref="OwnershipRequestFailed"/>.
        /// </summary>
        public void RequestOwnership() => onRequestOwnership?.Invoke();

        /// <summary>
        /// Gives the right back, so another client can take it. A script that took ownership to
        /// animate something should release it when the animation ends.
        /// </summary>
        public void ReleaseOwnership() => onReleaseOwnership?.Invoke();

        /// <summary>
        /// Whether the local client may drive this object right now. True when there is nothing to
        /// contend with — a single-player session, or an object the platform has not wired yet.
        /// </summary>
        public bool IsOwnedLocally => onCheckOwnership?.Invoke() ?? true;

        /// <summary>The local client has taken ownership. The success half of <see cref="RequestOwnership"/>.</summary>
        public event Action OwnerChanged;

        /// <summary>
        /// The local client no longer owns this object — it was taken, or the client left the
        /// shard. Anything a script was driving on it should stop here.
        /// </summary>
        public event Action OwnerLost;

        /// <summary>A request for ownership was refused.</summary>
        public event Action OwnershipRequestFailed;

        // -------------------------------------------------------------- what the platform fills in
        //
        // Reachable from a script, and that is a judgement rather than an oversight: rewiring these
        // breaks the object for the client that did it and for nobody else, because what actually
        // propagates over the network is decided by the real owner, not by this component's answer.
        // The perimeter exists to keep a script away from the platform, not away from its own
        // scene. If that judgement ever changes, six entries in the policy's denied members close
        // them with no change here.

        /// <summary>Platform wiring. Invoked by <see cref="RequestOwnership"/>.</summary>
        public Action onRequestOwnership;

        /// <summary>Platform wiring. Invoked by <see cref="ReleaseOwnership"/>.</summary>
        public Action onReleaseOwnership;

        /// <summary>Platform wiring. Read by <see cref="IsOwnedLocally"/>.</summary>
        public Func<bool> onCheckOwnership;

        /// <summary>Platform wiring. An event cannot be raised from outside the type that declares it.</summary>
        public void RaiseOwnerChanged() => OwnerChanged?.Invoke();

        /// <inheritdoc cref="RaiseOwnerChanged"/>
        public void RaiseOwnerLost() => OwnerLost?.Invoke();

        /// <inheritdoc cref="RaiseOwnerChanged"/>
        public void RaiseOwnershipRequestFailed() => OwnershipRequestFailed?.Invoke();

        // ------------------------------------------------------- the variables this object syncs

        /// <summary>
        /// The current value of a synced variable, or null if this object declares none by that
        /// name. Values are whatever the graph put in them, so the caller casts.
        /// </summary>
        public object GetSyncedVariable(string name) => Find(name)?.DeclarationValue;

        /// <summary>
        /// Sets a synced variable and lets the change travel to the other clients.
        /// </summary>
        /// <remarks>
        /// Writes through the variables collection rather than the declaration it holds, and that
        /// is not incidental: the collection is what raises the change notification the platform
        /// listens to. Assigning the declaration directly would update the value here and tell
        /// nobody.
        /// </remarks>
        public void SetSyncedVariable(string name, object value)
        {
            if (Find(name) == null)
            {
                return;
            }

            Variables variables = GetComponentInChildren<Variables>(true);

            if (variables != null)
            {
                variables.declarations[name] = value;
            }
        }

        /// <summary>
        /// Whether a synced variable has ever moved off the value it was authored with. False for
        /// a name this object does not declare, and false for one nobody has touched yet.
        /// </summary>
        public bool HasSyncedVariableChanged(string name) => Find(name)?.hasChanged ?? false;

        /// <summary>
        /// A synced variable took a new value, on this client or another one. Carries the name and
        /// the value, so a script does not have to read it back.
        /// </summary>
        public event Action<string, object> SyncedVariableChanged;

        /// <summary>Platform wiring. Raised wherever the graph's equivalent node is triggered.</summary>
        public void RaiseSyncedVariableChanged(string name, object value)
            => SyncedVariableChanged?.Invoke(name, value);

        private SyncedVariables.Data Find(string name)
        {
            SyncedVariables variables = GetComponent<SyncedVariables>();

            if (variables == null || variables.variableSettings == null)
            {
                return null;
            }

            foreach (SyncedVariables.Data data in variables.variableSettings)
            {
                if (data.name == name)
                {
                    return data;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Removes the hidden variables component. Back on the component itself now that both live
        /// in the same assembly.
        /// </summary>
        [ContextMenu("Remove Synced Variables")]
        private void RemoveSyncedVariables()
        {
            if (TryGetComponent(out SyncedVariables variables))
            {
                DestroyImmediate(variables);
            }
        }
#endif

#if UNITY_EDITOR
        protected void OnValidate()
        {
            if (Application.isPlaying)
                return;

            PrefabInstanceStatus prefabInstanceStatus = PrefabUtility.GetPrefabInstanceStatus(this);
            PrefabAssetType prefabAssetType = PrefabUtility.GetPrefabAssetType(this);

            bool isPrefab = prefabAssetType != PrefabAssetType.NotAPrefab;
            bool isPrefabInstance = isPrefab && prefabInstanceStatus == PrefabInstanceStatus.Connected;
            bool isPrefabAsset = isPrefab && prefabInstanceStatus == PrefabInstanceStatus.NotAPrefab;

            // set assetID if it's a prefab or instance of prefab
            if (isPrefabAsset || isPrefabInstance)
            {
                string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
                string assetGUID = AssetDatabase.AssetPathToGUID(assetPath);
                if (assetID != assetGUID)
                {
                    assetID = assetGUID;
                }
            }
            else
            {
                assetID = null;
            }

            // set instance ID if it's an instance in the scene
            // or embedded within a prefab object
            if (isPrefabAsset)
            {
                instanceID = null;
            }
            else
            {
                HashSet<string> allInstanceIDs = new HashSet<string>();
                SyncedObject[] allInstanceSyncedObjects = GameObject.FindObjectsOfType<SyncedObject>();
                foreach (SyncedObject syncedObject in allInstanceSyncedObjects)
                {
                    if (syncedObject == this)
                        continue;

                    allInstanceIDs.Add(syncedObject.instanceID);
                }

                while (string.IsNullOrEmpty(instanceID) || allInstanceIDs.Contains(instanceID))
                {
                    instanceID = System.Guid.NewGuid().ToString();
                }
            }
        }
#endif
    }
}
