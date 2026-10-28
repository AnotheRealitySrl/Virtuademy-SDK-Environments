using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.InputSystem;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Single placeholder for the two systems that hold pickable objects: the tools (radial menu,
    /// formerly <see cref="ToolInventoryPlaceholder"/>) and the general inventory (slots, formerly
    /// <see cref="InventorySlotPlaceholder"/>). At most one of them is active at a time, chosen by
    /// <see cref="InventoryMode"/>: in VR the same controller button opens either UI.
    /// At scene start only the selected system is initialized and shown (none with <see cref="EInventoryMode.None"/>).
    /// Changing <see cref="InventoryMode"/> at runtime raises <see cref="InventoryModeChanged"/>, on which the
    /// runtime hides the current system and initializes (if needed) and shows the new one.
    /// The fields of both systems are serialized, because either one can be activated at runtime.
    /// </summary>
    public class InventoryPlaceholder : SpawnableHandlerPlaceholder
    {
        public enum EInventoryMode
        {
            None = 0,
            Tools = 1,
            Inventory = 2,
        }

        public enum EToolLayout
        {
            Radial = 0,
            Grid = 1,
        }

        /// <summary>What an equipment slot holds: the tools (the hand), or the wearables of one wear slot.</summary>
        public enum EEquipmentSlotType
        {
            Hand = 0,
            Custom = 1,
        }

        /// <summary>
        /// An equipment slot declared by the environment: its type, and for a custom slot the name the wearables
        /// give in <see cref="WearablePlaceholder.wearSlot"/> (Head, Hands, Badge...).
        /// </summary>
        [Serializable]
        public class EquipmentSlotDefinition
        {
            [Tooltip("Hand: the tools. Custom: the wearables whose Wear Slot is this slot's name")]
            public EEquipmentSlotType type = EEquipmentSlotType.Custom;

            [Tooltip("Custom slot: the name the wearables give in their Wear Slot (Head, Hands, Badge...), which matches "
                     + "them to this slot; shown on the slot when it has no icon")]
            public string name;

            [Tooltip("Optional: the icon shown on the slot to say its category, in place of its name. The hand slot "
                     + "always shows the application's hand icon")]
            public Sprite icon;
        }

        [SerializeField, Tooltip("Which system is active: none, the tools radial menu or the inventory of pickable items. It can be changed at runtime")]
        private EInventoryMode inventoryMode = EInventoryMode.Inventory;

        #region Tools

        [HideInInspector] public EToolLayout inventoryLayout = EToolLayout.Radial; //Hide for now --> Display in the future

        [Header("Tools")]
        [Tooltip("The list of the items that are already inside the menu")]
        public List<ToolItemPlaceholder> inventoryItems;

        [Tooltip("The distance offset of the radialMenu from the camera")]
        public float zOffset = 0.8f;

        [Tooltip("The distance offset of the radialMenu on the y axis")]
        public float yOffset = 0.15f;

        [Tooltip("The radius that the items are going to use when opening the radialMenu")]
        public float radius = 0.3f;

        [Tooltip("The speed with which the radialMenu will be opened")]
        public float openSpeed = 0.25f;

        [Tooltip("the input pressed in order to open and close the radialMenu")]
        public InputAction action;

        [Tooltip("Multiplayer: a tool a player picks up during the experience goes to every player's wheel "
                 + "(also players who join later). Off: only to the wheel of the player who picked it up. "
                 + "A tool can override this with its Pickup Sharing.")]
        public bool pickedToolsShared = false;

        [Tooltip("Whether or not you want to show the hand item in the inventory, used to remove items instead of clicking on the same item again")]
        [HideInInspector] public bool displayEmptyHand;
        [HideInInspector] public Sprite emptyHandSprite;
        [HideInInspector] public string emptyHandName;

        #endregion

        #region Inventory

        [Header("Inventory")]
        [Tooltip("The equipment slots, each with its type: the hand (the tools) and the custom wear slots (Head, "
                 + "Hands...), shown from the start. A wearable whose Wear Slot is not declared here adds it. Empty: the "
                 + "wear slots are counted by Wear Slots Count instead")]
        public List<EquipmentSlotDefinition> equipmentSlots = new();

        [Tooltip("Number of wear slots in the wear inventory to start with, used when no Equipment Slots are declared. "
                 + "A wearable that does not fit adds a slot")]
        public int wearSlotsCount = 1;

        [Tooltip("Number of slots in the inventory to start with. An item that does not fit adds a slot")]
        public int inventorySlotsCount = 2;

        [Tooltip("Whether or not I want the inventory to show how many items the user has of a certain type")]
        public bool showItemCount = true;

        [Tooltip("Items each player has in the inventory from the start, routed as if picked up: a Wearable to the "
                 + "wear slots, a tool to the tool wheel, anything else to the inventory. Use prefabs (or objects that are "
                 + "not in the scene): an object listed here is not removed from the scene.")]
        public List<PickablePlaceholder> startingItems = new();

        [Tooltip("VR only: the input pressed in order to open and close the inventory panel")]
        public InputAction inventoryAction;

        [Tooltip("VR only: the distance of the inventory panel from the camera")]
        public float inventoryZOffset = 0.5f;

        [Tooltip("VR only: the offset of the inventory panel on the camera's y axis")]
        public float inventoryYOffset = -0.15f;

        [Tooltip("VR only: the time, in seconds, the inventory panel takes to open and close")]
        public float inventoryOpenSpeed = 0.25f;

        #endregion

        /// <summary>
        /// Raised when <see cref="InventoryMode"/> changes, with the previous and the new mode.
        /// </summary>
        public event Action<EInventoryMode, EInventoryMode> InventoryModeChanged;

        /// <summary>
        /// Raised by <see cref="SetSlotCounts"/> when a slot count grows, also when the inventory grows on its own (see
        /// <see cref="SetSlotCounts"/>).
        /// </summary>
        public event Action SlotCountsChanged;

        /// <summary>
        /// Raised by <see cref="AddEquipmentSlot"/> when a slot is added.
        /// </summary>
        public event Action EquipmentSlotsChanged;

        /// <summary>
        /// Declares one more equipment slot during the experience (for example a slot unlocked by a task). A slot
        /// already declared (same type and name) is not added twice. Slots are only added: one may hold items.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Inventory: Add Equipment Slot</c>.</remarks>
        public void AddEquipmentSlot(EEquipmentSlotType type, string slotName, Sprite slotIcon = null)
        {
            if (type == EEquipmentSlotType.Custom && string.IsNullOrEmpty(slotName))
            {
                return;
            }
            foreach (EquipmentSlotDefinition slot in equipmentSlots)
            {
                if (slot != null && slot.type == type && (type == EEquipmentSlotType.Hand || slot.name == slotName))
                {
                    return;
                }
            }
            equipmentSlots.Add(new EquipmentSlotDefinition { type = type, name = slotName, icon = slotIcon });
            EquipmentSlotsChanged?.Invoke();
        }

        /// <summary>
        /// Gives the inventory more slots during the experience (for example a slot unlocked by a task). Counts only
        /// grow: a value below the current one is ignored, since the slots may hold items. Takes effect at once when
        /// the inventory is shown, otherwise when it is first shown.
        /// <paramref name="wearSlots"/> is the wear inventory's number of generic cells, which counts only when no
        /// <see cref="equipmentSlots"/> are declared (as <see cref="wearSlotsCount"/>): with declared slots the
        /// equipment column shows one cell per declared slot, and a slot is added with <see cref="AddEquipmentSlot"/>.
        /// The inventory also grows on its own, and <see cref="inventorySlotsCount"/> follows it: an item that does not
        /// fit adds a slot, and on desktop, web and mobile the item slots round up to whole pages past the first (a
        /// page is the grid the inventory shows: web 3 or 4 columns x 6 rows, mobile 2 or 3 x 2, the equipment taking
        /// one column when it has slots), so a new page opens full of empty, usable slots.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Inventory: Set Slot Counts</c>.</remarks>
        public void SetSlotCounts(int wearSlots, int inventorySlots)
        {
            bool grows = wearSlots > wearSlotsCount || inventorySlots > inventorySlotsCount;
            wearSlotsCount = Mathf.Max(wearSlotsCount, wearSlots);
            inventorySlotsCount = Mathf.Max(inventorySlotsCount, inventorySlots);
            if (grows)
            {
                SlotCountsChanged?.Invoke();
            }
        }

        /// <summary>
        /// The active system; set it during the experience to switch (see <see cref="InventoryModeChanged"/>).
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Inventory: Set Mode</c>.</remarks>
        public EInventoryMode InventoryMode
        {
            get => inventoryMode;
            set
            {
                if (inventoryMode == value)
                {
                    return;
                }

                EInventoryMode previousMode = inventoryMode;
                inventoryMode = value;
                InventoryModeChanged?.Invoke(previousMode, inventoryMode);
            }
        }

#if UNITY_EDITOR
        // Called by the inspector when the mode is edited in Play mode, after the new value is already serialized
        internal void NotifyInventoryModeChangedFromInspector(EInventoryMode previousMode)
        {
            if (previousMode != inventoryMode)
            {
                InventoryModeChanged?.Invoke(previousMode, inventoryMode);
            }
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(InventoryPlaceholder))]
    public class InventoryPlaceholderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            InventoryPlaceholder inventoryPlaceholder = (InventoryPlaceholder)target;
            InventoryPlaceholder.EInventoryMode previousMode = inventoryPlaceholder.InventoryMode;

            serializedObject.Update();

            SerializedProperty property = serializedObject.GetIterator();
            for (bool enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
            {
                using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                {
                    EditorGUILayout.PropertyField(property, true);
                }

                // The empty hand fields belong to the tools: draw them right after the last tools field
                if (property.name == nameof(InventoryPlaceholder.action))
                {
                    DrawEmptyHandFields(inventoryPlaceholder);
                }
            }

            serializedObject.ApplyModifiedProperties();

            // In Play mode an edit of the selector switches the system, as a change from code does
            if (Application.isPlaying)
            {
                inventoryPlaceholder.NotifyInventoryModeChangedFromInspector(previousMode);
            }
        }

        private void DrawEmptyHandFields(InventoryPlaceholder inventoryPlaceholder)
        {
            if (inventoryPlaceholder.inventoryLayout != InventoryPlaceholder.EToolLayout.Radial)
            {
                return;
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(InventoryPlaceholder.displayEmptyHand)));

            if (inventoryPlaceholder.displayEmptyHand)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(InventoryPlaceholder.emptyHandSprite)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(InventoryPlaceholder.emptyHandName)));
            }
        }
    }
#endif
}
