using System;
using System.Collections.Generic;
using VF.Inspector;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Utils {
    public static class VRCExpressionsMenuExtensions {
        /**
         * This method is our primary way of iterating through menus. It needs to be recursion-aware,
         * since many avatars have recursion in their menus for some reason.
         */
        public static void ForEachMenu(
            this VRCExpressionsMenu root,
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
                            VRCFuryEditorUtils.MarkDirty(menu);
                            recurse = false;
                        }
                    }
                    if (recurse && item.type == VRCExpressionsMenu.Control.ControlType.SubMenu) {
                        stack.Push(Tuple.Create(itemPathArr, item.subMenu));
                    }
                }
                VRCFuryEditorUtils.MarkDirty(menu);
            }
        }
        
        public enum ForEachMenuItemResult {
            Continue,
            Delete,
            Skip
        }

        public static void RewriteParameters(this VRCExpressionsMenu root, Func<string,string> each) {
            root.ForEachMenu(ForEachItem: (item,path) => {
                if (item.parameter != null && item.parameter.name != null) {
                    item.parameter.name = each(item.parameter.name);
                }
                if (item.subParameters != null) {
                    foreach (var p in item.subParameters) {
                        if (p != null && p.name != null) {
                            p.name = each(p.name);
                        }
                    }
                }
                return ForEachMenuItemResult.Continue;
            });
        }
    }
}
