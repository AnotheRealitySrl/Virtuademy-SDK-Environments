
using UnityEngine;

namespace Virtuademy.Environments.ScriptingApi.Placeholders
{
    /// <summary>
    /// Marks a page of a <see cref="POIPlaceholder"/>: every GameObject carrying it under
    /// <see cref="POIPlaceholder.PagesContainer"/> becomes one page at runtime, and the
    /// <see cref="POIBlockPlaceholder"/>s below it become that page's content. To add a page,
    /// duplicate the "Page" GameObject under "Pages".
    /// </summary>
    /// <remarks>
    /// Pages used to be marked with the framework's GenericHookComponent (id "POIPage"). That
    /// component lives in Virtuademy-SystemCore, which a creator project no longer installs, so
    /// in a creator project the marker was a missing script, a world published from it carried
    /// no marker at all, and every POI in it came up empty. The v2026.5 -> v2026.6 update routine
    /// swaps the old marker for this one in a creator's scenes and prefabs.
    /// </remarks>
    [DisallowMultipleComponent]
    public class POIPagePlaceholder : MonoBehaviour
    {
    }
}
