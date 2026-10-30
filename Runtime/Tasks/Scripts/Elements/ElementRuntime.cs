using System;

using UnityEngine;

using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>Gives every task element placeholder its driver when it wakes up (application and play mode).</summary>
    internal static class ElementRuntime
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            // Without a domain reload the static event keeps its handlers: never subscribe twice.
            ElementPlaceholderBase.Created -= OnCreated;
            ElementPlaceholderBase.Created += OnCreated;
        }

        private static void OnCreated(ElementPlaceholderBase element)
        {
            Type driverType = DriverOf(element) ?? throw new InvalidOperationException(
                "[Elements] no driver for " + element.GetType().Name);
            if (!element.TryGetComponent(driverType, out Component driver))
            {
                driver = element.gameObject.AddComponent(driverType);
            }
            ((ElementDriver)driver).Bind(element);
        }

        internal static Type DriverOf(ElementPlaceholderBase element) => element switch
        {
            TargetElementPlaceholder => typeof(TargetElementDriver),
            ChoiceElementPlaceholder => typeof(ChoiceElementDriver),
            DistractorElementPlaceholder => typeof(DistractorElementDriver),
            ReachZoneElementPlaceholder => typeof(ReachZoneElementDriver),
            WatchElementPlaceholder => typeof(WatchElementDriver),
            QuestionElementPlaceholder => typeof(QuestionElementDriver),
            ItemOnTargetElementPlaceholder => typeof(ItemOnTargetElementDriver),
            _ => null,
        };
    }
}
