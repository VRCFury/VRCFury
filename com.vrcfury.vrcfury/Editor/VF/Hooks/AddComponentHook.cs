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
            if (MenuChanged != null) {
                bool inHandler = false;
                bool added = false;
                EventHandler onMenuChange = (src, args) => {
                    if (inHandler) return;
                    inHandler = true;
                    Debug.Log("On Menu Change");
                    RemoveJunkFromMenu();
                    if (!added) AddToMenu();
                    inHandler = false;
                };
                MenuChanged.AddEventHandler(null, onMenuChange);
            } else {
                EditorApplication.delayCall += () => {
                    RemoveJunkFromMenu();
                    AddToMenu();
                };
            }
        }

        private static readonly EventInfo MenuChanged = typeof(UnityEditor.Menu).GetEvent("menuChanged",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo RemoveMenuItem = typeof(UnityEditor.Menu).GetMethod("RemoveMenuItem",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo AddMenuItem = typeof(UnityEditor.Menu).GetMethod("AddMenuItem",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private static void RemoveJunkFromMenu() {
            Debug.Log("Removing VRCFury components to menu");
            if (RemoveMenuItem != null) {
                RemoveMenuItem.Invoke(null, new object[] { "Component/UI/Toggle" });
                RemoveMenuItem.Invoke(null, new object[] { "Component/UI/Legacy/Dropdown" });
                RemoveMenuItem.Invoke(null, new object[] { "Component/UI/Toggle Group" });
            }
        }
        private static void AddToMenu() {
            Debug.Log("Adding VRCFury components to menu");

            if (AddMenuItem != null) {
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
}
