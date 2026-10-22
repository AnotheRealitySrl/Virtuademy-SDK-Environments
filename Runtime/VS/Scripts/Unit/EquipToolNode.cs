using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    /// <summary>
    /// Puts the tool with the given item name in the local player's hand. Use it in answer to
    /// something the player did: the application never switches tools on its own.
    /// </summary>
    [UnitTitle("Virtuademy Tools: Equip Tool")]
    [UnitSurtitle("Tools")]
    [UnitShortTitle("Equip Tool")]
    [UnitCategory("Virtuademy\\Flow")]
    public class EquipToolNode : Unit
    {
        [DoNotSerialize]
        public ValueInput ToolName { get; private set; }

        [DoNotSerialize]
        public ValueOutput Equipped { get; private set; }

        [DoNotSerialize]
        [PortLabelHidden]
        public ControlInput InputTrigger { get; private set; }

        [DoNotSerialize]
        [PortLabelHidden]
        public ControlOutput OutputTrigger { get; private set; }

        private bool equipped;

        protected override void Definition()
        {
            ToolName = ValueInput<string>(nameof(ToolName), string.Empty);

            InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
            {
                equipped = IVirtuademyGameplay.Current.Tools.EquipTool(f.GetValue<string>(ToolName));

                return OutputTrigger;
            });

            OutputTrigger = ControlOutput(nameof(OutputTrigger));
            Equipped = ValueOutput<bool>(nameof(Equipped), (f) => equipped);

            Requirement(ToolName, InputTrigger);
            Succession(InputTrigger, OutputTrigger);
            Assignment(InputTrigger, Equipped);
        }
    }
}
