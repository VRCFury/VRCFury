using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Feature {
    [FeatureTitle("Move or Rename Menu Item")]
    [FeatureRootOnly]
    internal class MoveMenuItemBuilder : FeatureBuilder<MoveMenuItem> {
        
        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop, VFGameObject avatarObject) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("This feature will move a menu item to another location. You can use slashes to make subfolders."));

            var fromPath = prop.FindPropertyRelative("fromPath");
            var toPath = prop.FindPropertyRelative("toPath");
            
            content.Add(SelectButton(avatarObject, null, false, fromPath, label: "From Path"));
            content.Add(SelectButton(avatarObject, null, true, toPath, label: "To Path", append: () => {
                return GetLastMenuSlug(fromPath.stringValue, "New Thing");
            }));

            return content;
        }

        public static string GetLastMenuSlug(string path, string def) {
            var last = path
                .Replace("\\/", "SLAAASH")
                .Split('/')
                .Last()
                .Replace("SLAAASH", "\\/");
            if (last == "") return def;
            return last;
        }

        public static VisualElement SelectButton(
            [CanBeNull] VFGameObject avatarObject,
            [CanBeNull] VFGameObject componentObject,
            bool foldersOnly,
            SerializedProperty prop,
            string label = "Menu Path",
            Func<string> append = null,
            string selectLabel = "Select",
            string tooltip = null,
            bool immediate = false,
            Vector2? pos = null
        ) {
            void Apply(string path) {
                if (append != null) {
                    if (path != "") path += "/";
                    path += append();
                }
                if (componentObject != null) {
                    var folderPrefix = MenuManager.PrependFolders("", componentObject);
                    if (path.StartsWith(folderPrefix)) path = path.Replace(folderPrefix,"");
                    if (path.StartsWith("/")) path = path.Substring(1);
                }
                prop.stringValue = path;
                prop.serializedObject.ApplyModifiedProperties();
            }
            
            void OnClick() {
                if (avatarObject == null) return;

                var controlPaths = new List<IList<string>>();
                MenuEstimator.Estimate(avatarObject).GetRaw().ForEachMenu(ForEachItem: (control, path) => {
                    if (!foldersOnly || control.type == VRCExpressionsMenu.Control.ControlType.SubMenu) {
                        controlPaths.Add(path);
                    }

                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                });

                string PathToString(IList<string> path) {
                    return path.Select(p => p.Replace("/", "\\/")).Join('/');
                }

                void AddItem(VrcfSearchWindow.Group group, IList<string> prefix) {
                    var children = controlPaths
                        .Where(path => path.Count == prefix.Count + 1)
                        .Where(path => prefix.Select((segment, i) => path[i] == segment).All(c => c))
                        .ToList();
                    if (prefix.Count == 0) {
                        if (foldersOnly) {
                            group.Add("<Select this folder>", "");
                        }

                        foreach (var child in children) {
                            AddItem(group, child);
                        }
                    } else {
                        if (children.Count > 0) {
                            var subGroup = group.AddGroup(prefix.Last());
                            subGroup.Add("<Select this folder>", PathToString(prefix));
                            foreach (var child in children) {
                                AddItem(subGroup, child);
                            }
                        } else {
                            group.Add(prefix.Last(), PathToString(prefix));
                        }
                    }
                }

                var window = new VrcfSearchWindow("Avatar Menu Items");
                AddItem(window.GetMainGroup(), new string[] { });

                window.Open(Apply, pos);
            }

            if (immediate) {
                OnClick();
                return null;
            }

            var row = new VisualElement().Row();
            row.Add(VRCFuryEditorUtils.Prop(prop, label, tooltip: tooltip).FlexGrow(1));
            row.Add(new Button(OnClick) { text = selectLabel });
            return row;
        }
    }
}
