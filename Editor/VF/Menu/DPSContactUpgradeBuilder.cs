using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF.Menu {
    public class DPSContactUpgradeBuilder {
        [MenuItem("Tools/VRCFury/Upgrade DPS with TPS Contacts", priority = 2)]
        private static void Run() {
            var obj = MenuUtils.GetSelectedAvatar();
            Apply(obj);
        }

        [MenuItem("Tools/VRCFury/Upgrade DPS with TPS Contacts", true)]
        private static bool Check() {
            var obj = MenuUtils.GetSelectedAvatar();
            if (obj == null) return false;
            return true;
        }

        private static void Apply(GameObject avatarObject) {
            var oscDepth = 0.25f; // meters
            
            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
            var fx = VRCAvatarUtils.GetAvatarFx(avatar);
            var controller = new VFAController(fx, null);

            // Clean up
            for (var i = 0; i < fx.parameters.Length; i++) {
                var param = fx.parameters[i];
                if (param.name.StartsWith("TPS") && param.name.Contains("/VF")) {
                    fx.RemoveParameter(param);
                    i--;
                }
            }
            foreach (var c in avatarObject.GetComponentsInChildren<VRCContactSender>(true)) {
                if (c.collisionTags.Any(t => t.StartsWith("TPSVF_"))) Object.DestroyImmediate(c);
            }
            foreach (var c in avatarObject.GetComponentsInChildren<VRCContactReceiver>(true)) {
                if (c.collisionTags.Any(t => t.StartsWith("TPSVF_"))) Object.DestroyImmediate(c);
            }

            var penI = 0;
            foreach (var pair in getPenetrators(avatarObject)) {
                var obj = pair.Item1;
                var mesh = pair.Item2;
                
                Debug.Log("Found DPS penetrator: " + obj);
                
                if (obj.GetComponent<VRCContactReceiver>() != null || obj.GetComponent<VRCContactSender>() != null) {
                    Debug.Log("Already has contacts... skipping");
                }

                var forward = new Vector3(0, 0, 1);
                var length = mesh.vertices.Select(v => Vector3.Dot(v, forward)).Max();
                float width = mesh.vertices.Select(v => new Vector2(v.x, v.y).magnitude).Average() * 2;

                AddSender(obj, Vector3.zero, length, "TPS_Pen_Penetrating");
                AddSender(obj, Vector3.zero, Mathf.Max(0.01f, length - width), "TPS_Pen_Width");

                string paramFloatRootRoot = "TPS_Internal/Pen/VF" + penI + "/RootRoot";
                string paramFloatRootFrwd = "TPS_Internal/Pen/VF" + penI + "/RootForw";
                string paramFloatBackRoot = "TPS_Internal/Pen/VF" + penI + "/BackRoot";
                controller.NewFloat(paramFloatRootRoot);
                controller.NewFloat(paramFloatRootFrwd);
                controller.NewFloat(paramFloatBackRoot);
                AddReceiver(obj, Vector3.zero, paramFloatRootRoot, length, "TPS_Orf_Root");
                AddReceiver(obj, Vector3.zero, paramFloatRootFrwd, length, "TPS_Orf_Norm");
                AddReceiver(obj, Vector3.back * 0.01f, paramFloatBackRoot, length, "TPS_Orf_Root");
                penI++;
            }

            var orfI = 0;
            foreach (var pair in getOrificeLights(avatarObject)) {
                var light = pair.Item1;
                var normal = pair.Item2;

                Debug.Log("Found DPS orifice: " + light);

                var hasSenders = light.transform.parent.GetComponentsInChildren<VRCContactSender>(true).Length > 0;
                var hasReceivers = light.transform.parent.GetComponentsInChildren<VRCContactReceiver>(true).Length > 0;

                // If the user used TPS setup, they probably only got senders, so let's add the receivers for them.
                if (hasReceivers) {
                    Debug.Log("Already has receivers... skipping");
                }
                
                var forward = (normal.transform.localPosition - light.transform.localPosition).normalized;
                var param = "TPS_Internal/Orf/VF" + orfI + "/Depth_In";
                controller.NewFloat(param);
                AddReceiver(light.gameObject, forward * -oscDepth, param, oscDepth, "TPS_Pen_Penetrating");

                if (!hasSenders) {
                    AddSender(light.gameObject, Vector3.zero, 0.01f, "TPS_Orf_Root");
                    AddSender(light.gameObject, forward * 0.01f, 0.01f, "TPS_Orf_Norm");
                }
                orfI++;
            }
            
            EditorUtility.DisplayDialog(
                "DPS Upgrader",
                "VRCFury upgraded " + penI + " DPS penetrators and " + orfI + " DPS orifices with TPS contacts",
                "Ok"
            );
        }
        
        private static void AddSender(GameObject obj, Vector3 pos, float radius, string tag) {
            var normSender = obj.AddComponent<VRCContactSender>();
            normSender.position = pos;
            normSender.radius = radius;
            normSender.collisionTags = new List<string> { tag, RandomTag() };
        }

        private static void AddReceiver(GameObject obj, Vector3 pos, String param, float radius, string tag) {
            var receiver = obj.AddComponent<VRCContactReceiver>();
            receiver.position = pos;
            receiver.parameter = param;
            receiver.radius = radius;
            receiver.receiverType = VRC.Dynamics.ContactReceiver.ReceiverType.Proximity;
            receiver.collisionTags = new List<string> { tag, RandomTag() };
        }
        
        private static List<Tuple<GameObject,Mesh>> getPenetrators(GameObject avatarObject) {
            var skins = new List<Tuple<GameObject,Mesh>>();
            bool materialIsDps(Material mat) {
                if (mat == null) return false;
                if (!mat.shader) return false;
                if (mat.shader.name == "Raliv/Penetrator") return true;
                if (mat.shader.name.Contains("XSToon") && mat.shader.name.Contains("Penetrator")) return true;
                if (mat.HasProperty("_PenetratorEnabled") && mat.GetFloat("_PenetratorEnabled") > 0) return true;
                return false;
            };
            foreach (var skin in avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                if (!skin.sharedMaterials.Any(materialIsDps)) continue;
                skins.Add(Tuple.Create(skin.gameObject, skin.sharedMesh));
            }
            foreach (var renderer in avatarObject.GetComponentsInChildren<MeshRenderer>(true)) {
                if (!renderer.sharedMaterials.Any(materialIsDps)) continue;
                var meshFilter = renderer.GetComponent<MeshFilter>();
                if (!meshFilter) continue;
                skins.Add(Tuple.Create(renderer.gameObject, meshFilter.sharedMesh));
            }
            return skins;
        }

        private static readonly System.Random rand = new System.Random();

        private static string RandomTag() {
            return "TPSVF_" + rand.Next(100_000_000, 999_999_999);
        }

        private static List<Tuple<Light,Light>> getOrificeLights(GameObject avatarObject) {
            var pairs = new List<Tuple<Light,Light>>();
            foreach (var light in avatarObject.GetComponentsInChildren<Light>(true)) {
                if (light.range >= 0.405f && light.range <= 0.425f) {
                    Light normal = null;
                    foreach (Transform sibling in light.gameObject.transform.parent) {
                        var siblingLight = sibling.gameObject.GetComponent<Light>();
                        if (siblingLight != null && siblingLight.range >= 0.445f && siblingLight.range <= 0.455f) {
                            normal = siblingLight;
                        }
                    }
                    if (normal == null) {
                        Debug.Log("Failed to find normal sibling light for light: " + light);
                        continue;
                    }
                    pairs.Add(Tuple.Create(light, normal));
                }
            }

            return pairs;
        }
    }
}
