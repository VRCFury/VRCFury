using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Utils;

namespace VF {
    internal static class UnityReflection {
        public class Recorder {
            public static readonly Type animStateType = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditorInternal.AnimationWindowState");
            public static readonly PropertyInfo selectionField = animStateType?.GetProperty("selection");
            public static readonly PropertyInfo gameObjectField = selectionField?.PropertyType.GetProperty("gameObject");
            public static readonly PropertyInfo animationClipField = animStateType?.GetProperty("activeAnimationClip");
#if ! UNITY_6000_0_OR_NEWER
            public static readonly MethodInfo startRecording = animStateType?.GetMethod("StartRecording");
#endif
            public static readonly PropertyInfo isRecordingProperty = animStateType?.GetProperty("recording", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly Type AnimationWindow = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.AnimationWindow");
        }

        public class PackageImport {
            public static readonly Type PackageImportWindow = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.PackageImport");
            public static readonly FieldInfo m_ImportPackageItems = PackageImportWindow?.GetField("m_ImportPackageItems", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo m_Tree = PackageImportWindow?.GetField("m_Tree", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo m_TreeViewState = PackageImportWindow?.GetField("m_TreeViewState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly Type ImportPackageItem = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.ImportPackageItem");
            public static readonly FieldInfo AssetPath = ImportPackageItem?.GetField("exportedAssetPath");
        }
        
        public static class Console {
            public static readonly Type ConsoleWindow = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.ConsoleWindow");
            public delegate void SetConsoleErrorPause_(bool enabled);
            public static readonly SetConsoleErrorPause_ SetConsoleErrorPause = ConsoleWindow?.GetMatchingDelegate<SetConsoleErrorPause_>("SetConsoleErrorPause");
            public static readonly MethodInfo SetFlag = ConsoleWindow?.GetMethod("SetFlag", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly Type ConsoleFlags = ConsoleWindow?.GetNestedType("ConsoleFlags", BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly object LogLevelError = ConsoleFlags != null ? Enum.Parse(ConsoleFlags, "LogLevelError") : null;
        }

        public static class Binding {
            public static readonly Type bindEventType = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.UIElements.SerializedObjectBindEvent");
            public static readonly MethodInfo registerCallback = typeof(VisualElement)
                .GetMethods()
                .FirstOrDefault(method => method.Name == nameof(VisualElement.RegisterCallback) && method.GetParameters().Length == 2);
        }

        public static class Props {
            public static readonly Type ScriptAttributeUtility = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.ScriptAttributeUtility");
            public delegate FieldInfo GetFieldInfoFromProperty_(SerializedProperty property, out System.Type type);
            public static readonly GetFieldInfoFromProperty_ GetFieldInfoFromProperty = ScriptAttributeUtility?.GetMatchingDelegate<GetFieldInfoFromProperty_>("GetFieldInfoFromProperty");
        }

#if UNITY_2022_1_OR_NEWER
        public static class Collapse {
            public static readonly Type SceneHierarchyWindow = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.SceneHierarchyWindow");
            public static readonly MethodInfo SetExpanded = SceneHierarchyWindow?.GetMethod("SetExpanded", false);
            public static readonly MethodInfo GetExpandedIDs = SceneHierarchyWindow?.GetMethod("GetExpandedIDs", false);
        }
#endif
        
        public static class PrefabStage {
            public static readonly Type PrefabStageUtility =
#if UNITY_2022_1_OR_NEWER
                ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.SceneManagement.PrefabStageUtility");
#else
                ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Experimental.SceneManagement.PrefabStageUtility");
#endif
            public static readonly Type PrefabStag =
#if UNITY_2022_1_OR_NEWER
                ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.SceneManagement.PrefabStage");
#else
                ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Experimental.SceneManagement.PrefabStage");
#endif
            public delegate object GetCurrentPrefabStage_();
            public static readonly GetCurrentPrefabStage_ GetCurrentPrefabStage = PrefabStageUtility?.GetMatchingDelegate<GetCurrentPrefabStage_>("GetCurrentPrefabStage");
            public delegate object OpenPrefab_(string prefabAssetPath, GameObject openedFromInstance);
            public static readonly OpenPrefab_ OpenPrefab = PrefabStageUtility?.GetMatchingDelegate<OpenPrefab_>("OpenPrefab");
            public static PropertyInfo autoSave = PrefabStag?.GetProperty("autoSave", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        public static bool IsReady(Type type) {
            foreach (var field in type.GetFields(BindingFlags.Static)) {
                if (field.GetValue(null) == null) return false;
            }
            return true;
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            var notReady = new List<string>();
            foreach (var cl in typeof(UnityReflection).GetNestedTypes()) { 
                foreach (var field in cl.GetFields(BindingFlags.Public | BindingFlags.Static)) {
                    if (field.GetValue(null) == null) {
                        notReady.Add(cl.Name + "." + field.Name);
                    }
                }
            }
            if (notReady.Any()) {
                Debug.LogError("VRCFury failed to find hook into some parts of Unity properly. Perhaps this version of Unity is not yet supported?\n" + notReady.Join('\n'));
            }
        }
    }
}
