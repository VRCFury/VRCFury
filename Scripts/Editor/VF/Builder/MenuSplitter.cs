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
         * on a submenu. GestureManager crashes if there's any null parameters on ANYTHING.
         */
        public static void FixNulls(VRCExpressionsMenu root) {
            ForEachMenu(root, ForEachItem: (control, path) => {
                if (control.parameter == null) {
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
                var page = menu;
                var pageNum = 2;
                while (page.controls.Count > maxControlsPerPage) {
                    var nextPage = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                    nextPage.name = $"{menu.name} (Page {pageNum++})";
                    AssetDatabase.AddObjectToAsset(nextPage, root);
                    while (page.controls.Count > maxControlsPerPage - 1) {
                        nextPage.controls.Insert(0, page.controls[page.controls.Count - 1]);
                        page.controls.RemoveAt(page.controls.Count - 1);
                    }
                    page.controls.Add(new VRCExpressionsMenu.Control() {
                        name = nextText,
                        icon = nextIcon,
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        subMenu = nextPage
                    });
                    page = nextPage;
                }
            });
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