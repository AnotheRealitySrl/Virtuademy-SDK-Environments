using Virtuademy.Environments.ScriptingApi.Placeholders;

using Unity.VisualScripting;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy: Expose Quiz Answer")]
    [UnitSurtitle("Expose")]
    [UnitShortTitle("Quiz Answer")]
    [UnitCategory("Virtuademy\\Expose")]
    public class ExposeQuizAnswerUnit : Unit
    {
        [NullMeansSelf]
        [DoNotSerialize]
        [PortLabelHidden]
        public ValueInput Answer { get; private set; }

        [DoNotSerialize]
        public ValueOutput TitleLabel { get; private set; }

        [DoNotSerialize]
        public ValueOutput TitleValue { get; private set; }

        [DoNotSerialize]
        public ValueOutput HiddenTitleLabel { get; private set; }

        [DoNotSerialize]
        public ValueOutput HiddenTitleValue { get; private set; }

        [DoNotSerialize]
        public ValueOutput Image { get; private set; }

        [DoNotSerialize]
        public ValueOutput CorrectAnswer { get; private set; }

        [DoNotSerialize]
        public ValueOutput ScoreIfGood { get; private set; }

        [DoNotSerialize]
        public ValueOutput ScoreIfBad { get; private set; }

        [DoNotSerialize]
        public ValueOutput FeedbackLabel { get; private set; }

        [DoNotSerialize]
        public ValueOutput FeedbackValue { get; private set; }

        [DoNotSerialize]
        public ValueOutput IsSelected { get; private set; }

        [DoNotSerialize]
        public ValueOutput IsCorrectSelection { get; private set; }

        [DoNotSerialize]
        public ValueOutput Score { get; private set; }

        protected override void Definition()
        {
            Answer = ValueInput<QuizAnswer>(nameof(Answer), null).NullMeansSelf();

            // Anagraphics

            TitleLabel = ValueOutput(nameof(TitleLabel), (flow) => flow.GetValue<QuizAnswer>(Answer).TitleLabel);

            TitleValue = ValueOutput(nameof(TitleValue), (flow) =>
            {
                QuizAnswer ans = flow.GetValue<QuizAnswer>(Answer);
                string locLbl = ans.TitleLabel;
                string locVal = ans.QuizInstanceAnswerTitleValue;

                return !string.IsNullOrEmpty(locVal) ? locVal : locLbl;
            });

            HiddenTitleLabel = ValueOutput(nameof(HiddenTitleLabel), (flow) =>
            {
                var locLbl = flow.GetValue<QuizAnswer>(Answer).HiddenTitleLabel;
                // If "HiddenTitle" is not specified, it fallbacks using "Title".
                if (string.IsNullOrEmpty(locLbl))
                {
                    locLbl = flow.GetValue<QuizAnswer>(Answer).TitleLabel;
                }

                return locLbl;
            });


            HiddenTitleValue = ValueOutput(nameof(HiddenTitleValue), (flow) =>
            {
                QuizAnswer ans = flow.GetValue<QuizAnswer>(Answer);
                string locLbl, locVal;
                // If "HiddenTitle" is not specified, it fallbacks using "Title".
                if (!string.IsNullOrWhiteSpace(ans.HiddenTitleLabel))
                {
                    locLbl = ans.HiddenTitleLabel;
                    locVal = ans.QuizInstanceAnswerHiddenTitleValue;
                }
                else
                {
                    locLbl = ans.TitleLabel;
                    locVal = ans.QuizInstanceAnswerTitleValue;
                }

                return !string.IsNullOrWhiteSpace(locVal) ? locVal : locLbl;
            });

            Image = ValueOutput(nameof(Image), (flow) => flow.GetValue<QuizAnswer>(Answer).Image);

            CorrectAnswer = ValueOutput(nameof(CorrectAnswer), (flow) => flow.GetValue<QuizAnswer>(Answer).CorrectAnswer);

            ScoreIfGood = ValueOutput(nameof(ScoreIfGood), (flow) => flow.GetValue<QuizAnswer>(Answer).ScoreIfGood);

            ScoreIfBad = ValueOutput(nameof(ScoreIfBad), (flow) => flow.GetValue<QuizAnswer>(Answer).ScoreIfBad);

            FeedbackLabel = ValueOutput(nameof(FeedbackLabel), (flow) => flow.GetValue<QuizAnswer>(Answer).FeedbackLabel);

            FeedbackValue = ValueOutput(nameof(FeedbackValue), (flow) =>
            {
                QuizAnswer ans = flow.GetValue<QuizAnswer>(Answer);
                string locLbl = ans.FeedbackLabel;
                string locVal = ans.QuizInstanceAnswerFeedbackValue;

                return !string.IsNullOrEmpty(locVal) ? locVal : locLbl;
            });

            // Instance-related info

            IsSelected = ValueOutput(nameof(IsSelected), (flow) => flow.GetValue<QuizAnswer>(Answer).IsSelected);

            IsCorrectSelection = ValueOutput(nameof(IsCorrectSelection), (flow) => flow.GetValue<QuizAnswer>(Answer).IsCorrectSelection);

            Score = ValueOutput(nameof(Score), (flow) => flow.GetValue<QuizAnswer>(Answer).CurrentScore);
        }
    }
}
