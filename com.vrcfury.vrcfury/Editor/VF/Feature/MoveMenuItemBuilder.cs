using System;
using System.Collections.Generic;
using System.Linq;
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
    internal class MoveMenuItemBuilder : FeatureBuilder<MoveMenuItem> {
        public override string GetEditorTitle() {
            return "Move or Rename Menu Item";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("This feature will move a menu item to another location. You can use slashes to make subfolders."));

            var fromPath = prop.FindPropertyRelative("fromPath");
            var toPath = prop.FindPropertyRelative("toPath");
            
            {
                var row = new VisualElement().Row();
                row.Add(VRCFuryEditorUtils.Prop(fromPath, "From Path").FlexGrow(1));
                void Apply(string path) {
                    fromPath.stringValue = path;
                    fromPath.serializedObject.ApplyModifiedProperties();
                }
                var selectButton = new Button(() => SelectButtonPress(false, Apply)) { text = "Select" };
                row.Add(selectButton);
                content.Add(row);
            }
            {
                var row = new VisualElement().Row();
                row.Add(VRCFuryEditorUtils.Prop(toPath, "To Path").FlexGrow(1));
                void Apply(string path) {
                    var fromItemName = fromPath.stringValue
                        .Replace("\\/", "SLAAASH")
                        .Split('/')
                        .Last()
                        .Replace("SLAAASH", "\\/");
                    if (path != "") path += "/";
                    path += fromItemName;
                    toPath.stringValue = path;
                    toPath.serializedObject.ApplyModifiedProperties();
                }
                var selectButton = new Button(() => SelectButtonPress(true, Apply)) { text = "Select" };
                row.Add(selectButton);
                content.Add(row);
            }
            
            return content;
        }
        
        private void SelectButtonPress(bool foldersOnly, Action<string> apply) {
            if (avatarObject == null) return;

            var controlPaths = new List<IList<string>>();
            MenuEstimator.Estimate(avatarObject).GetRaw().ForEachMenu(ForEachItem: (control, path) => {
                if (!foldersOnly || control.type == VRCExpressionsMenu.Control.ControlType.SubMenu) {
                    controlPaths.Add(path);
                }
                return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
            });
            string PathToString(IList<string> path) {
                return string.Join("/", path.Select(p => p.Replace("/", "\\/")));
            }
            void AddItem(VrcfSearchWindow.Group group, IList<string> prefix) {
                var children = controlPaths
                    .Where(path => path.Count == prefix.Count + 1)
                    .Where(path => prefix.Select((segment, i) => path[i] == segment).All(c => c))
                    .ToList();
                if (prefix.Count == 0) {
                    if (foldersOnly) {
                        group.Add("<Move to root folder>", "");
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

            window.Open(apply);
        }

        public override bool AvailableOnRootOnly() {
            return true;
        }
    }
}
