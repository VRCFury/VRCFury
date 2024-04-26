using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Feature;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;

namespace VF.Hooks {
    public static class AddComponentHook {
        [InitializeOnLoadMethod]
        public static void Init() {
            if (MenuChangedAddHandler != null) {
                bool inHandler = false;
                bool added = false;
                Action onMenuChange = () => {
                    if (inHandler) return;
                    inHandler = true;
                    Debug.Log("On Menu Change");
                    RemoveJunkFromMenu();
                    if (!added) {
                        added = true;
                        AddToMenu();
                    }
                    inHandler = false;
                };
                MenuChangedAddHandler.Invoke(null, new object[] { onMenuChange });
            } else {
                EditorApplication.delayCall += () => {
                    RemoveJunkFromMenu();
                    AddToMenu();
                };
            }
        }

        private static readonly MethodInfo MenuChangedAddHandler = typeof(UnityEditor.Menu).GetEvent("menuChanged",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetAddMethod(true);
        private static readonly MethodInfo RemoveMenuItem = typeof(UnityEditor.Menu).GetMethod("RemoveMenuItem",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo AddMenuItem = typeof(UnityEditor.Menu).GetMethod("AddMenuItem",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        
        private static void RemoveJunkFromMenu() {
            if (RemoveMenuItem == null) {
                Debug.LogError("Menu.RemoveMenuItem is missing");
                return;
            }

            RemoveMenuItem.Invoke(null, new object[] { "Component/UI/Toggle" });
            RemoveMenuItem.Invoke(null, new object[] { "Component/UI/Legacy/Dropdown" });
            RemoveMenuItem.Invoke(null, new object[] { "Component/UI/Toggle Group" });
        }

        private static void AddToMenu() {
            if (AddMenuItem == null) {
                Debug.LogError("Menu.AddMenuItem is missing");
                return;
            }
            
            Debug.Log("Adding VRCFury components to menu");

            foreach (var feature in FeatureFinder.GetAllFeaturesForMenu()) {
                var editorInst = (FeatureBuilder)Activator.CreateInstance(feature.Value);
                var title = editorInst.GetEditorTitle();
                if (title != null) {
                    AddMenuItem.Invoke(null, new object[] {
                        "Component/VRCFury/VRCFury | " + title, // name
                        "", // shortcut
                        false, // checked
                        (int)0, // priority
                        (Action)(() => {
                            foreach (var obj in Selection.gameObjects) {
                                if (obj == null) continue;
                                var modelInst = Activator.CreateInstance(feature.Key) as FeatureModel;
                                if (modelInst == null) continue;
                                if (modelInst is ArmatureLink al) {
                                    al.propBone = ArmatureLinkBuilder.GuessLinkFrom(obj);
                                }

                                var c = Undo.AddComponent<VRCFury>(obj);
                                var so = new SerializedObject(c);
                                so.FindProperty("content").managedReferenceValue = modelInst;
                                so.ApplyModifiedPropertiesWithoutUndo();
                            }
                        }),
                        null
                    });
                }
            }
        }
    }
}
