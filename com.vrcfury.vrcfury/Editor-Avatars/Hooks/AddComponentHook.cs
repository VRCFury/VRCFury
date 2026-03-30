using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using VF.Feature;
using VF.Feature.Base;
using VF.Menu;
using VF.Model;
using VF.Model.Feature;
using VF.Updater;
using VF.Utils;

namespace VF.Hooks {
    internal static class AddComponentHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly MethodInfo MenuChangedAddHandler = typeof(UnityEditor.Menu)
                .VFEvent("menuChanged")
                ?.GetAddMethod(true);

            public delegate void RemoveMenuItem_(string path);
            public delegate void AddMenuItem_(string path, string shortcut, bool @checked, int priority, Action execute, Func<bool> validate);
            public delegate IEnumerable GetMenuItems_(string path, bool includeSeparators, bool localized);
            public static readonly RemoveMenuItem_ RemoveMenuItem = typeof(UnityEditor.Menu).GetMatchingDelegate<RemoveMenuItem_>("RemoveMenuItem");
            public static readonly AddMenuItem_ AddMenuItem = typeof(UnityEditor.Menu).GetMatchingDelegate<AddMenuItem_>("AddMenuItem");
            public static readonly GetMenuItems_ GetMenuItems = typeof(UnityEditor.Menu).GetMatchingDelegate<GetMenuItems_>("GetMenuItems");
        }

        private static bool addedThisFrame = false;
        
        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.delayCall += AddToMenu;
            if (Reflection.MenuChangedAddHandler != null) {
                Action onMenuChange = () => {
                    if (addedThisFrame) return;
                    EditorApplication.delayCall -= AddToMenu;
                    EditorApplication.delayCall += AddToMenu;
                };
                Reflection.MenuChangedAddHandler.Invoke(null, new object[] { onMenuChange });
            }
        } 

        private static void Add(string path, string shortcut, bool @checked, int priority, Action execute, Func<bool> validate) =>
            Reflection.AddMenuItem?.Invoke(path, shortcut, @checked, priority, execute, validate);
        private static void Remove(string path) => Reflection.RemoveMenuItem?.Invoke(path);
        private static IList<string> List(string path) {
            if (Reflection.GetMenuItems == null) return new string[] { };
            var l = Reflection.GetMenuItems(path, false, false);
            return l.OfType<object>()
                .Select(o => o.GetType().VFProperty("path")?.GetValue(o))
                .NotNull()
                .OfType<string>()
                .ToList();
        }

        private static void ResetAddedThisFrame() {
            addedThisFrame = false;
        }

        private static void AddToMenu() {
            //Debug.Log("Adding VRCFury components to menu");
            addedThisFrame = true;
            EditorApplication.delayCall -= ResetAddedThisFrame;
            EditorApplication.delayCall += ResetAddedThisFrame;

            if (Reflection.GetMenuItems == null) {
                Remove("Component/UI/Button");
                Remove("Component/UI/Slider");
                Remove("Component/UI/Toggle");
                Remove("Component/UI/Legacy/Dropdown");
                Remove("Component/UI/Toggle Group");
            } else {
                foreach (var path in List("Component/UI")) {
                    Remove(path);
                }
                foreach (var path in List("Component/UI Toolkit")) {
                    Remove(path);
                }
                foreach (var path in List("Component/Physics 2D")) {
                    Remove(path);
                }
            }

            if (UpdateMenuItem.ShouldShow()) {
                Add(
                    MenuItems.update,
                    "",
                    false,
                    MenuItems.updatePriority,
                    UpdateMenuItem.Upgrade,
                    null
                );
            }

            foreach (var menuItem in FeatureFinder.GetAllFeaturesForMenu<FeatureBuilder>()) {
                Add(
                    $"Component/VRCFury/{menuItem.title} (VRCFury)",
                    "",
                    false,
                    0,
                    () => {
                        var failureMsg = menuItem.builderType.GetCustomAttribute<FeatureFailWhenAddedAttribute>()?.Message;
                        if (failureMsg != null) {
                            DialogUtils.DisplayDialog($"Error adding {menuItem.title}", failureMsg, "Ok");
                            return;
                        }
                        if (menuItem.warning != null) {
                            DialogUtils.DisplayDialog("VRCFury Notice", menuItem.warning, "Ok");
                        }
                        foreach (var obj in Selection.gameObjects) {
                            if (obj == null) continue;
                            var modelInst = Activator.CreateInstance(menuItem.modelType) as FeatureModel;
                            if (modelInst == null) continue;
                            if (modelInst is ArmatureLink al) {
                                al.propBone = ArmatureLinkBuilder.GuessLinkFrom(obj);
                                ArmatureLinkBuilder.UpdateOnLinkFromChange(al, null, al.propBone);
                            }

                            var c = Undo.AddComponent<VRCFury>(obj);
                            var so = new SerializedObject(c);
                            so.FindProperty("content").managedReferenceValue = modelInst;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                    },
                    null
                );
            }
        }
    }
}

