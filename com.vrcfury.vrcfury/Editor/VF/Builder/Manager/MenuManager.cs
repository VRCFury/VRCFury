using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {

    public class MenuManager {
        private readonly VRCExpressionsMenu rootMenu;
        private readonly string tmpDir;
        private readonly Func<int> currentMenuSortPosition;
        private readonly Dictionary<VRCExpressionsMenu.Control, int> sortPositions
            = new Dictionary<VRCExpressionsMenu.Control, int>();

        public MenuManager(VRCExpressionsMenu menu, string tmpDir, Func<int> currentMenuSortPosition) {
            rootMenu = menu;
            this.tmpDir = tmpDir;
            this.currentMenuSortPosition = currentMenuSortPosition;
        }

        public VRCExpressionsMenu GetRaw() {
            return rootMenu;
        }

        private VRCExpressionsMenu.Control NewControl() {
            var control = new VRCExpressionsMenu.Control();
            sortPositions[control] = currentMenuSortPosition();
            return control;
        }

        public static IList<string> SplitPath(string path) {
            if (string.IsNullOrWhiteSpace(path))
                return new string[] { };
            return path
                .Replace("\\/", "REALSLASH")
                .Split('/')
                .Select(s => s.Replace("REALSLASH", "/"))
                .ToArray();
        }

        private VRCExpressionsMenu.Control NewMenuItem(string path) {
            var split = SplitPath(path);
            if (split.Count == 0) split = new[] { "" };
            var control = NewControl();
            control.name = split[split.Count-1];
            var submenu = GetSubmenu(Slice(split, split.Count-1));
            submenu.controls.Add(control);
            return control;
        }

        public bool SetIconGuid(string path, string guid) {
            var iconPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(iconPath)) return false;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (!icon) return false;
            return SetIcon(path, icon);
        }

        public bool SetIcon(string path, Texture2D icon) {
            GetSubmenuAndItem(path, false, out _, out _, out var controlName, out var parentMenu);
            if (!parentMenu) return false;

            var controls = parentMenu.controls.Where(c => c.name == controlName).ToList();
            if (controls.Count == 0) return false;

            foreach (var control in controls) {
                control.icon = icon;
            }
            return true;
        }

        public bool Move(string from, string to) {
            GetSubmenuAndItem(from, false, out var fromPath, out var fromPrefix, out var fromName, out var fromMenu);
            if (!fromMenu) return false;
            
            var fromControls = fromMenu.controls.Where(c => c.name == fromName).ToList();
            if (fromControls.Count == 0) return false;
            fromMenu.controls.RemoveAll(c => fromControls.Contains(c));

            if (string.IsNullOrWhiteSpace(to)) {
                // Just delete them!
                return true;
            }

            GetSubmenuAndItem(to, true, out var toPath, out var toPrefix, out var toName, out var toMenu);
            foreach (var control in fromControls) {
                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu) {
                    GetSubmenu(toPath, createFromControl: control);
                    MergeMenu(toPath, control.subMenu);
                } else {
                    control.name = toName;
                    var tmpMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                    tmpMenu.controls.Add(control);
                    MergeMenu(toPrefix, tmpMenu);
                }
            }
            return true;
        }

        private void GetSubmenuAndItem(
            string rawPath,
            bool create,
            out IList<string> path,
            out IList<string> prefix,
            out string name,
            out VRCExpressionsMenu prefixMenu
        ) {
            path = SplitPath(rawPath);
            if (path.Count > 0) {
                prefix = Slice(path, path.Count - 1);
                name = path[path.Count - 1];
            } else {
                prefix = new string[]{};
                name = "";
            }
            prefixMenu = GetSubmenu(prefix, createIfMissing: create);
        }

        /**
         * Gets the VRC menu for the path specified, recursively creating if it doesn't exist.
         * If createFromControl is set, we will use it as the basis if creating the folder control is needed.
         */
        private VRCExpressionsMenu GetSubmenu(
            IList<string> path,
            bool createIfMissing = true,
            VRCExpressionsMenu.Control createFromControl = null
        ) {
            var current = GetRaw();
            for (var i = 0; i < path.Count; i++) {
                var folderName = path[i];
                var dupIndex = folderName.IndexOf(".dup.");
                var offset = 0;
                if (dupIndex >= 0) {
                    offset = Int32.Parse(folderName.Substring(dupIndex + 5));
                    folderName = folderName.Substring(0, dupIndex);
                }
                var folderControls = current.controls.Where(
                    c => c.name == folderName && c.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    .ToArray();
                var folderControl = offset < folderControls.Length ? folderControls[offset] : null;
                if (folderControl == null) {
                    if (!createIfMissing) return null;
                    if (createFromControl != null && i == path.Count - 1) {
                        folderControl = CloneControl(createFromControl);
                    } else {
                        folderControl = NewControl();
                    }
                    folderControl.name = folderName;
                    folderControl.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
                    folderControl.subMenu = null;
                    current.controls.Add(folderControl);
                }
                var folder = folderControl.subMenu;
                if (folder == null) {
                    if (!createIfMissing) return null;
                    var newFolderPath = Slice(path, i + 1);
                    folder = CreateNewMenu(newFolderPath);
                    folderControl.subMenu = folder;
                }
                current = folder;
            }
            return current;
        }
        public void NewMenuButton(string path, VFAParam param = null, float value = 1, Texture2D icon = null) {
            var control = NewMenuItem(path);
            control.type = VRCExpressionsMenu.Control.ControlType.Button;
            control.parameter = new VRCExpressionsMenu.Control.Parameter {
                name = param != null ? param.Name() : ""
            };
            control.value = value;
            control.icon = icon;
        }
        public void NewMenuToggle(string path, VFAParam param, float value = 1, Texture2D icon = null) {
            var control = NewMenuItem(path);
            control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            control.parameter = new VRCExpressionsMenu.Control.Parameter {
                name = param.Name()
            };
            control.value = value;
            control.icon = icon;
        }
        public void NewMenuSlider(string path, VFAFloat param, Texture2D icon = null) {
            var control = NewMenuItem(path);
            control.type = VRCExpressionsMenu.Control.ControlType.RadialPuppet;
            var menuParam = new VRCExpressionsMenu.Control.Parameter {
                name = param.Name()
            };
            control.subParameters = new[]{menuParam};
            control.icon = icon;
        }
        public void NewMenuPuppet(string path, VFAFloat x, VFAFloat y, Texture2D icon = null) {
            var control = NewMenuItem(path);
            control.type = VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet;
            var menuParamX = new VRCExpressionsMenu.Control.Parameter();
            menuParamX.name = (x != null) ? x.Name() : "";
            var menuParamY = new VRCExpressionsMenu.Control.Parameter();
            menuParamY.name = (y != null) ? y.Name() : "";
            control.subParameters = new[]{menuParamX, menuParamY};
            control.icon = icon;
        }

        private VRCExpressionsMenu CreateNewMenu(IList<string> path) {
            var cleanPath = path.Select(CleanTitleForFilename);
            var newMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            newMenu.name = string.Join(" » ", cleanPath);
            AssetDatabase.AddObjectToAsset(newMenu, rootMenu);
            return newMenu;
        }
        private static string CleanTitleForFilename(string str) {
            // strip html tags
            str = Regex.Replace(str, "<.*?>", string.Empty);
            // remove after newline
            str = Regex.Replace(str, "\n.*", string.Empty);
            // clean up extra spaces
            str = Regex.Replace(str, " +", " ");
            return str.Trim();
        }

        public void MergeMenu(VRCExpressionsMenu from) {
            MergeMenu(new string[]{}, from);
        }

        public void MergeMenu(
            IList<string> prefix,
            VRCExpressionsMenu from,
            Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> seen = null
        ) {
            var to = GetSubmenu(prefix);
            if (seen == null) {
                seen = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();
            }
            seen.Add(from, to);

            var submenuCount = new Dictionary<string,int>();
            int GetNextSubmenuDupId(string name) {
                return submenuCount[name] = submenuCount.TryGetValue(name, out var value) ? value + 1 : 0;
            }
            foreach (var fromControl in from.controls) {
                if (fromControl.type == VRCExpressionsMenu.Control.ControlType.SubMenu && fromControl.subMenu != null) {
                    // Properly handle loops
                    if (seen.ContainsKey(fromControl.subMenu)) {
                        var toControl = CloneControl(fromControl);
                        toControl.subMenu = seen[fromControl.subMenu];
                        to.controls.Add(toControl);
                    } else {
                        var submenuDupId = GetNextSubmenuDupId(fromControl.name);
                        var prefix2 = new List<string>(prefix);
                        prefix2.Add(fromControl.name + (submenuDupId > 0 ? (".dup." + submenuDupId) : ""));
                        GetSubmenu(prefix2.ToArray(), createFromControl: fromControl);
                        MergeMenu(prefix2.ToArray(), fromControl.subMenu, seen);
                    }
                } else {
                    to.controls.Add(CloneControl(fromControl));
                }
            }
        }

        private VRCExpressionsMenu.Control CloneControl(VRCExpressionsMenu.Control from) {
            var control = NewControl();
            control.name = from.name;
            control.icon = from.icon;
            control.type = from.type;
            control.parameter = CloneControlParam(from.parameter);
            control.value = from.value;
            control.style = from.style;
            control.subMenu = from.subMenu;
            control.labels = from.labels;
            control.subParameters = from.subParameters == null
                ? null
                : new List<VRCExpressionsMenu.Control.Parameter>(from.subParameters)
                    .Select(p => CloneControlParam(p))
                    .ToArray();
            return control;
        }
        private VRCExpressionsMenu.Control.Parameter CloneControlParam(VRCExpressionsMenu.Control.Parameter from) {
            if (from == null) return null;
            return new VRCExpressionsMenu.Control.Parameter {
                name = from.name
            };
        }

        public static IList<string> Slice(IEnumerable<string> arr, int count) {
            return new ArraySegment<string>(arr.ToArray(), 0, count).ToArray();
        }

        public void SortMenu() {
            rootMenu.ForEachMenu((menu, path) => {
                menu.controls.Sort((a, b) => {
                    sortPositions.TryGetValue(a, out var aPos);
                    sortPositions.TryGetValue(b, out var bPos);
                    return aPos - bPos;
                });
            });
        }

    }

}
