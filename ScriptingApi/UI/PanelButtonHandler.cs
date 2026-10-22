using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.UI
{
    /// <summary>
    /// A button of an SDK panel was pressed. <paramref name="panel"/> is the panel's GameObject;
    /// <paramref name="index"/> is 0 for the primary button and 1 for the secondary one of a panel,
    /// the option's index on a choice panel, 0 for the hint button of a HUD.
    /// </summary>
    public delegate void PanelButtonHandler(GameObject panel, int index);
}
