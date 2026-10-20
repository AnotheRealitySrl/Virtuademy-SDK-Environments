using System;

/// <summary>Kept so that code naming the constant still compiles; it no longer registers anything.</summary>
[Obsolete("The package registers its scripting define symbols itself, in one pass (EnvironmentsScriptDefineSymbols). Use the symbol directly in an #if.")]
public class EnvironmentVariables
{
    public const string TASKS_ENV_VARIABLE = "VIRTUADEMY_ENVIRONMENTS_TASKS";
}
