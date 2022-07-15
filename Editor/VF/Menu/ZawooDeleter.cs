using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Menu {
    public class ZawooDeleter {
        [MenuItem("Tools/VRCFury/Nuke all Zawoo components on avatar", priority = 1)]
        public static void Run() {
            var avatarObj = MenuUtils.GetSelectedAvatar();
            var effects = CleanupAllZawooComponents(avatarObj, false);
            if (effects.Count == 0) {
                EditorUtility.DisplayDialog(
                    "Zawoo Cleanup",
                    "No zawoo objects were found on avatar",
                    "Ok"
                );
                return;
            }
            var doIt = EditorUtility.DisplayDialog(
                "Zawoo Cleanup",
                "The following parts will be deleted from your avatar:\n" + string.Join("\n", effects) +
                "\n\nContinue?",
                "Yes, Delete them",
                "Cancel"
            );
            if (!doIt) return;
            CleanupAllZawooComponents(avatarObj, true);
        }

        [MenuItem("Tools/VRCFury/Nuke all Zawoo components on avatar", true)]
        public static bool Check() {
            return MenuUtils.GetSelectedAvatar() != null;
        }

        private static bool ShouldRemoveObj(GameObject obj) {
            if (obj == null) return false;
            if (ShouldRemoveAsset(obj)) return true;
            var lower = obj.name.ToLower();
            if (lower.Contains("caninepeen")) return true;
            if (lower.Contains("hybridpeen")) return true;
            if (lower.Contains("hybridanthropeen")) return true;
            if (lower.Contains("peen_low")) return true;
            if (lower.Contains("particles_dynamic")) return true;
            if (lower.Contains("dynamic_penetrator")) return true;
            if (lower.Contains("armature_peen")) return true;
            return false;
        }
        private static bool ShouldRemoveAsset(Object obj) {
            if (obj == null) return false;
            var path = AssetDatabase.GetAssetPath(obj);
            if (path == null) return false;
            var lower = path.ToLower();
            if (lower.Contains("caninepeen")) return true;
            if (lower.Contains("hybridanthropeen")) return true;
            return false;
        }
        private static bool ShouldRemoveLayer(string name) {
            if (name.StartsWith("kcp_")) return true;
            if (name == "State Change") return true;
            if (name == "Particle") return true;
            if (name == "Dynamic") return true;
            return false;
        }
        private static bool ShouldRemoveParam(string name) {
            if (name.StartsWith("caninePeen")) return true;
            if (name.StartsWith("peen")) return true;
            return false;
        }

        private static List<string> CleanupAllZawooComponents(GameObject avatarObj, bool perform = false) {
            var removeItems = new List<string>();

            var checkStack = new Stack<Transform>();
            checkStack.Push(avatarObj.transform);
            while (checkStack.Count > 0) {
                var t = checkStack.Pop();
                var obj = t.gameObject;

                if (ShouldRemoveObj(obj) && (!PrefabUtility.IsPartOfPrefabInstance(obj) || PrefabUtility.IsOutermostPrefabInstanceRoot(obj))) {
                    removeItems.Add("Object: " + obj.name);
                    if (perform) Object.DestroyImmediate(obj);
                } else {
                    foreach (Transform t2 in t) checkStack.Push(t2);
                }
            }

            var avatar = avatarObj.GetComponent<VRCAvatarDescriptor>();
            var avatarFx = VRCAvatarUtils.GetAvatarFx(avatar);
            if (avatarFx != null) {
                if (ShouldRemoveAsset(avatarFx)) {
                    removeItems.Add("Avatar descriptor FX layer setting");
                    if (perform) VRCAvatarUtils.SetAvatarFx(avatar, null);
                } else {
                    for (var i = 0; i < avatarFx.layers.Length; i++) {
                        var layer = avatarFx.layers[i];
                        if (ShouldRemoveLayer(layer.name)) {
                            removeItems.Add("Layer: " + layer.name);
                            if (perform) {
                                avatarFx.RemoveLayer(i);
                                i--;
                            }
                        }
                    }
                    for (var i = 0; i < avatarFx.parameters.Length; i++) {
                        var prm = avatarFx.parameters[i];
                        if (ShouldRemoveParam(prm.name)) {
                            removeItems.Add("Parameter: " + prm.name);
                            if (perform) {
                                avatarFx.RemoveParameter(i);
                                i--;
                            }
                        }
                    }
                }
            }

            var syncParams = VRCAvatarUtils.GetAvatarParams(avatar);
            if (syncParams != null) {
                if (ShouldRemoveAsset(syncParams)) {
                    removeItems.Add("Avatar descriptor params setting");
                    if (perform) VRCAvatarUtils.SetAvatarParams(avatar, null);
                } else {
                    var prms = new List<VRCExpressionParameters.Parameter>(syncParams.parameters);
                    for (var i = 0; i < prms.Count; i++) {
                        if (ShouldRemoveParam(prms[i].name)) {
                            removeItems.Add("Synced param: " + prms[i].name);
                            if (perform) {
                                prms.RemoveAt(i);
                                i--;
                            }
                        }
                    }
                    if (perform) syncParams.parameters = prms.ToArray();
                }
            }

            void CheckMenu(VRCExpressionsMenu menu) {
                for (var i = 0; i < menu.controls.Count; i++) {
                    if (menu.controls[i].type != VRCExpressionsMenu.Control.ControlType.SubMenu) continue;
                    if (menu.controls[i].subMenu == null) continue;
                    if (ShouldRemoveAsset(menu.controls[i].subMenu)) {
                        removeItems.Add("Menu Item: " + menu.controls[i].name);
                        if (perform) {
                            menu.controls.RemoveAt(i);
                            i--;
                        }
                    } else {
                        CheckMenu(menu.controls[i].subMenu);
                    }
                }
            }

            var m = VRCAvatarUtils.GetAvatarMenu(avatar);
            if (m != null) {
                if (ShouldRemoveAsset(m)) {
                    removeItems.Add("Avatar descriptor menu setting");
                    if (perform) VRCAvatarUtils.SetAvatarMenu(avatar, null);
                } else {
                    CheckMenu(m);
                }
            }

            return removeItems;
        }
    }
}