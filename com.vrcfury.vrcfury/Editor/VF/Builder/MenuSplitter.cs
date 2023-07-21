using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {
    
    /**
     * This is responsible for fixing pagination for menus before / after VRCFury is done with them.
     * It is capable of splitting menus with more than the maximum allowed controls into separate pages,
     * and also capable of re-joining them back into oversized menus again.
     */
    public static class MenuSplitter {
        public static void SplitMenus(VRCExpressionsMenu root, OverrideMenuSettings menuSettings = null) {
            var nextText = "Next";
            Texture2D nextIcon = null;
            if (menuSettings != null) {
                if (!string.IsNullOrEmpty(menuSettings.nextText)) nextText = menuSettings.nextText;
                if (menuSettings.nextIcon != null) nextIcon = menuSettings.nextIcon;
            }
            var maxControlsPerPage = GetMaxControlsPerPage();
            root.ForEachMenu((menu, path) => {
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
