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
            EditorApplication.delayCall += () => {
                AddToMenu();
            };
        }

        private static void AddToMenu() {
            Debug.Log("Adding VRCFury components to menu");
            var removeMenuItem = typeof(UnityEditor.Menu).GetMethod("RemoveMenuItem",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            removeMenuItem.Invoke(null, new object[] {"Component/UI/Toggle"});
            removeMenuItem.Invoke(null, new object[] {"Component/UI/Legacy/Dropdown"});
            removeMenuItem.Invoke(null, new object[] {"Component/UI/Toggle Group"});
            var addMenuItem = typeof(UnityEditor.Menu).GetMethod("AddMenuItem",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var feature in FeatureFinder.GetAllFeaturesForMenu()) {
                var editorInst = (FeatureBuilder) Activator.CreateInstance(feature.Value);
                var title = editorInst.GetEditorTitle();
                if (title != null) { 
                    addMenuItem.Invoke(null, new object[] {
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
