using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace VF.Menu {
    public class DPSContactUpgradeBuilder {
        [MenuItem("Tools/VRCFury/Setup OscGB on Avatar", priority = 2)]
        private static void Run() {
            var obj = MenuUtils.GetSelectedAvatar();
            var msg = Apply(obj);
            VRCFuryVRCPatch.DeleteOscFilesForAvatar(obj);
            EditorUtility.DisplayDialog(
                "OscGB Upgrade",
                msg,
                "Ok"
            );
        }

        [MenuItem("Tools/VRCFury/Setup OscGB on Avatar", true)]
        private static bool Check() {
            var obj = MenuUtils.GetSelectedAvatar();
            if (obj == null) return false;
            return true;
        }

        public static string Apply(GameObject avatarObject) {
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
                if (c.collisionTags.Any(t => t.StartsWith("TPSVF_")) || c.gameObject.name.StartsWith("OGB_")) {
                    if (c.gameObject.GetComponents<Component>().Length == 2 && !PrefabUtility.IsPartOfAnyPrefab(c.gameObject) && c.gameObject.transform.childCount == 0) Object.DestroyImmediate(c.gameObject);
                    else Object.DestroyImmediate(c);
                }
            }
            foreach (var c in avatarObject.GetComponentsInChildren<VRCContactReceiver>(true)) {
                if (c.collisionTags.Any(t => t.StartsWith("TPSVF_")) || c.gameObject.name.StartsWith("OGB_")) {
                    if (c.gameObject.GetComponents<Component>().Length == 2 && !PrefabUtility.IsPartOfAnyPrefab(c.gameObject) && c.gameObject.transform.childCount == 0) Object.DestroyImmediate(c.gameObject);
                    else Object.DestroyImmediate(c);
                }
            }

            var addedOGB = new List<string>();
            var addedTPS = new List<string>();
            var usedNames = new List<string>();

            string getNextName(string prefix) {
                for (int i = 0; ; i++) {
                    var next = prefix + (i == 0 ? "" : i+"");
                    if (!usedNames.Contains(next)) return next;
                }
            }
            
            var CONTACT_PEN_MAIN = "TPS_Pen_Penetrating";
            var CONTACT_PEN_WIDTH = "TPS_Pen_Width";
            var CONTACT_PEN_CLOSE = "TPS_Pen_Close";
            var CONTACT_PEN_ROOT = "TPS_Pen_Root";
            var CONTACT_ORF_MAIN = "TPS_Orf_Root";
            var CONTACT_ORF_NORM = "TPS_Orf_Norm";

            var penI = 0;
            foreach (var pair in getPenetrators(avatarObject)) {
                var obj = pair.Item1;
                var mesh = pair.Item2;
                var isTps = pair.Item3;
                var path = AnimationUtility.CalculateTransformPath(obj.transform, avatarObject.transform);

                Debug.Log("Found DPS penetrator: " + obj);
                
                var prefabBase = PrefabUtility.IsPartOfAnyPrefab(obj) ? PrefabUtility.GetNearestPrefabInstanceRoot(obj) : obj;
                var hasSenders = isTps ? prefabBase.GetComponentsInChildren<VRCContactSender>(true).Length > 0 : false;
                var hasReceivers = isTps ? prefabBase.GetComponentsInChildren<VRCContactReceiver>(true).Length > 0 : false;

                var forward = new Vector3(0, 0, 1);
                var length = mesh.vertices
                    .Select(v => Vector3.Dot(v, forward)).Max();
                //float radius = mesh.vertices
                //    .Where(v => v.z > 0)
                //    .Select(v => new Vector2(v.x, v.y).magnitude).Average();
                var verticesInFront = mesh.vertices.Where(v => v.z > 0);
                var verticesInFrontCount = verticesInFront.Count();
                float radiusThatEncompasesMost = verticesInFront
                    .Select(v => new Vector2(v.x, v.y).magnitude)
                    .OrderBy(m => m)
                    .Where((m, i) => i <= verticesInFrontCount*0.75)
                    .Max();
                float radius = radiusThatEncompasesMost;
                
                var tightPos = forward * (length / 2);
                var tightRot = Quaternion.LookRotation(forward) * Quaternion.LookRotation(Vector3.up);

                // Add TPS senders
                if (!hasSenders) {
                    AddSender(obj, Vector3.zero, "Radius", length, CONTACT_PEN_MAIN);
                    AddSender(obj, Vector3.zero, "WidthHelper", Mathf.Max(0.01f, length - radius*2), CONTACT_PEN_WIDTH);
                    addedTPS.Add(path);
                }
                
                AddSender(obj, tightPos, "Envelope", radiusThatEncompasesMost, CONTACT_PEN_CLOSE, rotation: tightRot, height: length);
                AddSender(obj, Vector3.zero, "Root", 0.01f, CONTACT_PEN_ROOT);

                // Add TPS receivers
                /*
                if (!hasReceivers) {
                    AddReceiver(obj, Vector3.zero, "TPS_Internal/Pen/VF" + penI + "/RootRoot", controller, length, new []{CONTACT_ORF_MAIN});
                    AddReceiver(obj, Vector3.zero, "TPS_Internal/Pen/VF" + penI + "/RootForw", controller, length, new []{CONTACT_ORF_NORM});
                    AddReceiver(obj, Vector3.back * 0.01f, "TPS_Internal/Pen/VF" + penI + "/BackRoot", controller, length, new []{CONTACT_ORF_MAIN});
                }
                */

                // Add OscGB receivers
                var name = getNextName("OGB/Pen/" + obj.name.Replace('/','_'));
                AddReceiver(obj, tightPos, name + "/TouchSelfClose", "TouchSelfClose", controller, radiusThatEncompasesMost, SelfContacts, allowOthers:false, localOnly:true, rotation: tightRot, height: length, type: ContactReceiver.ReceiverType.Constant);
                AddReceiver(obj, Vector3.zero, name + "/TouchSelf", "TouchSelf", controller, length, SelfContacts, allowOthers:false, localOnly:true);
                AddReceiver(obj, tightPos, name + "/TouchOthersClose", "TouchOthersClose", controller, radiusThatEncompasesMost, BodyContacts, allowSelf:false, localOnly:true, rotation: tightRot, height: length, type: ContactReceiver.ReceiverType.Constant);
                AddReceiver(obj, Vector3.zero, name + "/TouchOthers", "TouchOthers", controller, length, BodyContacts, allowSelf:false, localOnly:true);
                AddReceiver(obj, Vector3.zero, name + "/PenSelf", "PenSelf", controller, length, new []{CONTACT_ORF_MAIN}, allowOthers:false, localOnly:true);
                AddReceiver(obj, Vector3.zero, name + "/PenOthers", "PenOthers", controller, length, new []{CONTACT_ORF_MAIN}, allowSelf:false, localOnly:true);
                AddReceiver(obj, Vector3.zero, name + "/FrotOthers", "FrotOthers", controller, length, new []{CONTACT_PEN_CLOSE}, allowSelf:false, localOnly:true);
                AddReceiver(obj, tightPos, name + "/FrotOthersClose", "FrotOthersClose", controller, radiusThatEncompasesMost, new []{CONTACT_PEN_CLOSE}, allowSelf:false, localOnly:true, rotation: tightRot, height: length, type: ContactReceiver.ReceiverType.Constant);
                addedOGB.Add(path);

                var versionTag = RandomTag();
                AddSender(obj, Vector3.zero, "Version", 0.01f, versionTag);
                AddReceiver(obj, Vector3.one * 0.01f, name + "/Version/2", "Version", controller, 0.01f, new []{versionTag}, allowOthers:false, localOnly:true);

                penI++;
            }

            var orfI = 0;
            foreach (var pair in getOrificeLights(avatarObject)) {
                var light = pair.Item1;
                var normal = pair.Item2;
                var obj = light.transform.parent.gameObject;
                var path = AnimationUtility.CalculateTransformPath(obj.transform, avatarObject.transform);

                Debug.Log("Found DPS orifice: " + light);

                var hasSenders = obj.GetComponentsInChildren<VRCContactSender>(true).Length > 0;
                var hasReceivers = obj.GetComponentsInChildren<VRCContactReceiver>(true).Length > 0;

                var forward = (normal.transform.localPosition - light.transform.localPosition).normalized;
                
                // Add TPS senders
                if (!hasSenders) {
                    AddSender(obj, Vector3.zero, "Root", 0.01f, CONTACT_ORF_MAIN);
                    AddSender(obj, forward * 0.01f, "Front", 0.01f, CONTACT_ORF_NORM);
                    addedTPS.Add(path);
                }

                // Add TPS receivers
                /*
                if (!hasReceivers) {
                    AddReceiver(obj, forward * -oscDepth, "TPS_Internal/Orf/VF" + orfI + "/Depth_In", controller, oscDepth, new []{"TPS_Pen_Penetrating"});
                }
                */
                
                var oscDepth = 0.25f;
                var tightRot = Quaternion.LookRotation(forward) * Quaternion.LookRotation(Vector3.up);
                var frotRadius = 0.1f;
                var frotPos = 0.05f;
                var closeRadius = 0.1f;

                // Add OscGB receivers
                var name = getNextName("OGB/Orf/" + obj.name.Replace('/','_'));
                AddReceiver(obj, forward * -oscDepth, name + "/TouchSelf", "TouchSelf", controller, oscDepth, SelfContacts, allowOthers:false, localOnly:true);
                AddReceiver(obj, forward * -(oscDepth/2), name + "/TouchSelfClose", "TouchSelfClose", controller, closeRadius, SelfContacts, allowOthers:false, localOnly:true, height: oscDepth, rotation: tightRot, type: ContactReceiver.ReceiverType.Constant);
                AddReceiver(obj, forward * -oscDepth, name + "/TouchOthers", "TouchOthers", controller, oscDepth, BodyContacts, allowSelf:false, localOnly:true);
                AddReceiver(obj, forward * -(oscDepth/2), name + "/TouchOthersClose", "TouchOthersClose", controller, closeRadius, BodyContacts, allowSelf:false, localOnly:true, height: oscDepth, rotation: tightRot, type: ContactReceiver.ReceiverType.Constant);
                AddReceiver(obj, forward * -oscDepth, name + "/PenSelf", "PenSelf", controller, oscDepth, new []{CONTACT_PEN_MAIN}, allowOthers:false, localOnly:true);
                AddReceiver(obj, Vector3.zero, name + "/PenSelfNewRoot", "PenSelfNewRoot", controller, 1f, new []{CONTACT_PEN_ROOT}, allowOthers:false, localOnly:true);
                AddReceiver(obj, Vector3.zero, name + "/PenSelfNewTip", "PenSelfNewTip", controller, 1f, new []{CONTACT_PEN_MAIN}, allowOthers:false, localOnly:true);
                AddReceiver(obj, forward * -oscDepth, name + "/PenOthers", "PenOthers", controller, oscDepth, new []{CONTACT_PEN_MAIN}, allowSelf:false, localOnly:true);
                AddReceiver(obj, Vector3.zero, name + "/PenOthersNewRoot", "PenOthersNewRoot", controller, 1f, new []{CONTACT_PEN_ROOT}, allowSelf:false, localOnly:true);
                AddReceiver(obj, Vector3.zero, name + "/PenOthersNewTip", "PenOthersNewTip", controller, 1f, new []{CONTACT_PEN_MAIN}, allowSelf:false, localOnly:true);
                AddReceiver(obj, forward * frotPos, name + "/FrotOthers", "FrotOthers", controller, frotRadius, new []{CONTACT_ORF_MAIN}, allowSelf:false, localOnly:true);
                addedOGB.Add(path);
                
                var versionTag = RandomTag();
                AddSender(obj, Vector3.zero, "Version", 0.01f, versionTag);
                AddReceiver(obj, Vector3.one * 0.01f, name + "/Version/2", "Version", controller, 0.01f, new []{versionTag}, allowOthers:false, localOnly:true);
                
                orfI++;
            }

            if (addedOGB.Count == 0 && addedTPS.Count == 0) {
                return "VRCFury failed to find any parts to upgrade! Ask on the discord?";
            }

            var msg = "";
            if (addedTPS.Count > 0) msg += "VRCFury added TPS senders to:\n" + String.Join("\n", addedTPS) + "\n\n";
            msg += "VRCFury added OscGB receivers to:\n" + String.Join("\n", addedOGB);
            return msg;
        }

        private static readonly string[] SelfContacts = {
            "Hand",
            "Finger",
            "Foot"
        };
        private static readonly string[] BodyContacts = {
            "Head",
            "Torso",
            "Hand",
            "Foot",
            "Finger"
        };
        
        private static void AddSender(
            GameObject obj,
            Vector3 pos,
            String objName,
            float radius,
            string tag,
            float height = 0,
            Quaternion rotation = default
        ) {
            var child = new GameObject();
            child.name = "OGB_Sender_" + objName;
            child.transform.SetParent(obj.transform, false);
            var sender = child.AddComponent<VRCContactSender>();
            sender.position = pos;
            sender.radius = radius;
            sender.collisionTags = new List<string> { tag };
            if (height > 0) {
                sender.shapeType = ContactBase.ShapeType.Capsule;
                sender.height = height;
                sender.rotation = rotation;
            }
        }

        private static void AddReceiver(
            GameObject obj,
            Vector3 pos,
            String param,
            String objName,
            VFAController controller,
            float radius,
            string[] tags,
            bool allowOthers = true,
            bool allowSelf = true,
            bool localOnly = false,
            float height = 0,
            Quaternion rotation = default,
            ContactReceiver.ReceiverType type = ContactReceiver.ReceiverType.Proximity
        ) {
            var child = new GameObject();
            child.name = "OGB_Receiver_" + objName;
            child.transform.SetParent(obj.transform, false);
            controller.NewFloat(param);
            var receiver = child.AddComponent<VRCContactReceiver>();
            receiver.position = pos;
            receiver.parameter = param;
            receiver.radius = radius;
            receiver.receiverType = type;
            receiver.collisionTags = new List<string>(tags);
            receiver.allowOthers = allowOthers;
            receiver.allowSelf = allowSelf;
            receiver.localOnly = localOnly;
            if (height > 0) {
                receiver.shapeType = ContactBase.ShapeType.Capsule;
                receiver.height = height;
                receiver.rotation = rotation;
            }
        }
        
        private static List<Tuple<GameObject,Mesh,bool>> getPenetrators(GameObject avatarObject) {
            var skins = new List<Tuple<GameObject,Mesh,bool>>();
            bool materialIsDps(Material mat) {
                if (mat == null) return false;
                if (!mat.shader) return false;
                if (mat.shader.name == "Raliv/Penetrator") return true;
                if (mat.shader.name.Contains("XSToon") && mat.shader.name.Contains("Penetrator")) return true;
                if (mat.HasProperty("_PenetratorEnabled") && mat.GetFloat("_PenetratorEnabled") > 0) return true;
                return false;
            };
            bool materialIsTps(Material mat) {
                if (mat == null) return false;
                if (!mat.shader) return false;
                if (mat.HasProperty("_TPSPenetratorEnabled") && mat.GetFloat("_TPSPenetratorEnabled") > 0) return true;
                return false;
            };
            foreach (var skin in avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                var isDps = skin.sharedMaterials.Any(materialIsDps);
                var isTps = skin.sharedMaterials.Any(materialIsTps);
                if (isDps || isTps) {
                    var temporaryMesh = new Mesh();
                    skin.BakeMesh(temporaryMesh);
                    var verts = temporaryMesh.vertices;
                    var scale = skin.transform.lossyScale;
                    var inverseScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
                    for (var i = 0; i < verts.Length; i++) {
                        verts[i].Scale(inverseScale);
                    }
                    temporaryMesh.vertices = verts;
                    skins.Add(Tuple.Create(skin.gameObject, temporaryMesh, isTps));
                }
            }
            foreach (var renderer in avatarObject.GetComponentsInChildren<MeshRenderer>(true)) {
                var meshFilter = renderer.GetComponent<MeshFilter>();
                if (!meshFilter) continue;
                if (renderer.sharedMaterials.Any(materialIsDps))
                    skins.Add(Tuple.Create(renderer.gameObject, meshFilter.sharedMesh, false));
                else if (renderer.sharedMaterials.Any(materialIsTps))
                    skins.Add(Tuple.Create(renderer.gameObject, meshFilter.sharedMesh, true));
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
