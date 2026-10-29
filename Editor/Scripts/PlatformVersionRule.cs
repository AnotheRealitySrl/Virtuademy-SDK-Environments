using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// The version rule for deploy targets (meta-repo ADR 0037): a tenant can be deployed to only when its platform
    /// version has the year and the release of the project's. The project's version is the registry entry the project
    /// was set up from, which the setup package records in its configuration asset
    /// (<c>PackageManagerConfiguration.currentInstallationVersion</c>); a tenant's is the <c>platformVersion</c> its
    /// <c>config-public</c> gave when the connection was last resolved.
    /// </summary>
    /// <remarks>
    /// The comparison stops at year and release: within them it is a coarse gate, and a route the tenant's API lacks is
    /// found from its answer. The project's asset is found by type name (or at its default path) and read as text, so this
    /// package does not reference the setup package. A project with no such asset, or one set up from an entry that is not
    /// a version (a prerelease channel such as <c>develop</c>), has nothing to compare: the rule is not applied there, and
    /// <see cref="Check"/> says so.
    /// </remarks>
    internal static class PlatformVersionRule
    {
        internal enum Verdict
        {
            /// <summary>Same year and release: the target can be deployed to.</summary>
            Match,

            /// <summary>Another year or release: the target is refused.</summary>
            Mismatch,

            /// <summary>The tenant reported no platform version: the target is refused.</summary>
            TargetUnknown,

            /// <summary>The project has no version to compare with: the rule is not applied.</summary>
            ProjectUnknown,
        }

        private const string setup_type = "PackageManagerConfiguration";
        private const string setup_default_path = "Assets/Virtuademy/Editor/Settings/SetupConfiguration.asset";
        private static readonly Regex setup_line = new(@"^\s*currentInstallationVersion:[ \t]*(.*?)\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        private static readonly Regex year_release = new(@"^\s*(\d{4})\.(\d+)(?:\.\d+)?\s*$", RegexOptions.CultureInvariant);

        /// <summary>
        /// The registry entry the project was set up from, as the setup package records it, or null when the project
        /// has no setup configuration (the application project, which embeds the packages).
        /// </summary>
        public static string ProjectVersion
        {
            get
            {
                // Read as text: the type is known only while the setup package is installed, and the version the project
                // was set up from is still the project's when it is not.
                string guid = AssetDatabase.FindAssets("t:" + setup_type).FirstOrDefault();
                string path = string.IsNullOrEmpty(guid) ? setup_default_path : AssetDatabase.GUIDToAssetPath(guid);
                if (!File.Exists(path))
                    return null;

                Match match = setup_line.Match(File.ReadAllText(path));
                string value = match.Success ? match.Groups[1].Value.Trim() : null;
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        /// <summary>Whether a tenant on <paramref name="targetVersion"/> can be deployed to from this project.</summary>
        public static Verdict Check(string targetVersion) => Check(ProjectVersion, targetVersion);

        public static Verdict Check(string projectVersion, string targetVersion)
        {
            if (!TryYearRelease(projectVersion, out string project))
                return Verdict.ProjectUnknown;

            if (!TryYearRelease(targetVersion, out string target))
                return Verdict.TargetUnknown;

            return project == target ? Verdict.Match : Verdict.Mismatch;
        }

        /// <summary>True when the verdict lets the target be ticked and deployed to.</summary>
        public static bool Allows(Verdict verdict) => verdict == Verdict.Match || verdict == Verdict.ProjectUnknown;

        /// <summary>Why a target is refused, in the creator's words; null when it is not.</summary>
        public static string Refusal(Verdict verdict, string projectVersion, string targetVersion) => verdict switch
        {
            Verdict.Mismatch => $"This tenant runs platform version {targetVersion}, and the project was set up for {projectVersion}: " +
                                "it cannot be deployed to from this project. Update the project to the tenant's version, or deploy from a project " +
                                "set up for it.",
            Verdict.TargetUnknown => "This tenant does not report its platform version, so whether the project's environments run there " +
                                     "cannot be checked: it cannot be deployed to from this project.",
            _ => null,
        };

        private static bool TryYearRelease(string version, out string yearRelease)
        {
            yearRelease = null;
            if (string.IsNullOrWhiteSpace(version))
                return false;

            Match match = year_release.Match(version);
            if (!match.Success)
                return false;

            yearRelease = $"{match.Groups[1].Value}.{int.Parse(match.Groups[2].Value)}";
            return true;
        }
    }
}
