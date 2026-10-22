using System.Collections;
using System.Collections.Generic;

using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
    /// <summary>
    /// Shows a popup (<see cref="IUIApi.ShowPopup"/>) and continues twice: at once through
    /// <c>Shown</c>, and when the popup closes through <c>Closed</c>, with the index of the button
    /// pressed (0 for the first, -1 when it closed without one).
    /// </summary>
    [UnitTitle("Virtuademy UI: Show Popup")]
    [UnitSurtitle("UI")]
    [UnitShortTitle("Show Popup")]
    [UnitCategory("Virtuademy\\Flow")]
    public class ShowPopupNode : Unit
    {
        [DoNotSerialize] public ValueInput Title { get; private set; }
        [DoNotSerialize] public ValueInput Text { get; private set; }

        /// <summary>First button. With only this one, it is the primary button.</summary>
        [DoNotSerialize] public ValueInput Button1 { get; private set; }

        /// <summary>Second button; empty: none.</summary>
        [DoNotSerialize] public ValueInput Button2 { get; private set; }

        /// <summary>Third button; empty: none.</summary>
        [DoNotSerialize] public ValueInput Button3 { get; private set; }

        [DoNotSerialize, PortLabelHidden] public ControlInput InputTrigger { get; private set; }

        /// <summary>Runs as soon as the popup is shown.</summary>
        [DoNotSerialize] public ControlOutput Shown { get; private set; }

        /// <summary>Runs when the popup closes.</summary>
        [DoNotSerialize] public ControlOutput Closed { get; private set; }

        /// <summary>The button that closed it, from 0; -1 when it closed without one.</summary>
        [DoNotSerialize] public ValueOutput ButtonIndex { get; private set; }

        protected override void Definition()
        {
            Title = ValueInput<string>(nameof(Title), string.Empty);
            Text = ValueInput<string>(nameof(Text), string.Empty);
            Button1 = ValueInput<string>(nameof(Button1), "OK");
            Button2 = ValueInput<string>(nameof(Button2), string.Empty);
            Button3 = ValueInput<string>(nameof(Button3), string.Empty);

            InputTrigger = ControlInputCoroutine(nameof(InputTrigger), Run);
            Shown = ControlOutput(nameof(Shown));
            Closed = ControlOutput(nameof(Closed));
            ButtonIndex = ValueOutput<int>(nameof(ButtonIndex));

            Requirement(Title, InputTrigger);
            Requirement(Text, InputTrigger);
            Succession(InputTrigger, Shown);
            Succession(InputTrigger, Closed);
            Assignment(InputTrigger, ButtonIndex);
        }

        private IEnumerator Run(Flow flow)
        {
            var labels = new List<string>(3);
            foreach (ValueInput input in new[] { Button1, Button2, Button3 })
            {
                string label = flow.GetValue<string>(input);
                if (!string.IsNullOrEmpty(label))
                {
                    labels.Add(label);
                }
            }

            bool closed = false;
            int index = -1;
            IVirtuademyGameplay.Current.UI.ShowPopup(flow.GetValue<string>(Title), flow.GetValue<string>(Text),
                labels.ToArray(), i =>
                {
                    index = i;
                    closed = true;
                });

            yield return Shown;

            while (!closed)
            {
                yield return null;
            }

            flow.SetValue(ButtonIndex, index);
            yield return Closed;
        }
    }
}
