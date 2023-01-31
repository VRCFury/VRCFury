using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VF.Inspector;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {

    public class MenuManager {
        private readonly VRCExpressionsMenu rootMenu;
        private readonly string tmpDir;

        public MenuManager(VRCExpressionsMenu menu, string tmpDir) {
            rootMenu = menu;
            this.tmpDir = tmpDir;
        }

        public VRCExpressionsMenu GetRaw() {
            return rootMenu;
        }
        private VRCExpressionsMenu.Control NewMenuItem(string path) {
            path = path.Replace("\\/", "REALSLASH");
            var split = path.Split('/').Select(s => s.Replace("REALSLASH", "/")).ToArray();
            var control = new VRCExpressionsMenu.Control();
            control.name = split[split.Length-1];
            var submenu = GetSubmenu(Slice(split, split.Length-1));
            submenu.controls.Add(control);
            return control;
        }

        /**
         * Gets the VRC menu for the path specified, recursively creating if it doesn't exist.
         * If createFromControl is set, we will use it as the basis if creating the folder control is needed.
         */
        public VRCExpressionsMenu GetSubmenu(
            string[] path,
            bool createIfMissing = true,
            VRCExpressionsMenu.Control createFromControl = null,
            Func<string,string> rewriteParamName = null
        ) {
            var current = GetRaw();
            for (var i = 0; i < path.Length; i++) {
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
                    if (createFromControl != null && i == path.Length - 1) {
                        folderControl = CloneControl(createFromControl, rewriteParamName);
                    } else {
                        folderControl = new VRCExpressionsMenu.Control();
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
        public void NewMenuToggle(string path, VFAParam param, float value = 1, Texture2D icon = null) {
            var control = NewMenuItem(path);
            control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            control.parameter = new VRCExpressionsMenu.Control.Parameter {
                name = param.Name()
            };
            control.value = value;
            control.icon = icon;
        }
        public void NewMenuSlider(string path, VFANumber param, Texture2D icon = null) {
            var control = NewMenuItem(path);
            control.type = VRCExpressionsMenu.Control.ControlType.RadialPuppet;
            var menuParam = new VRCExpressionsMenu.Control.Parameter {
                name = param.Name()
            };
            control.subParameters = new[]{menuParam};
            control.icon = icon;
        }
        public void NewMenuPuppet(string path, VFANumber x, VFANumber y, Texture2D icon = null) {
            var control = NewMenuItem(path);
            control.type = VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet;
            var menuParamX = new VRCExpressionsMenu.Control.Parameter();
            menuParamX.name = (x != null) ? x.Name() : "";
            var menuParamY = new VRCExpressionsMenu.Control.Parameter();
            menuParamY.name = (y != null) ? y.Name() : "";
            control.subParameters = new[]{menuParamX, menuParamY};
            control.icon = icon;
        }

        private VRCExpressionsMenu CreateNewMenu(string[] path) {
            var cleanPath = path.Select(CleanTitleForFilename);
            var newMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            string filename;
            if (path.Length > 0) filename = "VRCF_Menu_" + string.Join("_", cleanPath);
            else filename = tmpDir + "VRCF_Menu";
            VRCFuryAssetDatabase.SaveAsset(newMenu, tmpDir, filename);
            return newMenu;
        }
        private static string CleanTitleForFilename(string str) {
            // strip html tags
            str = Regex.Replace(str, "<.*?>", string.Empty);
            // clean up extra spaces
            str = Regex.Replace(str, " +", " ").Trim();
            return str;
        }

        public void MergeMenu(VRCExpressionsMenu from, Func<string,string> rewriteParamName = null) {
            MergeMenu(new string[]{}, from, rewriteParamName);
        }

        public void MergeMenu(
            string[] prefix,
            VRCExpressionsMenu from,
            Func<string,string> rewriteParamName = null,
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
                        var toControl = CloneControl(fromControl, rewriteParamName);
                        toControl.subMenu = seen[fromControl.subMenu];
                        to.controls.Add(toControl);
                    } else {
                        var submenuDupId = GetNextSubmenuDupId(fromControl.name);
                        var prefix2 = new List<string>(prefix);
                        prefix2.Add(fromControl.name + (submenuDupId > 0 ? (".dup." + submenuDupId) : ""));
                        GetSubmenu(prefix2.ToArray(), createFromControl: fromControl, rewriteParamName: rewriteParamName);
                        MergeMenu(prefix2.ToArray(), fromControl.subMenu, rewriteParamName, seen);
                    }
                } else {
                    to.controls.Add(CloneControl(fromControl, rewriteParamName));
                }
            }
        }

        private VRCExpressionsMenu.Control CloneControl(VRCExpressionsMenu.Control from, Func<string,string> rewriteParamName) {
            return new VRCExpressionsMenu.Control {
                name = from.name,
                icon = from.icon,
                type = from.type,
                parameter = CloneControlParam(from.parameter, rewriteParamName),
                value = from.value,
                style = from.style,
                subMenu = from.subMenu,
                labels = from.labels,
                subParameters = from.subParameters == null ? null : new List<VRCExpressionsMenu.Control.Parameter>(from.subParameters)
                    .Select(p => CloneControlParam(p, rewriteParamName))
                    .ToArray(),
            };
        }
        private VRCExpressionsMenu.Control.Parameter CloneControlParam(VRCExpressionsMenu.Control.Parameter from, Func<string,string> rewriteParamName) {
            if (from == null) return null;
            return new VRCExpressionsMenu.Control.Parameter {
                name = rewriteParamName != null ? rewriteParamName(from.name) : from.name
            };
        }

        public static string[] Slice(string[] arr, int count) {
            return new ArraySegment<string>(arr, 0, count).ToArray();
        }

    }

}
