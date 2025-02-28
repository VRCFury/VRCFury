using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Utils {

    internal class MenuManager {
        private readonly VRCExpressionsMenu rootMenu;
        private readonly Func<int> currentMenuSortPosition;
        private readonly Dictionary<VRCExpressionsMenu.Control, int> sortPositions
            = new Dictionary<VRCExpressionsMenu.Control, int>();
        private int overrideMenuSortPosition = -1;

        public MenuManager(VRCExpressionsMenu menu, Func<int> currentMenuSortPosition) {
            rootMenu = menu;
            this.currentMenuSortPosition = currentMenuSortPosition;
        }

        public void OverrideSortPosition(int serviceId, Action with) {
            this.overrideMenuSortPosition = serviceId;
            with();
            this.overrideMenuSortPosition = -1;
        }

        public VRCExpressionsMenu GetRaw() {
            return rootMenu;
        }

        private VRCExpressionsMenu.Control NewControl() {
            var control = new VRCExpressionsMenu.Control();
            sortPositions[control] = overrideMenuSortPosition >= 0 ? overrideMenuSortPosition : currentMenuSortPosition();
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

        public bool SetIcon(string path, Texture2D icon) {
            GetSubmenuAndItem(path, false, out _, out _, out var controlName, out var parentMenu);
            if (!parentMenu) return false;

            var controls = FindControlsWithName(parentMenu, controlName);
            if (controls.Length == 0) return false;

            var changed = false;
            foreach (var control in controls) {
                if (control.icon != icon) {
                    changed = true;
                    control.icon = icon;
                }
            }
            return changed;
        }

        public bool Move(string from, string to) {
            GetSubmenuAndItem(from, false, out var fromPath, out var fromPrefix, out var fromName, out var fromMenu);
            if (!fromMenu) return false;
            
            var fromControls = FindControlsWithName(fromMenu, fromName);
            if (fromControls.Length == 0) return false;
            fromMenu.controls.RemoveAll(c => fromControls.Contains(c));

            if (string.IsNullOrWhiteSpace(to)) {
                // Just delete them!
                return true;
            }

            GetSubmenuAndItem(to, true, out var toPath, out var toPrefix, out var toName, out var toMenu);
            foreach (var control in fromControls) {
                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu != null) {
                    GetSubmenu(toPath, createFromControl: control);
                    MergeMenu(toPath, control.subMenu);
                } else {
                    control.name = toName;
                    var tmpMenu = VrcfObjectFactory.Create<VRCExpressionsMenu>();
                    tmpMenu.controls.Add(control);
                    MergeMenu(toPrefix, tmpMenu);
                }
            }
            return true;
        }
        
        public bool Reorder(string path, int position) {
            GetSubmenuAndItem(path, false, out var splitPath, out var splitPrefix, out var fromName, out var fromMenu);
            if (!fromMenu) return false;
            
            var controls = FindControlsWithName(fromMenu, fromName);
            if (controls.Length == 0) return false;
            fromMenu.controls.RemoveAll(c => controls.Contains(c));

            if (position < 0) position = fromMenu.controls.Count + position;
            position = VrcfMath.Clamp(position, 0, fromMenu.controls.Count);

            fromMenu.controls.InsertRange(position, controls);
            return true;
        }

        public VRCExpressionsMenu.Control GetMenuItem(string path) {
            var split = SplitPath(path);
            if (split.Count == 0) split = new[] { "" };
            var name = split[split.Count-1];
            var submenu = GetSubmenu(Slice(split, split.Count-1));
            foreach (var control in submenu.controls) {
                if (control.name == name) {
                    return control;
                }
            }
            return null;
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

        private static VRCExpressionsMenu.Control[] FindControlsWithName(
            VRCExpressionsMenu menu,
            string name,
            Func<VRCExpressionsMenu.Control,bool> predicate = null
        ) {
            string Normalize(string a) => a
                .ToLower()
                .RemoveHtmlTags()
                .Replace("\n", " ")
                .Replace("\\n", " ")
                .NormalizeSpaces();
            string[] GetSlugs(string a) => Regex.Replace(a, @"<.*?>", "`")
                .Split('`')
                .Select(slug => Normalize(slug))
                .Where(slug => !string.IsNullOrWhiteSpace(slug))
                .ToArray();
            var nameMatchMethods = new Func<string,string,bool>[] {
                (a,b) => a == b,
                (a,b) => !string.IsNullOrWhiteSpace(Normalize(a)) && Normalize(a) == Normalize(b),
                (a,b) => !string.IsNullOrWhiteSpace(Normalize(a)) && GetSlugs(b).Contains(Normalize(a)),
            };
            foreach (var method in nameMatchMethods) {
                bool Matches(VRCExpressionsMenu.Control other) {
                    if (predicate != null && !predicate(other)) return false;
                    return method(name, other.name);
                }
                var matches = menu.controls.Where(Matches).ToArray();
                if (matches.Length > 0) return matches;
            }
            return new VRCExpressionsMenu.Control[] { };
        }

        public VRCExpressionsMenu GetSubmenu(string path) {
            return GetSubmenu(SplitPath(path));
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
                var folderControls = FindControlsWithName(current, folderName, c => c.type == VRCExpressionsMenu.Control.ControlType.SubMenu);
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
                name = param ?? ""
            };
            control.value = value;
            control.icon = icon;
        }
        public void NewMenuToggle(string path, VFAParam param, float value = 1, Texture2D icon = null) {
            var control = NewMenuItem(path);
            control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            control.parameter = new VRCExpressionsMenu.Control.Parameter {
                name = param
            };
            control.value = value;
            control.icon = icon;
        }
        public void NewMenuSlider(string path, VFAFloat param, Texture2D icon = null) {
            var control = NewMenuItem(path);
            control.type = VRCExpressionsMenu.Control.ControlType.RadialPuppet;
            var menuParam = new VRCExpressionsMenu.Control.Parameter {
                name = param
            };
            control.subParameters = new[]{menuParam};
            control.icon = icon;
        }
        public void NewMenuPuppet(string path, VFAFloat x, VFAFloat y, Texture2D icon = null) {
            var control = NewMenuItem(path);
            control.type = VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet;
            var menuParamX = new VRCExpressionsMenu.Control.Parameter();
            menuParamX.name = x ?? "";
            var menuParamY = new VRCExpressionsMenu.Control.Parameter();
            menuParamY.name = y ?? "";
            control.subParameters = new[]{menuParamX, menuParamY};
            control.icon = icon;
        }

        private VRCExpressionsMenu CreateNewMenu(IList<string> path) {
            var cleanPath = path.Select(CleanTitleForFilename);
            var newMenu = VrcfObjectFactory.Create<VRCExpressionsMenu>();
            newMenu.name = cleanPath.Join(" » ");
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
                    if (seen.TryGetValue(fromControl.subMenu, out var value)) {
                        var toControl = CloneControl(fromControl);
                        toControl.subMenu = value;
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
            UnitySerializationUtils.CloneSerializable(from, control);
            return control;
        }

        public static IList<string> Slice(IEnumerable<string> arr, int count) {
            return new ArraySegment<string>(arr.ToArray(), 0, count).ToArray();
        }

        public void SortMenu() {
            rootMenu.ForEachMenu((menu, path) => {
                // Do not use .Sort, because it's unstable and will not maintain order if you return 0
                // .OrderBy is a stable sort.
                menu.controls = menu.controls.OrderBy(a => a, Comparer<VRCExpressionsMenu.Control>.Create((a,b) => {
                    sortPositions.TryGetValue(a, out var aPos);
                    sortPositions.TryGetValue(b, out var bPos);
                    return aPos - bPos;
                })).ToList();
            });
        }

    }

}
