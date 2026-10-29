using Virtuademy.SDK.TenantConfiguration.Editor;
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;


namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// The confirmation of "Remove from this computer..." in the Publish Environments window (meta-repo ADR 0037, "Removal from
    /// the computer"). The removal reaches every Virtuademy project on the computer and cannot be undone, so it is not the
    /// standard dialog: it names the tenant and the account, offers "Log out only", has Cancel as its default button and
    /// enables the removal only once the tenant's name is typed.
    /// </summary>
    public sealed class RemoveConnectionWindow : EditorWindow
    {
        private const string window_title = "Remove from this computer";
        private const float window_width = 460;
        private const float window_height = 330;

        // Kept in the window, captured before anything is deleted: the record is gone when the removal ends.
        [SerializeField] private string connectionKey;
        [SerializeField] private string tenantLabel;
        [SerializeField] private string environment;
        [SerializeField] private string account;

        private TextField confirmationField;
        private Button cancelButton;
        private Button logoutButton;
        private Button removeButton;
        private Label errorLabel;
        private bool busy;

        /// <summary>The text that has to be typed: the tenant's label; the connection key for a record that has none.</summary>
        private string Expected => string.IsNullOrWhiteSpace(tenantLabel) ? connectionKey : tenantLabel.Trim();

        /// <summary>
        /// Opens the window for one connection; a window already open for another one is closed. A connection that is no longer
        /// in the store (null) gets a short message instead.
        /// </summary>
        public static void Open(ConnectionRecord record)
        {
            if (record == null)
            {
                EditorUtility.DisplayDialog(window_title, "This connection is no longer on this computer.", "OK");
                return;
            }

            foreach (RemoveConnectionWindow open in Resources.FindObjectsOfTypeAll<RemoveConnectionWindow>())
                open.Close();

            RemoveConnectionWindow window = CreateInstance<RemoveConnectionWindow>();
            window.connectionKey = record.Key;
            window.tenantLabel = record.TenantLabel;
            window.environment = record.Environment;
            window.account = record.Username;
            window.titleContent = new GUIContent(window_title);
            window.minSize = window.maxSize = new Vector2(window_width, window_height);

            Rect main = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(main.x + (main.width - window_width) / 2, main.y + (main.height - window_height) / 2,
                window_width, window_height);
            window.ShowUtility();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = root.style.paddingRight = 12;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            string label = string.IsNullOrWhiteSpace(tenantLabel) ? connectionKey : tenantLabel;
            string environmentText = string.IsNullOrEmpty(environment) ? string.Empty : $" ({environment})";
            string accountText = string.IsNullOrEmpty(account) ? "never logged in" : account;

            root.Add(new Label($"Remove {label}{environmentText} from this computer?")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13, whiteSpace = WhiteSpace.Normal, marginBottom = 6 },
            });

            root.Add(Line($"Tenant: {label}{environmentText}"));
            root.Add(Line($"Account: {accountText}"));

            root.Add(new Label("The removal applies to every Virtuademy project on this computer, not only this one. " +
                               "To use this tenant again you need its configuration file from the Back Office, and a new login. " +
                               "It cannot be undone.")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginTop = 8, marginBottom = 4 },
            });
            root.Add(new Label("To stop using the account but keep the connection and its ticked targets, choose Log out only.")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginBottom = 8, fontSize = 11, color = new Color(0.6f, 0.6f, 0.6f) },
            });

            root.Add(new Label($"To remove it, type the tenant's name: {Expected}")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginBottom = 2 },
            });
            confirmationField = new TextField { style = { marginLeft = 0, marginRight = 0 } };
            confirmationField.RegisterValueChangedCallback(_ => RefreshButtons());
            root.Add(confirmationField);

            errorLabel = new Label
            {
                style = { whiteSpace = WhiteSpace.Normal, marginTop = 4, color = new Color(0.9f, 0.45f, 0.45f), display = DisplayStyle.None },
            };
            root.Add(errorLabel);

            root.Add(new VisualElement { style = { flexGrow = 1 } });

            VisualElement buttons = new() { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd } };
            cancelButton = new Button(Close) { text = "Cancel", style = { minWidth = 80 } };
            logoutButton = new Button(() => _ = LogoutOnlyAsync())
            {
                text = "Log out only",
                tooltip = "Ends the login. The connection and its ticked targets stay.",
                style = { minWidth = 100 },
            };
            removeButton = new Button(() => _ = RemoveAsync())
            {
                text = "Remove from this computer",
                tooltip = "Type the tenant's name above to enable it.",
                style =
                {
                    backgroundColor = new Color(0.72f, 0.2f, 0.2f),
                    color = Color.white,
                },
            };
            buttons.Add(cancelButton);
            buttons.Add(logoutButton);
            buttons.Add(removeButton);
            root.Add(buttons);

            // Escape cancels, from anywhere in the window.
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape || busy)
                    return;
                evt.StopPropagation();
                Close();
            }, TrickleDown.TrickleDown);

            RefreshButtons();

            // Cancel is the default: Enter or Space on it does nothing but close the window.
            root.schedule.Execute(() => cancelButton.Focus());
        }

        private static Label Line(string text) => new(text) { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 1 } };

        private void RefreshButtons()
        {
            if (removeButton == null)
                return;

            bool typed = string.Equals(confirmationField.value?.Trim(), Expected, StringComparison.Ordinal);
            removeButton.SetEnabled(typed && !busy);
            logoutButton.SetEnabled(!busy && ConnectionStore.Get(connectionKey)?.HasSession == true);
            cancelButton.SetEnabled(!busy);
            confirmationField.SetEnabled(!busy);
        }

        private async Task LogoutOnlyAsync()
        {
            busy = true;
            RefreshButtons();
            try
            {
                // A login waiting in the browser would bring the account back right after.
                AddressablesManagementWindow.CancelPendingLogins(connectionKey);
                await EditorSessionManager.LogoutAsync(connectionKey);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PublishEnvironments] Logging out of {Expected} failed: {ex.Message}");
            }

            // Closed with the connection still in the store: its group shows "not logged in".
            if (this != null)
                Close();
        }

        private async Task RemoveAsync()
        {
            // The text is checked again here: the button state is only a convenience.
            if (busy || !string.Equals(confirmationField.value?.Trim(), Expected, StringComparison.Ordinal))
                return;

            busy = true;
            errorLabel.style.display = DisplayStyle.None;
            RefreshButtons();

            string key = connectionKey;
            bool removed = false;
            string failure = null;
            try
            {
                // A deploy holds on to the connection's token; a login waiting in the browser for it is dropped.
                if (AddressablesManagementWindow.DeployRunning)
                    failure = "a deploy is running. Try again when it ends";
                else
                {
                    AddressablesManagementWindow.CancelPendingLogins(key);
                    removed = await ConnectionLoginService.RemoveFromComputerAsync(key);
                }
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                Debug.LogError($"[PublishEnvironments] Removing {Expected} from this computer failed: {ex.Message}");
            }

            if (removed)
                PublishedEnvironmentsIndex.Clear(key);

            if (this == null)
                return;

            if (removed)
            {
                Close();
                return;
            }

            busy = false;
            errorLabel.text = failure != null
                ? $"The connection was not removed: {failure}."
                : "The connection was not removed. Try again, or see the Console.";
            errorLabel.style.display = DisplayStyle.Flex;
            RefreshButtons();
        }
    }
}
