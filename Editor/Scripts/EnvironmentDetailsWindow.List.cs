#if !VIRTUADEMY_APPLICATION
using System;
using System.Globalization;
using System.Linq;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.Editor
{
    // The left list: "All places", then one group per connection, drawn as Publish Environments draws its groups, with the places under it.
    internal sealed partial class EnvironmentDetailsWindow
    {
        private static readonly string[] lock_icons = { "LockIcon-On", "d_LockIcon-On", "IN LockButton on", "d_IN LockButton on" };

        private const string indicator_tooltip = "Thumbnail · Localization file: green = same file in the project, amber = online only, empty = none";
        private const string locked_tooltip = "You can see this place but not change it";

        private void BuildList(VisualElement list)
        {
            bool anyLoading = connections.Any(c => c.Load == ConnectionLoad.Loading);
            int places = PlaceCount();

            VisualElement all = new();
            all.AddToClassList("vd-li");
            all.AddToClassList("vd-li--all");
            all.EnableInClassList("vd-li--sel", allSelected);
            all.Add(Text("All places", "vd-li-name"));
            if (places > 0)
                all.Add(Text(places.ToString(CultureInfo.InvariantCulture), "muted"));

            // While nothing has been read there is nothing to select.
            if (places > 0 || !anyLoading)
                all.RegisterCallback<ClickEvent>(_ => SelectAllPlaces());

            list.Add(all);

            foreach (DetailsConnection connection in connections)
                BuildGroup(list, connection);
        }

        private void BuildGroup(VisualElement list, DetailsConnection connection)
        {
            // The header: the state, who the tenant is, and the platform version it runs.
            VisualElement head = new();
            head.AddToClassList("vd-group-head");

            VisualElement dot = new() { tooltip = connection.DotTooltip ?? string.Empty };
            dot.AddToClassList("dot");
            dot.AddToClassList(connection.Dot == ConnectionDot.Green ? "vd-dot--green" : connection.Dot == ConnectionDot.Blue ? "vd-dot--blue"
                : connection.Dot == ConnectionDot.Amber ? "vd-dot--amber" : "vd-dot--grey");
            head.Add(dot);

            Label label = new(connection.Label) { tooltip = $"Connection: {connection.Key}" };
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginRight = 4;
            label.style.flexShrink = 1;
            head.Add(label);

            // Production carries no chip: the chips mark the environments that are not the real one.
            if (!string.IsNullOrEmpty(connection.Environment))
            {
                Label environment = Chip(connection.Environment, "The tenant's environment.");
                if (string.Equals(connection.Environment, "Sandbox", StringComparison.OrdinalIgnoreCase))
                    environment.AddToClassList("chip-sandbox");
                else if (connection.Environment.StartsWith("Pre", StringComparison.OrdinalIgnoreCase))
                    environment.AddToClassList("chip-preproduction");
                head.Add(environment);
            }

            head.Add(Chip(connection.PlatformVersion ?? "no version", "The tenant's platform version."));
            list.Add(head);

            Label account = Text(connection.Account ?? string.Empty, "muted", "vd-group-account");
            account.tooltip = $"Connection: {connection.Key}";
            list.Add(account);

            switch (connection.Load)
            {
                case ConnectionLoad.Loading:
                    VisualElement loading = new();
                    loading.AddToClassList("vd-group-msg");
                    loading.Add(Text("Loading places…", "muted"));
                    loading.Add(Skeleton(70));
                    loading.Add(Skeleton(55));
                    list.Add(loading);
                    return;

                case ConnectionLoad.LoggedOut:
                    list.Add(BuildLoginMessage(connection));
                    return;

                case ConnectionLoad.Failed:
                    VisualElement failed = new();
                    failed.AddToClassList("vd-group-msg");
                    Label reason = Text($"Could not read {connection.Label}: {Sentence(connection.Error)}.", "vd-small", "vd-err");
                    reason.tooltip = connection.Error ?? string.Empty;
                    failed.Add(reason);
                    failed.Add(Btn("Retry", () => _ = LoadConnectionAsync(connection), "Reads this connection again."));
                    list.Add(failed);
                    return;
            }

            if (!connection.Targets.Any())
            {
                VisualElement none = new();
                none.AddToClassList("vd-group-msg");
                none.Add(Text("Not published here.", "muted"));
                list.Add(none);
                return;
            }

            foreach (FilesTarget target in OrderedPlaces(connection))
                list.Add(BuildPlaceRow(connection, target));
        }

        /// <summary>"Log in to see where this scene is published here." with Log in, or, while a login waits for the browser, what it says with Copy link and Cancel.</summary>
        private VisualElement BuildLoginMessage(DetailsConnection connection)
        {
            VisualElement message = new();
            message.AddToClassList("vd-group-msg");

            if (connection.LoginPending)
            {
                message.Add(Text(connection.LoginAuthUrl != null ? "Waiting for the browser…" : connection.LoginStatus ?? "Logging in…", "muted"));
                VisualElement buttons = Row(
                    Btn("Copy link", () => EditorGUIUtility.systemCopyBuffer = connection.LoginAuthUrl, "Copies the login address, to open it in another browser.",
                        enabled: connection.LoginAuthUrl != null),
                    Btn("Cancel", () => connection.LoginCancellation?.Cancel()));
                buttons.style.marginTop = 4;
                message.Add(buttons);
                return message;
            }

            message.Add(Text("Log in to see where this scene is published here.", "muted"));
            if (!string.IsNullOrEmpty(connection.LoginError))
            {
                Label error = Text(connection.LoginError, "vd-small", "vd-err");
                error.style.marginTop = 2;
                message.Add(error);
            }

            message.Add(Btn("Log in", () => LoginConnection(connection), "Logs in to this tenant in the browser."));
            return message;
        }

        private VisualElement BuildPlaceRow(DetailsConnection connection, FilesTarget target)
        {
            bool isSelected = !allSelected && selected != null && selected.Value.Equals(new PlaceKey(connection.Key, target.WorldId));

            VisualElement row = new();
            row.AddToClassList("vd-li");
            row.EnableInClassList("vd-li--sel", isSelected);

            VisualElement name = Row();
            name.AddToClassList("vd-li-name");
            if (target.IsTenant)
            {
                name.Add(Text("Tenant level", "vd-li-tenant"));
            }
            else
            {
                Label world = new(target.WorldLabel);
                world.style.flexShrink = 1;
                world.style.overflow = Overflow.Hidden;
                world.style.textOverflow = TextOverflow.Ellipsis;
                name.Add(world);
                Label id = Text($"#{target.WorldId.Value.ToString(CultureInfo.InvariantCulture)}", "muted");
                id.style.marginLeft = 4;
                id.style.flexShrink = 0;
                name.Add(id);
            }

            row.Add(name);

            if (EnvironmentFilesService.RefusalFor(target) != null)
                row.Add(LockMark());

            VisualElement indicators = new() { tooltip = indicator_tooltip };
            indicators.AddToClassList("vd-ind");
            indicators.Add(Indicator(target.Thumbnail));
            indicators.Add(Indicator(target.Localization));
            row.Add(indicators);

            Label count = Text(string.Empty, "muted", "vd-li-count");
            if (!target.IsTenant)
            {
                connection.Experiences.TryGetValue(target.WorldId.Value, out WorldExperiences experiences);
                count.text = experiences == null || experiences.Loading ? "…" : experiences.Known ? $"{experiences.Data.Counts.Total} exp" : "–";
                count.tooltip = experiences?.Known == true ? "Experiences that use this environment in this world." : string.Empty;
            }

            row.Add(count);
            row.RegisterCallback<ClickEvent>(_ => Select(connection.Key, target.WorldId));
            return row;
        }

        private static VisualElement Indicator(FileStatus status)
        {
            VisualElement square = new();
            square.AddToClassList("vd-ind-sq");
            switch (status.Presence)
            {
                case Presence.InProject:
                    square.AddToClassList("vd-ind-sq--yes");
                    break;
                case Presence.OnlineOnly:
                    square.AddToClassList("vd-ind-sq--online");
                    break;
            }

            return square;
        }

        /// <summary>The padlock of a place the account can see and not change: an icon of the editor, or the character when there is none.</summary>
        private static VisualElement LockMark()
        {
            Texture2D icon = null;
            foreach (string name in lock_icons)
            {
                icon = EditorGUIUtility.FindTexture(name);
                if (icon != null)
                    break;
            }

            if (icon == null)
            {
                Label fallback = Text("\U0001F512", "muted", "vd-li-lock");
                fallback.tooltip = locked_tooltip;
                return fallback;
            }

            VisualElement mark = new() { tooltip = locked_tooltip };
            mark.AddToClassList("vd-li-lock");
            mark.style.backgroundImage = new StyleBackground(icon);
            return mark;
        }

        /// <summary>A message without its closing full stop, so a sentence can add its own.</summary>
        private static string Sentence(string message) => string.IsNullOrWhiteSpace(message) ? "the platform did not answer" : message.Trim().TrimEnd('.');
    }
}
#endif
