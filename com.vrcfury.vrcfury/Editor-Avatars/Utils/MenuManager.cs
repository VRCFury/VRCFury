using System;
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
            var previous = overrideMenuSortPosition;
            overrideMenuSortPosition = serviceId;
            try {
                with();
            } finally {
                overrideMenuSortPosition = previous;
            }
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
            return Regex.Split(path, @"(?<!\\)/")
                .Select(s => s.Replace("\\/", "/"))
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
            var (parentMenu, controlName) = GetSubmenuAndItem(path, false);
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
            var (fromMenu, fromName) = GetSubmenuAndItem(from, false);
            if (!fromMenu) return false;
            
            var fromControls = FindControlsWithName(fromMenu, fromName);
            if (fromControls.Length == 0) return false;
            if (from == to) return true;
            fromMenu.controls.RemoveAll(c => fromControls.Contains(c));

            if (string.IsNullOrWhiteSpace(to)) {
                // Just delete them!
                return true;
            }

            var (toMenu, toName) = GetSubmenuAndItem(to, true);
            foreach (var control in fromControls) control.name = toName;
            MergeControls(toMenu, fromControls, preserveSubmenuLinks: true);
            return true;
        }
        
        public bool Reorder(string path, int position) {
            var (fromMenu, fromName) = GetSubmenuAndItem(path, false);
            if (!fromMenu) return false;
            
            var controls = FindControlsWithName(fromMenu, fromName);
            if (controls.Length == 0) return false;
            fromMenu.controls.RemoveAll(c => controls.Contains(c));

            if (position < 0) position = fromMenu.controls.Count + position;
            position = VrcfMath.Clamp(position, 0, fromMenu.controls.Count);

            fromMenu.controls.InsertRange(position, controls);
            return true;
        }

        private (VRCExpressionsMenu menu, string name) GetSubmenuAndItem(string rawPath, bool create) {
            var path = SplitPath(rawPath);
            var prefix = Slice(path, Math.Max(0, path.Count - 1));
            return (GetSubmenu(prefix, createIfMissing: create), path.LastOrDefault() ?? "");
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
         */
        private VRCExpressionsMenu GetSubmenu(
            IList<string> path,
            bool createIfMissing = true
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
                    folderControl = NewControl();
                    folderControl.name = folderName;
                    folderControl.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
                    current.controls.Add(folderControl);
                }
                var folder = folderControl.subMenu;
                if (folder == null) {
                    if (!createIfMissing) return null;
                    folder = VrcfObjectFactory.Create<VRCExpressionsMenu>();
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
        public void NewMenuSlider(string path, VFAParam param, Texture2D icon = null) {
            var control = NewMenuItem(path);
            control.type = VRCExpressionsMenu.Control.ControlType.RadialPuppet;
            var menuParam = new VRCExpressionsMenu.Control.Parameter {
                name = param
            };
            control.subParameters = new[]{menuParam};
            control.icon = icon;
        }
        public void NewMenuPuppet(string path, VFAParam x, VFAParam y, Texture2D icon = null) {
            var control = NewMenuItem(path);
            control.type = VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet;
            var menuParamX = new VRCExpressionsMenu.Control.Parameter();
            menuParamX.name = x ?? "";
            var menuParamY = new VRCExpressionsMenu.Control.Parameter();
            menuParamY.name = y ?? "";
            control.subParameters = new[]{menuParamX, menuParamY};
            control.icon = icon;
        }

        public void MergeMenu(VRCExpressionsMenu from) {
            MergeMenu(new string[]{}, from);
        }

        public void MergeMenu(
            IList<string> prefix,
            VRCExpressionsMenu from,
            bool preserveSubmenuLinks = false
        ) {
            if (!preserveSubmenuLinks) {
                var target = string.Join("/", prefix);
                var controlCount = from.controls.Count;
                rootMenu.WorkLog(
                    string.IsNullOrEmpty(target)
                        ? $"Merged menu {from.GetPathAndName()} with {controlCount} root controls"
                        : $"Merged menu {from.GetPathAndName()} with {controlCount} root controls into {target}"
                );
            }
            MergeControls(
                GetSubmenu(prefix),
                from.controls,
                preserveSubmenuLinks,
                from
            );
        }

        private void MergeControls(
            VRCExpressionsMenu destination,
            IEnumerable<VRCExpressionsMenu.Control> controls,
            bool preserveSubmenuLinks,
            VRCExpressionsMenu sourceRoot = null
        ) {
            if (preserveSubmenuLinks && sourceRoot == destination) return;
            var combined = new HashSet<(VRCExpressionsMenu, VRCExpressionsMenu)>();
            if (sourceRoot != null) combined.Add((sourceRoot, destination));
            var seen = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();
            if (!preserveSubmenuLinks) seen.Add(sourceRoot, destination);
            Merge(controls, destination, combineDuplicateNames: preserveSubmenuLinks);

            void Merge(
                IEnumerable<VRCExpressionsMenu.Control> sourceControls,
                VRCExpressionsMenu to,
                bool combineDuplicateNames = false
            ) {
                var submenuCount = new Dictionary<string, int>();
                foreach (var control in sourceControls.ToArray()) {
                    if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu || control.subMenu == null) {
                        to.controls.Add(preserveSubmenuLinks ? control : CloneControl(control));
                        continue;
                    }
                    if (!preserveSubmenuLinks && seen.TryGetValue(control.subMenu, out var previous)) {
                        var copy = CloneControl(control);
                        copy.subMenu = previous;
                        to.controls.Add(copy);
                        continue;
                    }
                    var index = combineDuplicateNames ? 0
                        : submenuCount.TryGetValue(control.name, out var count) ? count + 1 : 0;
                    submenuCount[control.name] = index;
                    var folder = FindControlsWithName(
                        to,
                        control.name,
                        c => c.type == VRCExpressionsMenu.Control.ControlType.SubMenu
                    ).ElementAtOrDefault(index);
                    if (folder == null) {
                        folder = preserveSubmenuLinks ? control : CloneControl(control);
                        if (!preserveSubmenuLinks) folder.subMenu = null;
                        to.controls.Add(folder);
                    }
                    if (folder.subMenu == null) {
                        folder.subMenu = preserveSubmenuLinks ? control.subMenu : VrcfObjectFactory.Create<VRCExpressionsMenu>();
                    }
                    var child = folder.subMenu;
                    if (preserveSubmenuLinks) {
                        if (control.subMenu == child || !combined.Add((control.subMenu, child))) continue;
                    } else {
                        seen.Add(control.subMenu, child);
                    }
                    Merge(control.subMenu.controls, child);
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
