using System;
using System.IO;
using System.Linq;

using UnityEditor;

using UnityEngine;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// A scene's default thumbnail and localization file (<see cref="SceneListScriptableObject.SceneConfiguration.DefaultThumbnail"/>,
    /// <see cref="SceneListScriptableObject.SceneConfiguration.DefaultLocalization"/>) as the two windows set them: which asset is accepted
    /// and why another is refused, and the write, which goes through a <see cref="SerializedObject"/> so that it is one undo step.
    /// <see cref="Changed"/> is raised after every write from any window, so the scene rows of Publish Environments and the defaults strip of
    /// Environment Details show the same.
    /// </summary>
    internal static class SceneDefaults
    {
        public const string NotImageMessage = "Use a PNG or JPEG image.";
        public const string NotLocalizationMessage = "Use a .csv file.";
        public const string NotInAssetsMessage = "Use a file of the project's Assets folder.";

        /// <summary>Raised on the main thread after a default was set or cleared.</summary>
        public static event Action Changed;

        public static void NotifyChanged() => Changed?.Invoke();

        /// <summary>The entry of the scene list for a scene's environment name; null when the list has none.</summary>
        public static SceneListScriptableObject.SceneConfiguration ConfigOf(SceneListScriptableObject list, string sceneKey) =>
            list?.SceneConfigurations?.FirstOrDefault(c => c.Scene != null && string.Equals(c.SceneNameFiltered, sceneKey, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Picks the asset a drop or a field offers for <paramref name="kind"/>: a PNG or JPEG image of the project's <c>Assets/</c> folder, or a
        /// <c>.csv</c> there. Null with the reason in <paramref name="refusal"/> when none qualifies; null with no reason for no object at all.
        /// </summary>
        public static UnityEngine.Object Accept(FileKind kind, UnityEngine.Object[] offered, out string refusal)
        {
            refusal = null;
            if (offered == null || offered.Length == 0)
                return null;

            string wrongKind = kind == FileKind.Thumbnail ? NotImageMessage : NotLocalizationMessage;
            foreach (UnityEngine.Object candidate in offered)
            {
                if (candidate == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(candidate);
                if (string.IsNullOrEmpty(path))
                    continue;

                string extension = Path.GetExtension(path).ToLowerInvariant();
                bool right = kind == FileKind.Thumbnail
                    ? extension == ".png" || extension == ".jpg" || extension == ".jpeg"
                    : extension == ".csv";
                if (!right)
                {
                    refusal = wrongKind;
                    continue;
                }

                if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    refusal = NotInAssetsMessage;
                    continue;
                }

                UnityEngine.Object asset = kind == FileKind.Thumbnail
                    ? AssetDatabase.LoadAssetAtPath<Texture2D>(path)
                    : AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset != null)
                {
                    refusal = null;
                    return asset;
                }

                refusal = wrongKind;
            }

            refusal ??= wrongKind;
            return null;
        }

        /// <summary>Sets (or, with null, clears) one default of a scene in the scene list, as one undo step, and saves the list.</summary>
        public static bool Set(SceneListScriptableObject list, string sceneKey, FileKind kind, UnityEngine.Object value)
        {
            SceneListScriptableObject.SceneConfiguration config = ConfigOf(list, sceneKey);
            if (config == null)
                return false;

            SerializedObject serialized = new(list);
            SerializedProperty entries = serialized.FindProperty("sceneConfigurations");
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("scene").objectReferenceValue != config.Scene)
                    continue;

                entry.FindPropertyRelative(kind == FileKind.Thumbnail ? "defaultThumbnail" : "defaultLocalization").objectReferenceValue = value;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(list);
                AssetDatabase.SaveAssetIfDirty(list);
                PublishedFiles.Invalidate();
                NotifyChanged();
                return true;
            }

            return false;
        }
    }
}
