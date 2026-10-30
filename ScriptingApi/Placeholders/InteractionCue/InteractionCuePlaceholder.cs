using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Shows how an object can be used: a rim when it is the next target, when the user hovers or selects it, and a
    /// green or red flash for a right or wrong action. Put it on the object (or the root of the object) to signal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cue only signals: it detects nothing and decides nothing. Its <see cref="State"/> is the first that applies
    /// in this order: <see cref="EInteractionCueState.Locked"/>, <see cref="EInteractionCueState.Right"/> or
    /// <see cref="EInteractionCueState.Wrong"/> (for <see cref="FeedbackSeconds"/>),
    /// <see cref="EInteractionCueState.Selected"/>, <see cref="EInteractionCueState.Hover"/>, and last the base state
    /// set by <see cref="SetBaseState"/> (<see cref="EInteractionCueState.Available"/> by default).
    /// </para>
    /// <para>
    /// The rim is drawn on copies of the object's renderers, created the first time the rim shows: the object's own
    /// materials are never changed, so scripts that change them by index keep working. A renderer that is part of a
    /// static batch gets no rim (a warning says so); a skinned renderer's rim follows its bones but not its blend
    /// shapes. Everything is driven by events: the cue costs no script time per frame, and the pulse is computed by
    /// the shader.
    /// </para>
    /// <para>
    /// <see cref="SetBaseState"/>, <see cref="SetLocked"/>, <see cref="SetHovered"/> and <see cref="SetSelected"/> act on
    /// this client only. Call them on the main thread.
    /// </para>
    /// </remarks>
    public class InteractionCuePlaceholder : SceneComponentPlaceholderNetwork
    {
        #region Settings

        [Header("Look")]
        [SerializeField, Tooltip("Materials, colour and arrow of the cue. The SDK's asset reproduces the Showcase highlight.")]
        private InteractionCueSettings settings;

        [SerializeField, Tooltip("How the cue shows that the object is the next target.")]
        private ECueNextTargetStyle nextTargetStyle = ECueNextTargetStyle.Pulse;

        [SerializeField, Tooltip("How the cue shows that the local user hovers the object.")]
        private ECueHoverStyle hoverStyle = ECueHoverStyle.Rim;

        [SerializeField, Tooltip("Key of the hover label's text in the environment's Localization File. Empty: the text " +
            "below as written.")]
        private string labelKey = "";

        [SerializeField, Tooltip("The hover label's text, shown when Hover Style is Rim And Label (needs the cue's feedback " +
            "objects), and when the key has no translation.")]
        private string labelText = "";

        [SerializeField, Tooltip("What a right or wrong action shows.")]
        private ECueFeedback feedback = ECueFeedback.Rim | ECueFeedback.Mark | ECueFeedback.Flash | ECueFeedback.Sound;

        [SerializeField, Min(0.1f), Tooltip("How long a right or wrong action is shown, in seconds.")]
        private float feedbackSeconds = 1.5f;

        [SerializeField, Tooltip("The renderers that get the rim. Empty: every mesh and skinned mesh renderer under this " +
            "object, except those of other cues nested in it.")]
        private List<Renderer> renderers = new();

        [SerializeField, Tooltip("Use the colour below for this object's next-target, hover and selected rims instead of " +
            "the settings' colour. Right and wrong keep their colours.")]
        private bool overrideColor = false;

        [SerializeField, ColorUsage(false, true), Tooltip("The colour of this object's rim when Override Color is on.")]
        private Color color = new(0f, 2.65f, 4f, 1f);

        [Header("Events")]
        [SerializeField] private UnityEvent onNextTarget = new();
        [SerializeField] private UnityEvent onHoverShown = new();
        [SerializeField] private UnityEvent onRight = new();
        [SerializeField] private UnityEvent onWrong = new();
        [SerializeField] private UnityEvent onCompleted = new();
        [SerializeField] private UnityEvent onLocked = new();

        public InteractionCueSettings Settings => settings;
        public ECueNextTargetStyle NextTargetStyle { get => nextTargetStyle; set { nextTargetStyle = value; Refresh(); } }
        public ECueHoverStyle HoverStyle { get => hoverStyle; set { hoverStyle = value; Refresh(); } }
        public ECueFeedback Feedback { get => feedback; set { feedback = value; Refresh(); } }
        public string LabelKey { get => labelKey; set { labelKey = value ?? ""; Refresh(); } }
        public string LabelText { get => labelText; set { labelText = value ?? ""; Refresh(); } }
        public float FeedbackSeconds { get => feedbackSeconds; set => feedbackSeconds = Mathf.Max(0.1f, value); }

        /// <summary>The renderers that get the rim; empty means every renderer under this object. Read once, the first
        /// time the rim shows.</summary>
        public IReadOnlyList<Renderer> Renderers => renderers;

        public bool OverrideColor { get => overrideColor; set { overrideColor = value; Refresh(); } }
        public Color Color { get => color; set { color = value; Refresh(); } }

        /// <summary>Raised when the cue becomes the next target.</summary>
        public UnityEvent OnNextTarget => onNextTarget;
        /// <summary>Raised when the cue starts showing that the local user hovers it.</summary>
        public UnityEvent OnHoverShown => onHoverShown;
        /// <summary>Raised for every right action shown.</summary>
        public UnityEvent OnRight => onRight;
        /// <summary>Raised for every wrong action shown.</summary>
        public UnityEvent OnWrong => onWrong;
        /// <summary>Raised when the cue becomes completed.</summary>
        public UnityEvent OnCompleted => onCompleted;
        /// <summary>Raised when the cue becomes locked.</summary>
        public UnityEvent OnLocked => onLocked;

        #endregion

        #region State

        private EInteractionCueState baseState = EInteractionCueState.Available;
        private EInteractionCueState transient = EInteractionCueState.Available; // Right, Wrong, or Available for none
        private EInteractionCueState state = EInteractionCueState.Available;
        private bool hoveredByScript;
        private bool selectedByScript;
        private bool lockedByScript;
        private bool hoveredByInteractable;
        private bool selectedByInteractable;
        private bool blockedByInteractable;
        private Coroutine transientEnd;

        /// <summary>The state the cue shows now.</summary>
        public EInteractionCueState State => state;

        /// <summary>The base state: <see cref="EInteractionCueState.Available"/>,
        /// <see cref="EInteractionCueState.NextTarget"/> or <see cref="EInteractionCueState.Completed"/>.</summary>
        public EInteractionCueState BaseState => baseState;

        /// <summary>Whether the local user hovers the object (with any controller), or a script set it hovered.</summary>
        public bool IsHovered => hoveredByScript || hoveredByInteractable;

        /// <summary>Whether the object is selected by the local user, or a script set it selected.</summary>
        public bool IsSelected => selectedByScript || selectedByInteractable;

        /// <summary>Whether the cue is locked: by <see cref="SetLocked"/>, or because the object's Interactable is
        /// blocked (<see cref="InteractablePlaceholder.IsBlocked"/>).</summary>
        public bool IsLocked => lockedByScript || blockedByInteractable;

        /// <summary>Raised when <see cref="State"/> changes, with the previous and the new state.</summary>
        public event InteractionCueChangedHandler CueChanged;

        #endregion

        #region API

        /// <summary>
        /// Sets the base state: <see cref="EInteractionCueState.Available"/>, <see cref="EInteractionCueState.NextTarget"/>
        /// or <see cref="EInteractionCueState.Completed"/>. Any other value is refused with an exception. This client only.
        /// </summary>
        public void SetBaseState(EInteractionCueState newBaseState)
        {
            if (newBaseState != EInteractionCueState.Available && newBaseState != EInteractionCueState.NextTarget
                && newBaseState != EInteractionCueState.Completed)
            {
                throw new ArgumentException("[InteractionCue] " + name + ": the base state is Available, NextTarget or " +
                    "Completed, not " + newBaseState, nameof(newBaseState));
            }
            baseState = newBaseState;
            Refresh();
        }

        /// <summary>
        /// Locks or unlocks the cue: a locked cue shows no rim and ignores hover. This client only. A cue is also locked
        /// while its object's Interactable is blocked; unlocking here does not unblock it.
        /// </summary>
        public void SetLocked(bool locked)
        {
            lockedByScript = locked;
            Refresh();
        }

        /// <summary>Sets the object hovered, for objects that a script drives without an Interactable. This client only.</summary>
        public void SetHovered(bool hovered)
        {
            hoveredByScript = hovered;
            Refresh();
        }

        /// <summary>Sets the object selected, for objects that a script drives without an Interactable. This client only.</summary>
        public void SetSelected(bool selected)
        {
            selectedByScript = selected;
            Refresh();
        }

        /// <summary>Shows a right action for <see cref="FeedbackSeconds"/>.</summary>
        public void ShowRight() => ShowResult(true);

        /// <summary>Shows a wrong action for <see cref="FeedbackSeconds"/>.</summary>
        public void ShowWrong() => ShowResult(false);

        /// <summary>Shows a right or wrong action for <see cref="FeedbackSeconds"/>; a new one replaces the one shown.</summary>
        public void ShowResult(bool right)
        {
            ShowResultLocally(right);
        }

        #endregion

        #region Runtime hooks (internal: the SDK runtime draws the cue)

        /// <summary>Raised in <c>Awake</c> of every cue; the SDK runtime adds the component that draws it.</summary>
        internal static event Action<InteractionCuePlaceholder> Created;

        /// <summary>Raised when what the cue draws may have changed: its state, or a setting of its look.</summary>
        internal event Action VisualChanged;

        /// <summary>Raised for every right (true) or wrong (false) action shown, after <see cref="VisualChanged"/>.</summary>
        internal event Action<bool> ResultShown;

        /// <summary>
        /// The state the rim draws: <see cref="State"/>, except that a right or wrong action without
        /// <see cref="ECueFeedback.Rim"/> draws the state below it, and hover with <see cref="ECueHoverStyle.None"/> is
        /// already left out of <see cref="State"/>.
        /// </summary>
        internal EInteractionCueState RimState =>
            (state == EInteractionCueState.Right || state == EInteractionCueState.Wrong) && (feedback & ECueFeedback.Rim) == 0
                ? Resolve(false)
                : state;

        /// <summary>Whether the label shows: hovered or selected, Rim And Label, and a text to show.</summary>
        internal bool LabelVisible => hoverStyle == ECueHoverStyle.RimAndLabel
            && (state == EInteractionCueState.Hover || state == EInteractionCueState.Selected)
            && (labelKey.Length > 0 || labelText.Length > 0);

        // The object's Interactable, as the SDK runtime sees it: hovered by at least one local controller, selected,
        // blocked.
        internal void SetInteractableHovered(bool hovered)
        {
            hoveredByInteractable = hovered;
            Refresh();
        }

        internal void SetInteractableSelected(bool selected)
        {
            selectedByInteractable = selected;
            Refresh();
        }

        internal void SetInteractableBlocked(bool blocked)
        {
            blockedByInteractable = blocked;
            Refresh();
        }

        internal void ShowResultLocally(bool right)
        {
            transient = right ? EInteractionCueState.Right : EInteractionCueState.Wrong;
            if (transientEnd != null)
            {
                StopCoroutine(transientEnd);
                transientEnd = null;
            }
            // A cue on an inactive object cannot run the timer, and shows nothing anyway: the action is raised as an
            // event only.
            bool timed = isActiveAndEnabled;
            if (timed)
            {
                transientEnd = StartCoroutine(EndTransient(feedbackSeconds));
            }
            else
            {
                transient = EInteractionCueState.Available;
            }
            Refresh();
            ResultShown?.Invoke(right);
            (right ? onRight : onWrong).Invoke();
        }

        private IEnumerator EndTransient(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            transientEnd = null;
            transient = EInteractionCueState.Available;
            Refresh();
        }

        #endregion

        private void Reset()
        {
            IsNetworked = true;
#if UNITY_EDITOR
            settings = UnityEditor.AssetDatabase.LoadAssetAtPath<InteractionCueSettings>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(DefaultSettingsGuid));
            if (settings != null)
            {
                color = settings.Color;
            }
#endif
        }

        /// <summary>The GUID of the SDK's <c>Prefabs/InteractionCue/InteractionCueSettings.asset</c>.</summary>
        internal const string DefaultSettingsGuid = "8465b3026219456db48ed91c01eb59d8";

        private void Awake()
        {
            Created?.Invoke(this);
        }

        private void OnDisable()
        {
            // Coroutines stop with the object: a right or wrong action being shown ends with it.
            if (transientEnd != null)
            {
                transientEnd = null;
                transient = EInteractionCueState.Available;
                Refresh();
            }
        }

        private EInteractionCueState Resolve(bool withTransient)
        {
            if (IsLocked)
            {
                return EInteractionCueState.Locked;
            }
            if (withTransient && transient != EInteractionCueState.Available)
            {
                return transient;
            }
            if (IsSelected)
            {
                return EInteractionCueState.Selected;
            }
            if (IsHovered && hoverStyle != ECueHoverStyle.None)
            {
                return EInteractionCueState.Hover;
            }
            return baseState;
        }

        private void Refresh()
        {
            EInteractionCueState previous = state;
            state = Resolve(true);
            VisualChanged?.Invoke();
            if (previous == state)
            {
                return;
            }
            RaiseCueChanged(previous, state);
            switch (state)
            {
                case EInteractionCueState.NextTarget: onNextTarget.Invoke(); break;
                case EInteractionCueState.Hover: onHoverShown.Invoke(); break;
                case EInteractionCueState.Completed: onCompleted.Invoke(); break;
                case EInteractionCueState.Locked: onLocked.Invoke(); break;
            }
        }

        // Every handler runs, then a failure surfaces (platform errors fail loudly), as for the Interactable's events.
        private void RaiseCueChanged(EInteractionCueState previous, EInteractionCueState current)
        {
            InteractionCueChangedHandler handlers = CueChanged;
            if (handlers == null)
            {
                return;
            }
            List<Exception> failures = null;
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((InteractionCueChangedHandler)handler)(this, previous, current);
                }
                catch (Exception e)
                {
                    (failures ??= new List<Exception>()).Add(e);
                }
            }
            if (failures == null)
            {
                return;
            }
            if (failures.Count == 1)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            }
            throw new AggregateException("[InteractionCue] " + name + ": " + failures.Count + " CueChanged handlers failed",
                failures);
        }
    }
}
