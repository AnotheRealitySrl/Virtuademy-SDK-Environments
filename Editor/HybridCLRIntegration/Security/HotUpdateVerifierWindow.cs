using System;
using System.IO;

using UnityEditor;

using UnityEngine;


namespace Virtuademy.SDK.Environments.HybridCLR.Editor
{
    /// <summary>
    /// Author-facing pre-check tool. Resolves the project's hot-update DLL automatically,
    /// DOWNLOADS the whitelist policy (shared with the backend) and then runs the check
    /// locally on the IL, listing each violation with its source file:line (from the PDB).
    /// Results are also written to the Unity Console. The authoritative check is still the
    /// backend; that server call is a stub for now.
    /// </summary>
    public sealed class HotUpdateVerifierWindow : EditorWindow
    {
        private const string EditorTokenPrefKey = "Virtuademy_EditorLogin_Token";

        private enum Source { Auto, StandaloneWindows64, Android, WebGL, Browse }

        private Source _source = Source.Auto;
        private string _assemblyName;
        private string _browsePath = string.Empty;
        private int _worldId = 1;
        private Vector2 _scroll;

        private VerificationResult _result;
        private HotUpdatePolicyFetcher.SourceKind _policySource;
        private bool _busy;
        private string _status = string.Empty;
        private bool _initialRunDone;

        //[MenuItem("Virtuademy/Security/Verify script DLL…")]
        public static void Open() => GetWindow<HotUpdateVerifierWindow>("Script DLL Verifier");

        private void OnEnable() => _initialRunDone = false;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("HybridCLR Script DLL — local pre-check", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Downloads the shared whitelist policy, then checks the auto-resolved HotUpdate " +
                "DLL locally and points at the offending lines. Results are logged to the Console. " +
                "The authoritative server check is a stub for now.", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _source = (Source)EditorGUILayout.EnumPopup("Source", _source);
            if (EditorGUI.EndChangeCheck())
                _result = null;

            if (_source == Source.Browse)
            {
                using EditorGUILayout.HorizontalScope _ = new();
                _browsePath = EditorGUILayout.TextField("DLL path", _browsePath);
                if (GUILayout.Button("Browse…", GUILayout.Width(80)))
                {
                    string picked = EditorUtility.OpenFilePanel("Select DLL", Application.dataPath, "dll,bytes");
                    if (!string.IsNullOrEmpty(picked)) { _browsePath = picked; _result = null; }
                }
            }

            string path = ResolvePath();
            bool exists = !string.IsNullOrEmpty(path) && File.Exists(path);

            EditorGUILayout.LabelField("Resolved", string.IsNullOrEmpty(path) ? "<not found>" : path, EditorStyles.miniLabel);
            if (!exists && _source != Source.Browse)
            {
                EditorGUILayout.HelpBox(
                    _source == Source.Auto
                        ? "Compiled HotUpdate assembly not found under Library/ScriptAssemblies. " +
                          "Let Unity compile the scripts (or use a per-target/Browse source)."
                        : "Per-target DLL not found. Build it first (HybridCLR › CompileDll) or use 'Auto'.",
                    MessageType.Warning);
            }

            _worldId = EditorGUILayout.IntField("World id (wid)", _worldId);

            using (new EditorGUI.DisabledScope(!exists || _busy))
            {
                if (GUILayout.Button(_busy ? "Working…" : "Check", GUILayout.Height(28)))
                    RunCheck(path);
            }

            if (!_initialRunDone && exists && !_busy && _result == null)
            {
                _initialRunDone = true;
                RunCheck(path);
            }

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, _result == null && !_busy ? MessageType.Error : MessageType.None);

            if (_result == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Policy source: {_policySource}", EditorStyles.miniLabel);

            if (_result.Passed)
            {
                EditorGUILayout.HelpBox("Local check PASSED — no policy violations found.", MessageType.Info);
                DrawServerVerificationStub();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Local check REJECTED — {_result.Violations.Count} violation(s). " +
                    "Fix these and re-check. The server call is NOT made.", MessageType.Error);

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                foreach (Violation v in _result.Violations)
                    EditorGUILayout.HelpBox($"{v.Where}\n[{v.Kind}] {v.Detail}", MessageType.Error);
                EditorGUILayout.EndScrollView();

                EditorGUILayout.HelpBox(
                    "file:line appears only when the DLL has debug symbols (PDB). 'Auto' targets " +
                    "Library/ScriptAssemblies, which always has them; per-target outputs may not.",
                    MessageType.None);
            }
        }

        private string ResolvePath()
        {
            switch (_source)
            {
                case Source.Auto:
                    return HotUpdateDllLocator.ResolveDefaultDllPath(out _assemblyName);
                case Source.Browse:
                    return _browsePath;
                default:
                    return HotUpdateDllLocator.ResolveTargetDllPath(_source.ToString(), out _assemblyName);
            }
        }

        // Fire-and-forget: fetch the shared policy, then verify + log. Repaints on each step.
        private async void RunCheck(string path)
        {
            if (_busy)
                return;

            _busy = true;
            _result = null;
            _status = "Downloading policy…";
            Repaint();

            try
            {
                HotUpdatePolicyFetcher.FetchResult fetch = await HotUpdatePolicyFetcher.FetchAsync();
                _policySource = fetch.Source;

                if (!fetch.Ok)
                {
                    _status = "Policy unavailable — check blocked (fail-closed).\n" + fetch.Error;
                    return;
                }

                _status = fetch.Source == HotUpdatePolicyFetcher.SourceKind.Cached
                    ? "Using CACHED policy (network fetch failed)."
                    : string.Empty;

                _result = HotUpdateDllLocator.VerifyAndLog(path, fetch.Policy);
            }
            catch (Exception e)
            {
                _status = "Check failed: " + e.Message;
                _result = null;
                Debug.LogException(e);
            }
            finally
            {
                _busy = false;
                Repaint();
            }
        }

        private void DrawServerVerificationStub()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Server verification (authoritative)", EditorStyles.boldLabel);

            string token = EditorPrefs.GetString(EditorTokenPrefKey, string.Empty);
            bool hasToken = !string.IsNullOrEmpty(token);

            EditorGUILayout.HelpBox(
                hasToken
                    ? "Editor login token found. Server verification is not wired yet (stub)."
                    : "No editor login token found — log in via 'Virtuademy / Available Tenants' first.",
                hasToken ? MessageType.Info : MessageType.Warning);

            using (new EditorGUI.DisabledScope(true))
                GUILayout.Button("Verify on server (coming soon)", GUILayout.Height(24));

            // ── TODO: chiamata API ────────────────────────────────────────────────────────
            // POST {apiUrl}/worlds/{_worldId}/environments/scripts/verify with Authorization:
            // Bearer {token}, multipart file "dll" = File.ReadAllBytes(path). Handle:
            //   200 → approved (sha256); 422 → server violations; 400 → bad request;
            //   401/403 → re-login / missing Owner|EnvironmentManager grant on the world.
            // ──────────────────────────────────────────────────────────────────────────────
        }
    }
}
