using System;

namespace Virtuademy.SDK.Environments.Dialogs.Editor
{
    /// <summary>Kept so that code naming the constant still compiles; it no longer registers anything.</summary>
    [Obsolete("The package registers its scripting define symbols itself, in one pass (EnvironmentsScriptDefineSymbols). Use the symbol directly in an #if.")]
    public class ScriptDefineSymbols
    {
        public const string DIALOGS_SCRIPT_DEFINE_SYMBOL = "VIRTUADEMY_ENVIRONMENTS_DIALOGS";
    }
}
