using System;

using Virtuademy.ScriptingApi;

using System.Collections;

namespace Virtuademy.Environments.ScriptingApi
{
    /// <summary>
    /// Fading the view. Both members report completion through a callback because the fade takes
    /// time and a script cannot await.
    /// </summary>
    public interface IScreenApi
    {
        /// <summary>Fades the view to black. <paramref name="onDone"/> runs once it is.</summary>
        /// <remarks>Nodes: <c>Virtuademy Scene: Fade To Black</c>.</remarks>
        IEnumerator FadeToBlack(Action onDone = null);

        /// <summary>Fades the view back in. <paramref name="onDone"/> runs once it has.</summary>
        /// <remarks>Nodes: <c>Virtuademy Scene: Fade From Black</c>.</remarks>
        IEnumerator FadeFromBlack(Action onDone = null);
    }
}
