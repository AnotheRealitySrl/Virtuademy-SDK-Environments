namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// The way into the Environment Details window from the scene rows of Publish Environments. The window is not compiled in the application
    /// (<c>VIRTUADEMY_APPLICATION</c>), like Publish Environments, whose rows open it (and its own menu item); there this does nothing.
    /// </summary>
    internal static class EnvironmentDetailsEntry
    {
        /// <summary>Opens the single Environment Details window on a scene (its environment name, <see cref="PublishedEnvironmentsIndex.Key"/>), or brings the open one to it.</summary>
        public static void Open(string sceneKey)
        {
#if !VIRTUADEMY_APPLICATION
            EnvironmentDetailsWindow.Open(sceneKey);
#endif
        }
    }
}
