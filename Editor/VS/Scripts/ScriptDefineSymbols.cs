using UnityEditor;


using SPACS.Editor;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    [InitializeOnLoad]
    public class ScriptDefineSymbols
    {
        public const string VISUAL_SCRIPTING_SCRIPT_DEFINE_SYMBOL = "VIRTUADEMY_ENVIRONMENTS_VISUAL_SCRIPTING";
        static ScriptDefineSymbols()
        {
            // The symbol was REFLECTIS_CREATOR_KIT_WORLDS_* until 2026-09-21; a project updating the package
            // still carries the old one in its PlayerSettings, so it is retired here, once, before the new one is added.
            ScriptDefineSymbolsUtilities.RemoveScriptingDefineSymbolFromAllBuildTargetGroups(VISUAL_SCRIPTING_SCRIPT_DEFINE_SYMBOL.Replace("VIRTUADEMY_ENVIRONMENTS_", "REFLECTIS_CREATOR_KIT_WORLDS_"));
            ScriptDefineSymbolsUtilities.AddScriptingDefineSymbolToAllBuildTargetGroups(VISUAL_SCRIPTING_SCRIPT_DEFINE_SYMBOL);
        }
    }
}