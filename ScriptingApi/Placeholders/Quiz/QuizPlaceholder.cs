using System;
using System.Collections.Generic;

using Unity.VisualScripting;

using UnityEngine;

using System.Linq;

using UnityEngine.Events;




#if UNITY_EDITOR
using UnityEditor;
#endif


using SPACS.Utilities;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    public enum EQuizLayout
    {
        Horizontal = 0,
        Vertical = 1,
        Grid = 2
    }
    public enum EQuizSizeRatio
    {
        Free = 0,
        Square_1_1 = 1,
        Landscape_4_3 = 2,
        Landscape_16_9 = 3,
        Landscape_16_10 = 4,
        Portrait_3_4 = 20,
        Portrait_9_16 = 21,
        Portrait_10_16 = 22
    }
    public enum EQuizElementLayout
    {
        Line = 0,
        Box = 1
    }

    [Serializable]
    public class QuizAnswer
    {
        [SerializeField, TextArea]
        private string titleLabel = string.Empty;

        [SerializeField, TextArea]
        private string hiddenTitleLabel = string.Empty;

        [SerializeField]
        private Sprite image = null;

        [SerializeField]
        private bool correctAnswer = false;

        [SerializeField]
        private float scoreIfGood = 1f;

        [SerializeField]
        private float scoreIfBad = 0f;

        [SerializeField, TextArea]
        private string feedbackLabel = string.Empty;

        public string TitleLabel => titleLabel.Trim();  // Removes white spaces at start and end of the string.
        public string HiddenTitleLabel => hiddenTitleLabel.Trim();  // Removes white spaces at start and end of the string.
        public Sprite Image => image;
        public bool CorrectAnswer => correctAnswer;
        public float ScoreIfGood => scoreIfGood;
        public float ScoreIfBad => scoreIfBad;
        public string FeedbackLabel => feedbackLabel.Trim();  // Removes white spaces at start and end of the string.

        public bool IsSelected { get; private set; }
        public bool IsCorrectSelection => IsSelected == correctAnswer;
        public float CurrentScore => IsCorrectSelection ? ScoreIfGood : ScoreIfBad;

        // Localizations
        public string QuizInstanceAnswerTitleValue { get; set; } = string.Empty;
        public string QuizInstanceAnswerHiddenTitleValue { get; set; } = string.Empty;
        public string QuizInstanceAnswerFeedbackValue { get; set; } = string.Empty;

        public void Select()
        {
            IsSelected = true;
        }
        public void Deselect()
        {
            IsSelected = false;
        }
    }

    public class QuizPlaceholder : SceneComponentPlaceholderNetwork
    {
        #region Constants

        private const string QUIZ_SIZE_FORMAT = "W: <b>{0}</b> - H: <b>{1}</b>";
        private const string QUIZ_LAYOUT_FORMAT = "Layout: <b>{0}</b> with <b>{1}</b> answers";
        private const string QUIZ_MAXANSWERS_FORMAT = "Max Answer items: <b>{0}{1}</b> out of <b>{2}</b>";
        private const string QUIZ_SELECTIONS_FORMAT = "Selections: <b>{0}</b> to <b>{1}</b>";

        #endregion

        #region Inspector Info

        [HelpBox("Do not change the value of \"IsNetworked\" field", HelpBoxMessageType.Warning)]

        #region Quiz Placeholder References

        [Header("Quiz Placeholder references (internal). \n=> Do not change unless making a custom prefab.")]

#pragma warning disable 0414 // suppress value not used warning
        [SerializeField]
        private bool showReferences = false;
#pragma warning restore 0414 // restore value not used warning

        [DrawIf(nameof(showReferences), true)]
        [SerializeField, Tooltip("The transform that contains the body of the media player (the panel, optional graphics, and so on). " +
       "It's recommended to put custom graphics, like a background, a logo, etc. as children of this transform, " +
       "but keep in mind that, when a media is sent to this panel, the GameObject associated with this transform will be deactivated.")]
        private Transform contentTransform;

        [DrawIf(nameof(showReferences), true)]
        [SerializeField, Tooltip("The transform that represents the panel where the media is being reproduced. " +
            "Do not change its size, it will be automatically updated by using the panel settings.")]
        private Transform panelTransform;

        [DrawIf(nameof(showReferences), true)]
        [SerializeField, Tooltip("The transform used by the camera in case of a pan towards the panel. " +
            "Do not change its local position, it will be automatically updated by using the panel settings.")]
        private Transform cameraPanTransform;

        [Space]

        [DrawIf(nameof(showReferences), true)]
        [SerializeField]
        private TextMesh quizTitleTextMesh;

        [DrawIf(nameof(showReferences), true)]
        [SerializeField]
        private TextMesh quizSizeTextMesh;

        [DrawIf(nameof(showReferences), true)]
        [SerializeField]
        private TextMesh quizLayoutTextMesh;

        [DrawIf(nameof(showReferences), true)]
        [SerializeField]
        private TextMesh quizMaxAnswersTextMesh;

        [DrawIf(nameof(showReferences), true)]
        [SerializeField]
        private TextMesh quizMaxSelectionsTextMesh;

        #endregion

        #region Quiz Panel Size

        [HelpBox("To resize the panel, don't modify the scale of the transforms, but use the parameters \"Panel Width\" and \"Panel Height\" " +
            "and they will adjust automatically its dimensions. The same applies to the distance of the camera pan transform.", HelpBoxMessageType.Info)]

        [Header("Panel settings")]

        [SerializeField, /*Range(0.5f, 10),*/ Tooltip("The width of the panel.")]
        [OnChangedCall(nameof(OnWidthChanged))]
        private float panelWidth = 1.5f;

        [SerializeField, /*Range(0.5f, 10),*/ Tooltip("The height of the panel.")]
        [OnChangedCall(nameof(OnHeightChanged))]
        private float panelHeight = 1f;

        [SerializeField]
        [OnChangedCall(nameof(OnPanelLockRatioChanged))]
        private bool panelLockRatio = false;

        [SerializeField]
        [DrawIf(nameof(panelLockRatio), true)]
        [OnChangedCall(nameof(OnPanelSizeRatioChanged))]
        private EQuizSizeRatio panelSizeRatio = EQuizSizeRatio.Free;

        [SerializeField/*, Range(0.5f, 10)*/, Tooltip("The distance of the transform to which the camera pans (WebGL only).")]
        [OnChangedCall(nameof(OnPanTransformChanged))]
        private float cameraPanDistance = 1f;

        #endregion

        #region Quiz Text Size

        [FoldableGroup("Text size settings")]
        [SerializeField, Tooltip("Overrides font size for Header field in the quiz panel")]
        private bool overrideHeaderTextSize = false;

        [FoldableGroup("Text size settings")]
        [DrawIf(nameof(overrideHeaderTextSize), true)]
        [SerializeField, Indent, Tooltip("Target size for header field")]
        private float headerSizeValue = 0.25f;

        [FoldableGroup("Text size settings")]
        [DrawIf(nameof(overrideHeaderTextSize), true)]
        [SerializeField, Indent, Tooltip("Autosize setting for header field. If it's on, " +
            "the value of Header Size Value will be used as max size")]
        private bool headerAutoSize = false;


        [FoldableGroup("Text size settings")]
        [SerializeField, Tooltip("Overrides font size for Title field in the quiz panel")]
        private bool overrideTitleTextSize = false;

        [FoldableGroup("Text size settings")]
        [DrawIf(nameof(overrideTitleTextSize), true)]
        [SerializeField, Indent, Tooltip("Target size for title field")]
        private float titleSizeValue = 0.35f;

        [FoldableGroup("Text size settings")]
        [DrawIf(nameof(overrideTitleTextSize), true)]
        [SerializeField, Indent, Tooltip("Autosize setting for title field. If it's on, " +
            "the value of Title Size Value will be used as max size")]
        private bool titleAutoSize = false;


        [FoldableGroup("Text size settings")]
        [SerializeField, Tooltip("Overrides font size for Description field in the quiz panel")]
        private bool overrideDescriptionTextSize = false;

        [FoldableGroup("Text size settings")]
        [DrawIf(nameof(overrideDescriptionTextSize), true)]
        [SerializeField, Indent, Tooltip("Target size for description field")]
        private float descriptionSizeValue = 0.25f;

        [FoldableGroup("Text size settings")]
        [DrawIf(nameof(overrideDescriptionTextSize), true)]
        [SerializeField, Indent, Tooltip("Autosize setting for description field. If it's on, " +
            "the value of Description Size Value will be used as max size")]
        private bool descriptionAutoSize = false;


        [FoldableGroup("Text size settings")]
        [SerializeField, Tooltip("Overrides font size for Question field in the quiz panel")]
        private bool overrideQuestionTextSize = false;

        [FoldableGroup("Text size settings")]
        [DrawIf(nameof(overrideQuestionTextSize), true)]
        [SerializeField, Indent, Tooltip("Target size for question field")]
        private float questionSizeValue = 0.25f;

        [FoldableGroup("Text size settings")]
        [DrawIf(nameof(overrideQuestionTextSize), true)]
        [SerializeField, Indent, Tooltip("Autosize setting for question field. If it's on, " +
            "the value of Question Size Value will be used as max size")]
        private bool questionAutoSize = false;


        [FoldableGroup("Text size settings")]
        [SerializeField, Tooltip("Overrides font size for results feedback field in the quiz panel")]
        private bool overrideFeedbacksTextSize = false;

        [FoldableGroup("Text size settings")]
        [DrawIf(nameof(overrideFeedbacksTextSize), true)]
        [SerializeField, Indent, Tooltip("Target size for feedbacks field")]
        private float feedbacksSizeValue = 0.25f;

        [FoldableGroup("Text size settings")]
        [DrawIf(nameof(overrideFeedbacksTextSize), true)]
        [SerializeField, Indent, Tooltip("Autosize setting for feedbacks field. If it's on, " +
            "the value of Feedbacks Size Value will be used as max size")]
        private bool feedbacksAutoSize = false;


        [FoldableGroup("Text size settings")]
        [SerializeField, Tooltip("Overrides font size for answers' title field")]
        private bool overrideAnswerTitleTextSize = false;

        [FoldableGroup("Text size settings")]
        [DrawIf(nameof(overrideAnswerTitleTextSize), true)]
        [SerializeField, Indent, Tooltip("Target size for the title field of quiz answers")]
        private float answerTitleSizeValue = 0.25f;

        [FoldableGroup("Text size settings")]
        [DrawIf(nameof(overrideAnswerTitleTextSize), true)]
        [SerializeField, Indent, Tooltip("Autosize setting for answers' titles. If it's on, " +
            "the value of Answer Title Size Value will be used as max size")]
        private bool answerTitleAutoSize = false;

        #endregion

        [Header("Quiz details")]

        [SerializeField, TextArea]
        private string headerLabel = string.Empty;

        [SerializeField, TextArea]
        private string titleLabel = "Quiz";

        [SerializeField, TextArea]
        private string descriptionLabel = string.Empty;

        [SerializeField, TextArea]
        private string questionLabel = string.Empty;

        [Space]
        [SerializeField]
        public bool showFeedbackOnCorrectAnswer = false;

        [SerializeField]
        [OnChangedCall(nameof(OnSelectableAnswersChanged))]
        private bool allowMultipleSelection = true;

        [DrawIf(nameof(allowMultipleSelection), true)]
        [Min(0)]
        [SerializeField]
        [OnChangedCall(nameof(OnSelectableAnswersChanged))]
        private int minSelectableAnswers = 0;

        [DrawIf(nameof(allowMultipleSelection), true)]
        [Min(0)]
        [SerializeField]
        [OnChangedCall(nameof(OnSelectableAnswersChanged))]
        private int maxSelectableAnswers = 100;

        [Space]

        [SerializeField]
        [OnChangedCall(nameof(OnAnswersChanged))]
        private bool shuffleAnswers = false;

        [DrawIf(nameof(shuffleAnswers), true)]
        [SerializeField]
        [OnChangedCall(nameof(OnAnswersChanged))]
        private bool pickSubset = false;

        [DrawIf(nameof(shuffleAnswers), true)]
        [DrawIf(nameof(pickSubset), true)]
        [Min(1)]
        [SerializeField]
        [OnChangedCall(nameof(OnAnswersChanged))]
        private int answersSubsetQuantity = 100;

        [DrawIf(nameof(shuffleAnswers), true)]
        [DrawIf(nameof(pickSubset), true)]
        [Min(0)]
        [SerializeField]
        [OnChangedCall(nameof(OnAnswersChanged))]
        private int minCorrectAnswersQuantity = 0;

        [Space]

        [SerializeField]
        [OnChangedCall(nameof(OnLayoutChanged))]
        private EQuizLayout quizLayout = EQuizLayout.Horizontal;

        [DrawIf(nameof(quizLayout), EQuizLayout.Grid)]
        [SerializeField]
        [OnChangedCall(nameof(OnLayoutChanged))]
        private int quizGridColumns = 3;

        [SerializeField]
        [OnChangedCall(nameof(OnLayoutChanged))]
        private EQuizElementLayout quizElementLayout = EQuizElementLayout.Line;

        [SerializeField]
        private ScriptMachine quizEventsScriptMachine;

        [Space]

        [SerializeField]
        private List<QuizAnswer> quizAnswers;

        #endregion

        #region Private variables (Editor)

        private float lastWidth = -1f;
        private float lastHeight = -1f;
        private float lastAspectRatio = -1f;
        private EQuizSizeRatio lastSizeRatio = EQuizSizeRatio.Free;

        #endregion

        #region Private variables (Runtime)

        private List<QuizAnswer> quizInstanceAnswers = new List<QuizAnswer>();

        #endregion

        #region Properties based on Inspecor Info

        public Transform ContentTransform => contentTransform;
        public Transform PanelTransform => panelTransform;
        public Transform CameraPanTransform => cameraPanTransform;

        // Text size override properties
        public bool OverrideHeaderTextSize => overrideHeaderTextSize;
        public float HeaderSizeValue => headerSizeValue;
        public bool HeaderAutoSize => headerAutoSize;
        public bool OverrideTitleTextSize => overrideTitleTextSize;
        public float TitleSizeValue => titleSizeValue;
        public bool TitleAutoSize => titleAutoSize;
        public bool OverrideDescriptionTextSize => overrideDescriptionTextSize;
        public float DescriptionSizeValue => descriptionSizeValue;
        public bool DescriptionAutoSize => descriptionAutoSize;
        public bool OverrideQuestionTextSize => overrideQuestionTextSize;
        public float QuestionSizeValue => questionSizeValue;
        public bool QuestionAutoSize => questionAutoSize;
        public bool OverrideFeedbacksTextSize => overrideFeedbacksTextSize;
        public float FeedbacksSizeValue => feedbacksSizeValue;
        public bool FeedbacksAutoSize => feedbacksAutoSize;
        public bool OverrideAnswerTitleTextSize => overrideAnswerTitleTextSize;
        public float AnswerTitleSizeValue => answerTitleSizeValue;
        public bool AnswerTitleAutoSize => answerTitleAutoSize;

        public string HeaderLabel => headerLabel.Trim(); // Removes white spaces at start and end of the string.
        public string TitleLabel => titleLabel.Trim(); // Removes white spaces at start and end of the string.
        public string DescriptionLabel => descriptionLabel.Trim(); // Removes white spaces at start and end of the string.
        public string QuestionLabel => questionLabel.Trim(); // Removes white spaces at start and end of the string.
        public bool AllowMultipleSelection => allowMultipleSelection;
        public bool ShuffleAnswers => shuffleAnswers;
        public bool PickSubset => ShuffleAnswers ? pickSubset : false;
        public EQuizLayout QuizLayout => quizLayout;
        public int QuizGridColumns => quizGridColumns;
        public EQuizElementLayout QuizElementLayout => quizElementLayout;
        public ScriptMachine QuizEventsScriptMachine => quizEventsScriptMachine;
        public List<QuizAnswer> QuizAnswers => quizAnswers;

        // MinAnswers: if multiple answers are not allowed, automatically reduce to 1.
        // In every case, MinAnswers can't be negative.
        public int MinSelectableAnswers => AllowMultipleSelection ? Mathf.Clamp(minSelectableAnswers, 0, QuizAnswers.Count) : 1;

        // MaxAnswers: if multiple answers are not allowed, automatically reduce to 1.
        // MaxAnswers is clamped at least to MinAnswers.
        // In every case, MaxAnswers can't be negative.
        public int MaxSelectableAnswers => AllowMultipleSelection ? Mathf.Clamp(maxSelectableAnswers, MinSelectableAnswers, QuizAnswers.Count) : 1;

        // AnswersSubsetQuantity
        // In every case, AnswersSubsetQuantity can't be negative.
        public int AnswersSubsetQuantity => PickSubset ? Mathf.Clamp(answersSubsetQuantity, 0, QuizAnswers.Count) : QuizAnswers.Count;

        // MinCorrectAnswersQuantity
        // In every case, MinCorrectAnswersQuantity can't be negative.
        public int MinCorrectAnswersQuantity => PickSubset ? Mathf.Clamp(minCorrectAnswersQuantity, 0, QuizAnswers.Count) : 0;

        #endregion

        #region Properties based on Runtime

        public IReadOnlyList<QuizAnswer> QuizInstanceAnswers => quizInstanceAnswers;

        public int QuizInstanceAnswersCount => QuizInstanceAnswers.Count;

        public float QuizInstanceAllGoodScore { get; private set; }
        public float QuizInstanceAllBadScore { get; private set; }

        public float QuizInstanceScore => QuizInstanceAnswers.Sum(x => x.CurrentScore);

        public int QuizInstanceCorrectAnswersCount => QuizInstanceAnswers.Count(x => x.IsCorrectSelection);

        // Localizations
        public string QuizInstanceHeaderValue { get; set; } = string.Empty;
        public string QuizInstanceTitleValue { get; set; } = string.Empty;
        public string QuizInstanceDescriptionValue { get; set; } = string.Empty;
        public string QuizInstanceQuestionValue { get; set; } = string.Empty;

        #endregion

        #region Public Events

        public UnityEvent onFinish = default;
        public UnityEvent<bool, bool, bool> onGoToResults = default;
        public UnityEvent onEdit = default;
        public UnityEvent onReset = default;
        public UnityEvent onContinue = default;

        #endregion

        #region Unity Events

        private void Awake()
        {
            // Placeholder setup, not related to runtime features.
            if (panelLockRatio)
            {
                lastWidth = panelWidth;
                lastHeight = panelHeight;
            }
        }

        #endregion

        #region Public Methods (Runtime)

        public void ClearInstanceAnswers()
        {
            quizInstanceAnswers.Clear();
            QuizInstanceAllGoodScore = 0f;
            QuizInstanceAllBadScore = 0f;
        }

        public void AddInstanceAnswer(QuizAnswer answer)
        {
            quizInstanceAnswers.Add(answer);
        }

        public void SetupInstanceConstants()
        {
            foreach (var answer in QuizInstanceAnswers)
            {
                QuizInstanceAllGoodScore += answer.ScoreIfGood;
                QuizInstanceAllBadScore += answer.ScoreIfBad;
            }
        }

        public void VSNode_Finish()
        {
            onFinish?.Invoke();
        }
        public void VSNode_GoToResults(bool showEditButton, bool showResetButton, bool showContinueButton)
        {
            onGoToResults?.Invoke(showEditButton, showResetButton, showContinueButton);
        }
        public void VSNode_Edit()
        {
            onEdit?.Invoke();
        }
        public void VSNode_Reset()
        {
            onReset?.Invoke();
        }
        public void VSNode_Continue()
        {
            onContinue?.Invoke();
        }

        #endregion

        #region Placeholder Setup Methods! Not Runtime

        private void UpdateAspectRatio()
        {
            // Recalculate the ratio only if not locked.
            if (!panelLockRatio)
            {
                if (lastHeight != 0f)
                {
                    lastAspectRatio = lastWidth / lastHeight;
                }
                else
                {
                    lastAspectRatio = 0f;
                }
            }
            else if (lastSizeRatio != panelSizeRatio)
            {
                lastSizeRatio = panelSizeRatio;

                switch (lastSizeRatio)
                {
                    case EQuizSizeRatio.Square_1_1:
                        lastAspectRatio = 1f;
                        break;

                    case EQuizSizeRatio.Landscape_4_3:
                        lastAspectRatio = 4f / 3f;
                        break;
                    case EQuizSizeRatio.Landscape_16_9:
                        lastAspectRatio = 16f / 9f;
                        break;
                    case EQuizSizeRatio.Landscape_16_10:
                        lastAspectRatio = 16f / 10f;
                        break;

                    case EQuizSizeRatio.Portrait_3_4:
                        lastAspectRatio = 3f / 4f;
                        break;
                    case EQuizSizeRatio.Portrait_9_16:
                        lastAspectRatio = 9f / 16f;
                        break;
                    case EQuizSizeRatio.Portrait_10_16:
                        lastAspectRatio = 10f / 16f;
                        break;

                    default:
                        break;
                }
            }
        }
        public void OnWidthChanged()
        {
            if (lastWidth != panelWidth)
            {
                lastWidth = panelWidth;

                panelTransform.localScale = new Vector3(panelWidth, panelTransform.localScale.y, panelTransform.localScale.z);

                if (panelLockRatio)
                {
                    UpdateAspectRatio();
                    panelHeight = panelWidth / lastAspectRatio;
                    OnHeightChanged();
                }
                else
                {
                    UpdateSizeText();
                    UpdateAspectRatio();
                }
            }
        }
        public void OnHeightChanged()
        {
            if (lastHeight != panelHeight)
            {
                lastHeight = panelHeight;

                panelTransform.localScale = new Vector3(panelTransform.localScale.x, panelHeight, panelTransform.localScale.z);

                if (panelLockRatio)
                {
                    UpdateAspectRatio();
                    panelWidth = panelHeight * lastAspectRatio;
                    OnWidthChanged();
                }
                else
                {
                    UpdateSizeText();
                    UpdateAspectRatio();
                }
            }
        }
        public void OnPanelLockRatioChanged()
        {
            if (!panelLockRatio)
            {
                panelSizeRatio = EQuizSizeRatio.Free;
                lastSizeRatio = panelSizeRatio;
            }
        }
        public void OnPanelSizeRatioChanged()
        {
            if (panelLockRatio && panelSizeRatio != EQuizSizeRatio.Free)
            {
                UpdateAspectRatio();

                // Keep width to change height.
                panelHeight = panelWidth / lastAspectRatio;
                OnHeightChanged();
            }
        }
        public void OnPanTransformChanged()
        {
            cameraPanTransform.localPosition = new Vector3(cameraPanTransform.localPosition.x, cameraPanTransform.localPosition.y, -cameraPanDistance);
        }

        public void OnLayoutChanged() => UpdateLayoutText();
        public void OnAnswersChanged() => UpdateAnswersText();
        public void OnSelectableAnswersChanged() => UpdateMaxSelectionsText();

        private void UpdateSizeText()
        {
            if (quizSizeTextMesh != null)
            {
                var newVal = string.Format(QUIZ_SIZE_FORMAT, panelWidth, panelHeight);
                if (quizSizeTextMesh.text != newVal)
                {
                    quizSizeTextMesh.text = string.Format(QUIZ_SIZE_FORMAT, panelWidth, panelHeight);
#if UNITY_EDITOR
                    EditorUtility.SetDirty(quizSizeTextMesh);
#endif
                }
            }
        }
        private void UpdateTitleText()
        {
            if (quizTitleTextMesh != null && quizTitleTextMesh.text != TitleLabel)
            {
                quizTitleTextMesh.text = TitleLabel;
#if UNITY_EDITOR
                EditorUtility.SetDirty(quizTitleTextMesh);
#endif
            }
        }
        private void UpdateLayoutText()
        {
            if (quizLayoutTextMesh != null)
            {
                var newVal = string.Format(QUIZ_LAYOUT_FORMAT, QuizLayout, QuizElementLayout);
                if (QuizLayout == EQuizLayout.Grid)
                {
                    newVal += " (" + QuizGridColumns + ")";
                }
                if (quizLayoutTextMesh.text != newVal)
                {
                    quizLayoutTextMesh.text = newVal;
#if UNITY_EDITOR
                    EditorUtility.SetDirty(quizLayoutTextMesh);
#endif
                }
            }
        }
        private void UpdateAnswersText()
        {
            if (quizMaxAnswersTextMesh != null)
            {
                // ToDo: add also "MinCorrectAnswersQuantity" value?
                var newVal = string.Format(QUIZ_MAXANSWERS_FORMAT, AnswersSubsetQuantity, (ShuffleAnswers ? " shuffled" : string.Empty), QuizAnswers.Count);
                if (quizMaxAnswersTextMesh.text != newVal)
                {
                    quizMaxAnswersTextMesh.text = newVal;
#if UNITY_EDITOR
                    EditorUtility.SetDirty(quizMaxAnswersTextMesh);
#endif
                }
            }
        }
        private void UpdateMaxSelectionsText()
        {
            if (quizMaxSelectionsTextMesh != null)
            {
                var newVal = string.Format(QUIZ_SELECTIONS_FORMAT, MinSelectableAnswers, MaxSelectableAnswers);
                if (quizMaxSelectionsTextMesh.text != newVal)
                {
                    quizMaxSelectionsTextMesh.text = newVal;
#if UNITY_EDITOR
                    EditorUtility.SetDirty(quizMaxSelectionsTextMesh);
#endif
                }
            }
        }

        [ContextMenu("Update all Texts!")]
        private void UpdateAllTexts()
        {
            UpdateSizeText();
            UpdateTitleText();
            UpdateLayoutText();
            UpdateAnswersText();
        }

#if UNITY_EDITOR
        int lastAnswersCount = 0;
        private void OnValidate()
        {
            // Title TextArea and Answers List can't be managed with "OnChangedCall" attribute.

            // Title
            UpdateTitleText();

            // Answers
            if (lastAnswersCount != QuizAnswers.Count)
            {
                lastAnswersCount = QuizAnswers.Count;
                UpdateAnswersText();
            }
        }
#endif

        #endregion
    }
}
