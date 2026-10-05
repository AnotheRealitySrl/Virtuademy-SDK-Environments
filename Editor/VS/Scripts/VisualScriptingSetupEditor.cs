#if UNITY_EDITOR

using System;
using System.Collections.Generic;

using Unity.VisualScripting;

using UnityEditor;


using SPACS.Editor;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    public static class VisualScriptingSetupEditor
    {
        //[MenuItem("Virtuademy/Setup/Reset Visual Scripting nodes")]
        public static void ResetNodes()
        {
            while (BoltCore.Configuration.typeOptions.Count > 0)
            {
                BoltCore.Configuration.typeOptions.RemoveAt(0);
            }

            PluginConfigurationItemMetadata _typeOptionsMetadata = BoltCore.Configuration.GetMetadata(nameof(BoltCore.Configuration.typeOptions));

            if (_typeOptionsMetadata.defaultValue is List<Type> defaultTypes)
            {
                foreach (Type type in defaultTypes)
                {
                    BoltCore.Configuration.typeOptions.Add(type);
                }
            }
        }

        [MenuItem("Virtuademy/Setup/Visual Scripting nodes")]
        public static void Setup()
        {
            if (!VSUsageUtility.isVisualScriptingUsed)
            {
                VSUsageUtility.isVisualScriptingUsed = true;
            }

            ResetNodes();

            foreach (var type in GetCustomTypes())
            {
                if (type == null)
                {
                    //Debug.LogError("Found Null type!");
                    continue;
                }
                if (!BoltCore.Configuration.typeOptions.Contains(type))
                {
                    if (type.Namespace != null && !BoltCore.Configuration.assemblyOptions.Exists(x => x.name == type.Namespace))
                    {
                        LooseAssemblyName looseAssemblyName = new LooseAssemblyName(type.Namespace);
                        BoltCore.Configuration.assemblyOptions.Add(looseAssemblyName);
                    }

                    BoltCore.Configuration.typeOptions.Add(type);
                    //Debug.Log($"Add {type.FullName} to Visual Scripting type options.");
                }
            }
            var typeOptionsMetadata = BoltCore.Configuration.GetMetadata(nameof(BoltCore.Configuration.typeOptions));
            typeOptionsMetadata.Save();
            BoltCore.Configuration.SaveProjectSettingsAsset(true);
            Codebase.UpdateSettings();
            UnitBase.Rebuild();
        }

        private static IEnumerable<Type> GetCustomTypes()
        {
            UnityEditor.AssetDatabase.Refresh();
            List<VisualScriptingCustomTypeCollector> customTypeCollectors = AssetDatabaseExtension.SearchAssetByType<VisualScriptingCustomTypeCollector>();
            List<Type> customTypes = new();
            foreach (var customTypeCollector in customTypeCollectors)
            {
                customTypes.AddRange(customTypeCollector.GetCustomTypes());
            }
            return customTypes;
        }


    }


}
#endif