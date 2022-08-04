using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Inspector;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {

    public class MenuManager {
        private static string prefix = "VRCFury";
        private readonly VRCExpressionsMenu rootMenu;
        private readonly string tmpDir;

        public MenuManager(VRCExpressionsMenu menu, string tmpDir) {
            rootMenu = menu;
            this.tmpDir = tmpDir;
        }

        public VRCExpressionsMenu GetVrcFuryMenu() {
            return rootMenu;
        }
        public VRCExpressionsMenu.Control NewMenuItem(string path) {
            var split = path.Split('/');
            var control = new VRCExpressionsMenu.Control();
            control.name = split[split.Length-1];
            AddMenuItem(new ArraySegment<string>(split, 0, split.Length-1).ToArray(), control);
            return control;
        }

        /**
         * If createIcon is set, we will use it when creating the folder control (if it didn't already exist)
         */
        public VRCExpressionsMenu GetMenu(string[] path, Texture2D createIcon = null) {
            var current = GetVrcFuryMenu();
            for (var i = 0; i < path.Length; i++) {
                var folderName = path[i];
                var folderControl = current.controls.Find(
                    c => c.name == folderName && c.type == VRCExpressionsMenu.Control.ControlType.SubMenu);
                if (folderControl == null) {
                    folderControl = new VRCExpressionsMenu.Control() {
                        name = folderName,
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu
                    };
                    if (createIcon != null && i == path.Length - 1) {
                        folderControl.icon = createIcon;
                    }
                    current.controls.Add(folderControl);
                }
                var folder = folderControl.subMenu;
                if (folder == null) {
                    var newFolderPath = new ArraySegment<string>(path, 0, i + 1)
                        .ToArray();
                    folder = CreateNewMenu(newFolderPath);
                    folderControl.subMenu = folder;
                }
                current = folder;
            }
            return current;
        }
        public void AddMenuItem(string[] menuPath, VRCExpressionsMenu.Control control) {
            var menu = GetMenu(menuPath);
            menu.controls.Add(control);
        }
        public void NewMenuToggle(string path, VFAParam param, float value = 1) {
            var control = NewMenuItem(path);
            control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            var menuParam = new VRCExpressionsMenu.Control.Parameter();
            menuParam.name = param.Name();
            control.parameter = menuParam;
            control.value = value;
        }
        public void NewMenuSlider(string path, VFANumber param) {
            var control = NewMenuItem(path);
            control.type = VRCExpressionsMenu.Control.ControlType.RadialPuppet;
            var menuParam = new VRCExpressionsMenu.Control.Parameter();
            menuParam.name = param.Name();
            control.subParameters = new[]{menuParam};
        }
        public void NewMenuPuppet(string path, VFANumber x, VFANumber y) {
            var control = NewMenuItem(path);
            control.type = VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet;
            var menuParamX = new VRCExpressionsMenu.Control.Parameter();
            menuParamX.name = (x != null) ? x.Name() : "";
            var menuParamY = new VRCExpressionsMenu.Control.Parameter();
            menuParamY.name = (y != null) ? y.Name() : "";
            control.subParameters = new[]{menuParamX, menuParamY};
        }

        private VRCExpressionsMenu CreateNewMenu(string[] path) {
            var newMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            string filePath;
            if (path.Length > 0) filePath = tmpDir + "/VRCF_Menu_" + VRCFuryEditorUtils.MakeFilenameSafe(string.Join("_", path)) + ".asset";
            else filePath = tmpDir + "/VRCF_Menu.asset";
            AssetDatabase.CreateAsset(newMenu, filePath);
            return newMenu;
        }
        
        public static void PurgeFromMenu(VRCExpressionsMenu menu) {
            if (menu == null) return;
            for (var i = 0; i < menu.controls.Count; i++) {
                var remove = false;
                var control = menu.controls[i];
                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu != null) {
                    if (control.subMenu.name.StartsWith("VRCFury")) {
                        remove = true;
                    }
                    if (VRCFuryBuilder.IsVrcfAsset(control.subMenu)) {
                        remove = true;
                    }
                }
                if (control.name == "SenkyFX" || control.name == "VRCFury") {
                    remove = true;
                }
                if (control.parameter != null && control.parameter.name != null && control.parameter.name.StartsWith(prefix)) {
                    remove = true;
                }
                if (control.subParameters != null && control.subParameters.Any(p => p != null && p.name.StartsWith(prefix))) {
                    remove = true;
                }
                if (remove) {
                    menu.controls.RemoveAt(i);
                    i--;
                } else if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu) {
                    PurgeFromMenu(control.subMenu);
                }
            }
        }

    }

}
