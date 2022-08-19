using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Inspector;
using VF.Model;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF.Menu {
    public class DPSContactUpgradeBuilder {
        public static void Run() {
            var avatarObject = MenuUtils.GetSelectedAvatar();
            var msg = Apply(avatarObject);
            EditorUtility.DisplayDialog(
                "OscGB Upgrade",
                msg,
                "Ok"
            );
        }

        public static bool Check() {
            var obj = MenuUtils.GetSelectedAvatar();
            if (obj == null) return false;
            return true;
        }

        private static bool IsOGBContact(Component c, List<string> collisionTags) {
            if (collisionTags.Any(t => t.StartsWith("TPSVF_"))) return true;
            else if (c.gameObject.name.StartsWith("OGB_")) return true;
            return false;
        }

        private static void DeleteIfNotInPrefab(GameObject obj) {
            if (!PrefabUtility.IsPartOfAnyPrefab(obj)) Object.DestroyImmediate(obj);
        }

        public static string Apply(GameObject avatarObject) {
            var addedOGB = new List<string>();
            var alreadyExists = new List<string>();

            string GetPath(GameObject obj) {
                return AnimationUtility.CalculateTransformPath(obj.transform, avatarObject.transform);
            }
            OGBPenetrator addPen(GameObject obj) {
                if (obj.GetComponentsInParent<OGBPenetrator>(true).Length > 0) return null;
                addedOGB.Add(GetPath(obj));
                return obj.AddComponent<OGBPenetrator>();
            }
            OGBOrifice addOrifice(GameObject obj) {
                if (obj.GetComponentsInParent<OGBOrifice>(true).Length > 0) return null;
                addedOGB.Add(GetPath(obj));
                return obj.AddComponent<OGBOrifice>();
            }

            foreach (var c in avatarObject.GetComponentsInChildren<OGBPenetrator>(true)) {
                alreadyExists.Add(GetPath(c.gameObject));
            }
            foreach (var c in avatarObject.GetComponentsInChildren<OGBOrifice>(true)) {
                alreadyExists.Add(GetPath(c.gameObject));
            }
            
            // Un-bake baked components
            foreach (var t in avatarObject.GetComponentsInChildren<Transform>(true)) {
                if (!t) continue; // this can happen if we're visiting one of the things we deleted below
                var penMarker = t.Find("OGB_Baked_Pen");
                if (penMarker) {
                    var p = addPen(t.gameObject);
                    if (p) {
                        var size = penMarker.Find("size");
                        if (size) {
                            p.length = size.localScale.x;
                            p.radius = size.localScale.y;
                        }
                        p.name = GetNameFromBakeMarker(penMarker.gameObject);
                    }
                    DeleteIfNotInPrefab(penMarker.gameObject);
                }

                var orfMarker = t.Find("OGB_Baked_Orf");
                if (orfMarker) {
                    var o = addOrifice(t.gameObject);
                    if (o) {
                        var autoInfo = OGBOrificeEditor.GetInfoFromLights(t.gameObject);
                        if (autoInfo != null) {
                            o.addLight = autoInfo.Item2 ? AddLight.Ring : AddLight.Hole;
                        }
                        o.name = GetNameFromBakeMarker(orfMarker.gameObject);
                        foreach (var light in t.gameObject.GetComponentsInChildren<Light>()) {
                            OGBUtils.RemoveComponent(light);
                        }
                    }
                    DeleteIfNotInPrefab(orfMarker.gameObject);
                }
            }
            
            // Auto-add DPS and TPS penetrators
            foreach (var skin in avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                if (OGBPenetratorEditor.GetAutoSize(skin.gameObject, true) != null) addPen(skin.gameObject);
            }
            foreach (var mesh in avatarObject.GetComponentsInChildren<MeshRenderer>(true)) {
                if (OGBPenetratorEditor.GetAutoSize(mesh.gameObject, true) != null) addPen(mesh.gameObject);
            }
            
            // Auto-add DPS orifices
            foreach (var light in avatarObject.GetComponentsInChildren<Light>(true)) {
                var parent = light.gameObject.transform.parent?.gameObject;
                if (parent) {
                    if (OGBOrificeEditor.GetInfoFromLights(parent) != null) addOrifice(parent);
                }
            }
            
            // Upgrade old OGB markers to components
            foreach (var t in avatarObject.GetComponentsInChildren<Transform>(true)) {
                if (!t) continue; // this can happen if we're visiting one of the things we deleted below
                var penMarker = t.Find("OGB_Marker_Pen");
                if (penMarker) {
                    addPen(t.gameObject);
                    DeleteIfNotInPrefab(penMarker.gameObject);
                }

                var holeMarker = t.Find("OGB_Marker_Hole");
                if (holeMarker) {
                    var o = addOrifice(t.gameObject);
                    if (o) o.addLight = AddLight.Hole;
                    DeleteIfNotInPrefab(holeMarker.gameObject);
                }
                
                var ringMarker = t.Find("OGB_Marker_Ring");
                if (ringMarker) {
                    var o = addOrifice(t.gameObject);
                    if (o) o.addLight = AddLight.Ring;
                    DeleteIfNotInPrefab(ringMarker.gameObject);
                }
            }

            // Clean up
            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
            var fx = VRCAvatarUtils.GetAvatarFx(avatar);
            if (fx) {
                for (var i = 0; i < fx.parameters.Length; i++) {
                    var param = fx.parameters[i];
                    var isOldTpsVf = param.name.StartsWith("TPS") && param.name.Contains("/VF");
                    var isOgb = param.name.StartsWith("OGB/");
                    if (isOldTpsVf || isOgb) {
                        fx.RemoveParameter(param);
                        i--;
                    }
                }
            }
            foreach (var c in avatarObject.GetComponentsInChildren<VRCContactSender>(true)) {
                if (IsOGBContact(c, c.collisionTags)) OGBUtils.RemoveComponent(c);
            }
            foreach (var c in avatarObject.GetComponentsInChildren<VRCContactReceiver>(true)) {
                if (IsOGBContact(c, c.collisionTags)) OGBUtils.RemoveComponent(c);
            }

            if (addedOGB.Count == 0 && alreadyExists.Count == 0) {
                return "VRCFury failed to find any parts to upgrade! Ask on the discord?";
            }

            return "VRCFury upgraded these objects with OscGB support:\n"
                   + String.Join("\n", addedOGB)
                   + "\n"
                   + String.Join("\n", alreadyExists.Select(a => a + " (already upgraded)"));
        }

        private static string GetNameFromBakeMarker(GameObject marker) {
            foreach (Transform child in marker.transform) {
                if (child.name.StartsWith("name=")) {
                    return child.name.Substring(5);
                }
            }
            return "";
        }
    }
}
