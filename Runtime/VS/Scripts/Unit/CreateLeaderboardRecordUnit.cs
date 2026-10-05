using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    [UnitTitle("Virtuademy Leaderboard Create Record: Create Record")]
    [UnitSurtitle("Leaderboard Create Record")]
    [UnitShortTitle("Create Record")]
    [UnitCategory("Virtuademy\\Flow")]
    public class CreateLeaderboardRecordUnit : Unit
    {
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }
        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        public ValueInput LeaderboardKey { get; private set; }

        [NullMeansSelf]
        [DoNotSerialize]
        public ValueInput Data { get; private set; }

        protected override void Definition()
        {
            LeaderboardKey = ValueInput<string>(nameof(LeaderboardKey), string.Empty);

            Data = ValueInput<float>(nameof(Data), 0f);

            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                IVirtuademyFramework.Current.SaveData.SubmitLeaderboardRecord(
                    f.GetValue<string>(LeaderboardKey),
                    f.GetValue<float>(Data));

                return OutputTrigger;
            });

            OutputTrigger = ControlOutput(nameof(OutputTrigger));

            Succession(InputTrigger, OutputTrigger);
        }
    }
}
