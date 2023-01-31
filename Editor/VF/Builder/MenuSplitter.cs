using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VF.Model.Feature;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {
    
    /**
     * This is responsible for fixing pagination for menus before / after VRCFury is done with them.
     * It is capable of splitting menus with more than the maximum allowed controls into separate pages,
     * and also capable of re-joining them back into oversized menus again.
     */
    public static class MenuSplitter {
        /**
         * This method is our primary way of iterating through menus. It needs to be recursion-aware,
         * since many avatars have recursion in their menus for some reason.
         */
        public static void ForEachMenu(
            VRCExpressionsMenu root,
            Action<VRCExpressionsMenu,IList<string>> ForEachMenu = null,
            Func<VRCExpressionsMenu.Control,IList<string>,ForEachMenuItemResult> ForEachItem = null
        ) {
            var stack = new Stack<Tuple<string[],VRCExpressionsMenu>>();
            var seen = new HashSet<VRCExpressionsMenu>();
            stack.Push(Tuple.Create(new string[]{}, root));
            while (stack.Count > 0) {
                var (path,menu) = stack.Pop();
                if (menu == null || seen.Contains(menu)) continue;
                seen.Add(menu);
                if (ForEachMenu != null)
                    ForEachMenu(menu, path);
                for (var i = 0; i < menu.controls.Count; i++) {
                    var item = menu.controls[i];
                    var itemPath = new List<string>();
                    itemPath.AddRange(path);
                    itemPath.Add(item.name);
                    var itemPathArr = itemPath.ToArray();

                    var recurse = true;
                    if (ForEachItem != null) {
                        var result = ForEachItem(item, itemPathArr);
                        if (result == ForEachMenuItemResult.Skip) {
                            recurse = false;
                        } else if (result == ForEachMenuItemResult.Delete) {
                            menu.controls.RemoveAt(i);
                            i--;
                            EditorUtility.SetDirty(menu);
                            recurse = false;
                        }
                    }
                    if (recurse && item.type == VRCExpressionsMenu.Control.ControlType.SubMenu) {
                        stack.Push(Tuple.Create(itemPathArr, item.subMenu));
                    }
                }
            }
        }
        
        public enum ForEachMenuItemResult {
            Continue,
            Delete,
            Skip
        }

        /**
         * VRChat doesn't care, but SDK3ToCCKConverter crashes if there are any null parameters
         * on a submenu
         */
        public static void FixNulls(VRCExpressionsMenu root) {
            ForEachMenu(root, ForEachItem: (control, path) => {
                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.parameter == null) {
                    control.parameter = new VRCExpressionsMenu.Control.Parameter() {
                        name = ""
                    };
                }
                return ForEachMenuItemResult.Continue;
            });
        }
        
        public static void SplitMenus(VRCExpressionsMenu root, OverrideMenuSettings menuSettings = null) {
            var nextText = "Next";
            Texture2D nextIcon = null;
            if (menuSettings != null) {
                if (!string.IsNullOrEmpty(menuSettings.nextText)) nextText = menuSettings.nextText;
                if (menuSettings.nextIcon != null) nextIcon = menuSettings.nextIcon;
            }
            var maxControlsPerPage = GetMaxControlsPerPage();
            ForEachMenu(root, (menu, path) => {
                if (menu.controls.Count > maxControlsPerPage) {
                    var nextPath = GetNextPageFilename(menu);
                    var nextMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                    AssetDatabase.CreateAsset(nextMenu, nextPath);
                    while (menu.controls.Count > maxControlsPerPage - 1) {
                        nextMenu.controls.Insert(0, menu.controls[menu.controls.Count - 1]);
                        menu.controls.RemoveAt(menu.controls.Count - 1);
                    }
                    menu.controls.Add(new VRCExpressionsMenu.Control() {
                        name = nextText,
                        icon = nextIcon,
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        subMenu = nextMenu
                    });
                }
            });
        }

        private static string GetNextPageFilename(VRCExpressionsMenu menu) {
            var assetPath = AssetDatabase.GetAssetPath(menu);
            if (assetPath == null) {
                throw new Exception(
                    "Needed to split menu " + menu.name + ", but it doesn't seem to be saved to a file?");
            }

            var dirName = Path.GetDirectoryName(assetPath);
            var baseName = Path.GetFileNameWithoutExtension(assetPath);
            var vfp = baseName.IndexOf("_vfp");
            if (vfp >= 0) {
                baseName = baseName.Substring(0, vfp);
            }
            for (var i = 2;; i++) {
                var newPath = dirName + "/" + baseName + "_vfp" + i + ".asset";
                if (File.Exists(newPath)) {
                    continue;
                }
                return newPath;
            }
        }

        public static void JoinMenus(VRCExpressionsMenu root) {
            ForEachMenu(root, (menu, path) => {
                for (var i = 0; i < menu.controls.Count; i++) {
                    var item = menu.controls[i];
                    if (IsVrcfPageItem(item)) {
                        menu.controls.RemoveAt(i);
                        menu.controls.InsertRange(i, item.subMenu.controls);
                        AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(item.subMenu));
                        i--;
                    }
                }
            });
        }

        private static bool IsVrcfPageItem(VRCExpressionsMenu.Control item) {
            if (item.type != VRCExpressionsMenu.Control.ControlType.SubMenu) return false;
            if (item.subMenu == null) return false;
            var assetPath = AssetDatabase.GetAssetPath(item.subMenu);
            if (assetPath == null) return false;
            return assetPath.Contains("_vfp");
        }

        private static int GetMaxControlsPerPage() {
            var num = VRCExpressionsMenu.MAX_CONTROLS;
            // In some SDK releases, this seems to be an unreasonable number. Auto-correct it to 8 in that case.
            if (num > 1000) {
                num = 8;
            }
            return num;
        }
    }
}