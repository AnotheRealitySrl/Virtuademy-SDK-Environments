using System;
using System.IO;
using System.Linq;

using UnityEditor;

using UnityEngine;

namespace Virtuademy.SDK.Environments.HybridCLR.Editor
{
    /// <summary>
    /// Finds the project's compiled hot-update assembly. Every creator project compiles it under
    /// the same name, <see cref="HotUpdateSetupper.ScriptAlias"/>, so there is nothing to guess: the
    /// editor's copy in Library/ScriptAssemblies (with its PDB, for line numbers) or HybridCLR's
    /// per-target copy in HybridCLRData/HotUpdateDlls.
    /// </summary>
    internal static class HotUpdateDllLocator
    {
        public static string ProjectRoot => Directory.GetParent(Application.dataPath)!.FullName;

        public static string ScriptAssembliesDll(string assemblyName)
            => Path.Combine(ProjectRoot, "Library", "ScriptAssemblies", assemblyName + ".dll");

        public static string HotUpdateDll(string assemblyName, string target)
            => Path.Combine(ProjectRoot, "HybridCLRData", "HotUpdateDlls", target, assemblyName + ".dll");

        public static string ResolveAssemblyName() => HotUpdateSetupper.ScriptAlias;

        public static string ResolveDefaultDllPath(out string assemblyName)
        {
            assemblyName = HotUpdateSetupper.ScriptAlias;
            string dll = ScriptAssembliesDll(assemblyName);
            return File.Exists(dll) ? dll : null;
        }

        public static string ResolveTargetDllPath(string target, out string assemblyName)
        {
            assemblyName = HotUpdateSetupper.ScriptAlias;
            return HotUpdateDll(assemblyName, target);
        }

        /// <summary>SHA-256 of the bytes as lowercase hex (informational, shown in the log).</summary>
        public static string Sha256Hex(byte[] data)
        {
            using System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
            byte[] hash = sha.ComputeHash(data);
            System.Text.StringBuilder sb = new(hash.Length * 2);
            foreach (byte b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>Runs the verifier against the given policy and writes the outcome to the
        /// Unity Console.</summary>
        [HideInCallstack]
        public static VerificationResult VerifyAndLog(string dllPath, HotUpdatePolicy policy)
        {
            VerificationResult result = HotUpdateAssemblyVerifier.VerifyFile(dllPath, policy);
            LogResult(result, dllPath);
            return result;
        }

        /// <summary>
        /// Writes the result to the Console as ONE entry per violation, each aimed at the line that
        /// caused it (when a PDB is present) three separate ways, because the Console offers three
        /// separate ways to follow a message and they do not use the same information:
        ///
        /// <list type="bullet">
        /// <item>a hyperlink, for a single click on the <c>file:line</c> text;</item>
        /// <item>the asset as the entry's context object, which is what a double-click opens;</item>
        /// <item>a synthesised stack frame, so the file and the LINE can be parsed out of the entry.</item>
        /// </list>
        ///
        /// The last two exist because a double-click resolves through the entry's stack trace, and
        /// the creator's script is never in ours: a violation is found by reading the assembly's
        /// metadata, not by running their code, so no real frame points at it. Left alone, a
        /// double-click lands on this method — the code that reported the problem instead of the
        /// code that has it.
        /// </summary>
        [HideInCallstack]
        public static void LogResult(VerificationResult result, string dllPath, string sha256 = null)
        {
            string name = Path.GetFileName(dllPath);
            string shaLine = string.IsNullOrEmpty(sha256) ? string.Empty : $"\n  sha256: {sha256}";

            if (result.Passed)
            {
                Debug.Log($"[HotUpdateSecurity] {name} — PASSED (no policy violations).\n  path: {dllPath}{shaLine}");
                return;
            }

            Debug.LogError($"[HotUpdateSecurity] {name} — REJECTED: {result.Violations.Count} " +
                           $"policy violation(s) (see entries below).\n  path: {dllPath}{shaLine}");

            foreach (Violation v in result.Violations)
            {
                string rel = v.SourceFile != null ? ToProjectRelative(v.SourceFile) : null;

                // Null when the path is outside the project or the PDB gave nothing: the entry then
                // behaves exactly as it did before, hyperlink included where one is possible.
                UnityEngine.Object context = string.IsNullOrEmpty(rel)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(rel);

                Debug.LogError(FormatViolation(v, rel), context);
            }
        }

        private static string FormatViolation(Violation v, string rel)
        {
            string where = (rel != null && v.SourceLine.HasValue)
                ? $"<a href=\"{rel}\" line=\"{v.SourceLine.Value}\">{rel}:{v.SourceLine.Value}</a>"
                : v.Where;

            string text = $"[HotUpdateSecurity] [{v.Kind}] {v.Detail}\n  → {where}";

            if (rel == null || !v.SourceLine.HasValue)
                return text;

            // Shaped like a real frame — "Method () (at path:line)" — which is the form the Console
            // parses when it looks for somewhere to jump to. It is a fabrication, and says so.
            return text + $"\nHotUpdateSecurity.Violation () (at {rel}:{v.SourceLine.Value})";
        }

        private static string ToProjectRelative(string absolute)
        {
            try
            {
                string full = Path.GetFullPath(absolute).Replace('\\', '/');
                string root = ProjectRoot.Replace('\\', '/').TrimEnd('/') + "/";
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return full.Substring(root.Length);

                int idx = full.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                    return full.Substring(idx + 1);
            }
            catch { /* unusable path */ }
            return null;
        }
    }
}
