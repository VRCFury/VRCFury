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

namespace VF.Menu {
    public class DPSContactUpgradeBuilder {
        
        // Bump when pen senders or receivers are changed
        private static int penVersion = 8;
        
        // Bump when orf senders or receivers are changed
        private static int orfVersion = 9;
        
        // Bump when any senders are changed
        private static int beaconVersion = 6;
        
        private static string CONTACT_PEN_MAIN = "TPS_Pen_Penetrating";
        private static string CONTACT_PEN_WIDTH = "TPS_Pen_Width";
        private static string CONTACT_PEN_CLOSE = "TPS_Pen_Close";
        private static string CONTACT_PEN_ROOT = "TPS_Pen_Root";
        private static string CONTACT_ORF_MAIN = "TPS_Orf_Root";
        private static string CONTACT_ORF_NORM = "TPS_Orf_Norm";

        public static string MARKER_ORF = "OGB_Marker_Orf";
        public static string MARKER_PEN = "OGB_Marker_Pen";

        public static void Run() {
            var obj = MenuUtils.GetSelectedAvatar();
            var msg = Apply(obj);
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
        
        public static void Remove() {
            var obj = MenuUtils.GetSelectedAvatar();
            Purge(obj);
            EditorUtility.DisplayDialog(
                "OscGB Upgrade",
                "Removed OGB from avatar",
                "Ok"
            );
        }

        private static void Purge(GameObject avatarObject) {
            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();

            // Clean up
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

            void maybeRemoveComponent(Component c, List<string> collisionTags) {
                var shouldRemove = false;
                if (collisionTags.Any(t => t.StartsWith("TPSVF_"))) shouldRemove = true;
                else if (c.gameObject.name.StartsWith("OGB_")) shouldRemove = true;
                else if (collisionTags.Any(t => t == CONTACT_PEN_MAIN)) shouldRemove = true;
                else if (collisionTags.Any(t => t == CONTACT_PEN_WIDTH)) shouldRemove = true;
                else if (collisionTags.Any(t => t == CONTACT_ORF_MAIN)) shouldRemove = true;
                else if (collisionTags.Any(t => t == CONTACT_ORF_NORM)) shouldRemove = true;
                if (!shouldRemove) return;
                if (c.gameObject.GetComponents<Component>().Length == 2 && !PrefabUtility.IsPartOfAnyPrefab(c.gameObject) && c.gameObject.transform.childCount == 0) Object.DestroyImmediate(c.gameObject);
                else Object.DestroyImmediate(c);
            }
            foreach (var c in avatarObject.GetComponentsInChildren<VRCContactSender>(true)) {
                maybeRemoveComponent(c, c.collisionTags);
            }
            foreach (var c in avatarObject.GetComponentsInChildren<VRCContactReceiver>(true)) {
                maybeRemoveComponent(c, c.collisionTags);
            }
        }

        public static string Apply(GameObject avatarObject) {
            Purge(avatarObject);

            var addedOGB = new List<string>();
            var usedNames = new List<string>();

            string getNextName(string prefix) {
                for (int i = 0; ; i++) {
                    var next = prefix + (i == 0 ? "" : i+"");
                    if (!usedNames.Contains(next)) {
                        usedNames.Add(next);
                        return next;
                    }
                }
            }

            foreach (var pair in getPenetrators(avatarObject)) {
                var obj = pair.Item1;
                var mesh = pair.Item2;
                var path = AnimationUtility.CalculateTransformPath(obj.transform, avatarObject.transform);

                Debug.Log("Found DPS penetrator: " + obj);

                var worldScale = obj.transform.lossyScale.x;

                var forward = new Vector3(0, 0, 1);
                var length = mesh.vertices
                    .Select(v => Vector3.Dot(v, forward)).Max() * worldScale;
                var verticesInFront = mesh.vertices.Where(v => v.z > 0);
                var verticesInFrontCount = verticesInFront.Count();
                float radius = verticesInFront
                    .Select(v => new Vector2(v.x, v.y).magnitude)
                    .OrderBy(m => m)
                    .Where((m, i) => i <= verticesInFrontCount*0.75)
                    .Max() * worldScale;
                
                var tightPos = forward * (length / 2);
                var tightRot = Quaternion.LookRotation(forward) * Quaternion.LookRotation(Vector3.up);

                var extraRadiusForTouch = Math.Min(radius, 0.08f /* 8cm */);
                
                // Extra frot radius should always match for everyone, so when two penetrators collide, both parties experience at the same time
                var extraRadiusForFrot = 0.08f;

                // Senders
                AddSender(obj, Vector3.zero, "Radius", length, CONTACT_PEN_MAIN);
                AddSender(obj, Vector3.zero, "WidthHelper", Mathf.Max(0.01f/obj.transform.lossyScale.x, length - radius*2), CONTACT_PEN_WIDTH);
                AddSender(obj, tightPos, "Envelope", radius, CONTACT_PEN_CLOSE, rotation: tightRot, height: length);
                AddSender(obj, Vector3.zero, "Root", 0.01f, CONTACT_PEN_ROOT);

                // Receivers
                var name = getNextName("OGB/Pen/" + obj.name.Replace('/','_'));
                AddReceiver(obj, tightPos, name + "/TouchSelfClose", "TouchSelfClose", radius+extraRadiusForTouch, SelfContacts, allowOthers:false, localOnly:true, rotation: tightRot, height: length+extraRadiusForTouch*2, type: ContactReceiver.ReceiverType.Constant);
                AddReceiver(obj, Vector3.zero, name + "/TouchSelf", "TouchSelf", length+extraRadiusForTouch, SelfContacts, allowOthers:false, localOnly:true);
                AddReceiver(obj, tightPos, name + "/TouchOthersClose", "TouchOthersClose", radius+extraRadiusForTouch, BodyContacts, allowSelf:false, localOnly:true, rotation: tightRot, height: length+extraRadiusForTouch*2, type: ContactReceiver.ReceiverType.Constant);
                AddReceiver(obj, Vector3.zero, name + "/TouchOthers", "TouchOthers", length+extraRadiusForTouch, BodyContacts, allowSelf:false, localOnly:true);
                AddReceiver(obj, Vector3.zero, name + "/PenSelf", "PenSelf", length, new []{CONTACT_ORF_MAIN}, allowOthers:false, localOnly:true);
                AddReceiver(obj, Vector3.zero, name + "/PenOthers", "PenOthers", length, new []{CONTACT_ORF_MAIN}, allowSelf:false, localOnly:true);
                AddReceiver(obj, Vector3.zero, name + "/FrotOthers", "FrotOthers", length, new []{CONTACT_PEN_CLOSE}, allowSelf:false, localOnly:true);
                AddReceiver(obj, tightPos, name + "/FrotOthersClose", "FrotOthersClose", radius+extraRadiusForFrot, new []{CONTACT_PEN_CLOSE}, allowSelf:false, localOnly:true, rotation: tightRot, height: length, type: ContactReceiver.ReceiverType.Constant);

                // Version Contacts
                var versionLocalTag = RandomTag();
                var versionBeaconTag = "OGB_VERSION_" + beaconVersion;
                AddSender(obj, Vector3.zero, "Version", 0.01f, versionLocalTag);
                // The "TPS_" + versionTag one is there so that the TPS wizard will delete this version flag if someone runs it
                AddReceiver(obj, Vector3.one * 0.01f, name + "/Version/" + penVersion, "Version", 0.01f, new []{versionLocalTag, "TPS_" + RandomTag()}, allowOthers:false, localOnly:true);
                AddSender(obj, Vector3.zero, "VersionBeacon", 1f, versionBeaconTag);
                AddReceiver(obj, Vector3.zero, name + "/VersionMatch", "VersionBeacon", 1f, new []{versionBeaconTag, "TPS_" + RandomTag()}, allowSelf:false, localOnly:true);

                addedOGB.Add(path);
            }

            foreach (var pair in getOrificeLights(avatarObject)) {
                var obj = pair.Item1;
                var forward = pair.Item2;
                var path = AnimationUtility.CalculateTransformPath(obj.transform, avatarObject.transform);

                Debug.Log("Found DPS orifice: " + obj);

                // Senders
                AddSender(obj, Vector3.zero, "Root", 0.01f, CONTACT_ORF_MAIN);
                AddSender(obj, forward * 0.01f, "Front", 0.01f, CONTACT_ORF_NORM);

                // Receivers
                var oscDepth = 0.25f;
                var tightRot = Quaternion.LookRotation(forward) * Quaternion.LookRotation(Vector3.up);
                var frotRadius = 0.1f;
                var frotPos = 0.05f;
                var closeRadius = 0.1f;
                var name = getNextName("OGB/Orf/" + obj.name.Replace('/','_'));
                AddReceiver(obj, forward * -oscDepth, name + "/TouchSelf", "TouchSelf", oscDepth, SelfContacts, allowOthers:false, localOnly:true);
                AddReceiver(obj, forward * -(oscDepth/2), name + "/TouchSelfClose", "TouchSelfClose", closeRadius, SelfContacts, allowOthers:false, localOnly:true, height: oscDepth, rotation: tightRot, type: ContactReceiver.ReceiverType.Constant);
                AddReceiver(obj, forward * -oscDepth, name + "/TouchOthers", "TouchOthers", oscDepth, BodyContacts, allowSelf:false, localOnly:true);
                AddReceiver(obj, forward * -(oscDepth/2), name + "/TouchOthersClose", "TouchOthersClose", closeRadius, BodyContacts, allowSelf:false, localOnly:true, height: oscDepth, rotation: tightRot, type: ContactReceiver.ReceiverType.Constant);
                AddReceiver(obj, Vector3.zero, name + "/PenSelfNewRoot", "PenSelfNewRoot", 1f, new []{CONTACT_PEN_ROOT}, allowOthers:false, localOnly:true);
                AddReceiver(obj, Vector3.zero, name + "/PenSelfNewTip", "PenSelfNewTip", 1f, new []{CONTACT_PEN_MAIN}, allowOthers:false, localOnly:true);
                AddReceiver(obj, forward * -oscDepth, name + "/PenOthers", "PenOthers", oscDepth, new []{CONTACT_PEN_MAIN}, allowSelf:false, localOnly:true);
                AddReceiver(obj, forward * -(oscDepth/2), name + "/PenOthersClose", "PenOthersClose", closeRadius, new []{CONTACT_PEN_MAIN}, allowSelf:false, localOnly:true, height: oscDepth, rotation: tightRot, type: ContactReceiver.ReceiverType.Constant);
                AddReceiver(obj, Vector3.zero, name + "/PenOthersNewRoot", "PenOthersNewRoot", 1f, new []{CONTACT_PEN_ROOT}, allowSelf:false, localOnly:true);
                AddReceiver(obj, Vector3.zero, name + "/PenOthersNewTip", "PenOthersNewTip", 1f, new []{CONTACT_PEN_MAIN}, allowSelf:false, localOnly:true);
                AddReceiver(obj, forward * frotPos, name + "/FrotOthers", "FrotOthers", frotRadius, new []{CONTACT_ORF_MAIN}, allowSelf:false, localOnly:true);

                // Version Contacts
                var versionLocalTag = RandomTag();
                var versionBeaconTag = "OGB_VERSION_" + beaconVersion;
                AddSender(obj, Vector3.zero, "Version", 0.01f, versionLocalTag);
                // The "TPS_" + versionTag one is there so that the TPS wizard will delete this version flag if someone runs it
                AddReceiver(obj, Vector3.one * 0.01f, name + "/Version/" + orfVersion, "Version", 0.01f, new []{versionLocalTag, "TPS_" + RandomTag()}, allowOthers:false, localOnly:true);
                AddSender(obj, Vector3.zero, "VersionBeacon", 1f, versionBeaconTag);
                AddReceiver(obj, Vector3.zero, name + "/VersionMatch", "VersionBeacon", 1f, new []{versionBeaconTag, "TPS_" + RandomTag()}, allowSelf:false, localOnly:true);

                addedOGB.Add(path);
            }

            if (addedOGB.Count == 0) {
                return "VRCFury failed to find any parts to upgrade! Ask on the discord?";
            }

            return "VRCFury upgraded these objects with OscGB support:\n" + String.Join("\n", addedOGB);
        }

        private static readonly string[] SelfContacts = {
            "Hand",
            "Finger",
            "Foot"
        };
        private static readonly string[] BodyContacts = {
            "Head",
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
            Quaternion rotation = default,
            bool worldScale = true
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
            if (worldScale) {
                sender.position /= child.transform.lossyScale.x;
                sender.radius /= child.transform.lossyScale.x;
                sender.height /= child.transform.lossyScale.x;
            }
        }

        private static void AddReceiver(
            GameObject obj,
            Vector3 pos,
            String param,
            String objName,
            float radius,
            string[] tags,
            bool allowOthers = true,
            bool allowSelf = true,
            bool localOnly = false,
            float height = 0,
            Quaternion rotation = default,
            ContactReceiver.ReceiverType type = ContactReceiver.ReceiverType.Proximity,
            bool worldScale = true
        ) {
            var child = new GameObject();
            child.name = "OGB_Receiver_" + objName;
            child.transform.SetParent(obj.transform, false);
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
            if (worldScale) {
                receiver.position /= child.transform.lossyScale.x;
                receiver.radius /= child.transform.lossyScale.x;
                receiver.height /= child.transform.lossyScale.x;
            }
        }
        
        private static List<Tuple<GameObject,Mesh>> getPenetrators(GameObject avatarObject) {
            var skins = new List<Tuple<GameObject,Mesh>>();
            bool materialIsDps(Material mat) {
                if (mat == null) return false;
                if (!mat.shader) return false;
                if (mat.shader.name == "Raliv/Penetrator") return true; // Raliv
                if (mat.shader.name.Contains("XSToon") && mat.shader.name.Contains("Penetrator")) return true; // XSToon w/ Raliv
                if (mat.HasProperty("_PenetratorEnabled") && mat.GetFloat("_PenetratorEnabled") > 0) return true; // Poiyomi 7 w/ Raliv
                if (mat.shader.name.Contains("DPS") && mat.HasProperty("_ReCurvature")) return true; // UnityChanToonShader w/ DPS
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
                var hasMarker = skin.transform.Find(MARKER_PEN) != null;
                if (isDps || isTps || hasMarker) {
                    Mesh mesh;
                    
                    // If the skinned mesh doesn't have any bones attached, it's treated like a regular mesh and BakeMesh applies no transforms
                    // So we have to skip calling BakeMesh, because otherwise we'd apply the inverse scale inappropriately and it would be too small.
                    bool actuallySkinned = skin.bones.Any(b => b != null);
                    if (actuallySkinned) {
                        var temporaryMesh = new Mesh();
                        skin.BakeMesh(temporaryMesh);
                        var verts = temporaryMesh.vertices;
                        var scale = skin.transform.lossyScale;
                        var inverseScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
                        for (var i = 0; i < verts.Length; i++) {
                            verts[i].Scale(inverseScale);
                        }
                        temporaryMesh.vertices = verts;
                        mesh = temporaryMesh;
                    } else {
                        mesh = skin.sharedMesh;
                    }
                    skins.Add(Tuple.Create(skin.gameObject, mesh));
                }
            }
            foreach (var renderer in avatarObject.GetComponentsInChildren<MeshRenderer>(true)) {
                var isDps = renderer.sharedMaterials.Any(materialIsDps);
                var isTps = renderer.sharedMaterials.Any(materialIsTps);
                var hasMarker = renderer.transform.Find(MARKER_PEN) != null;
                if (isDps || isTps || hasMarker) {
                    var meshFilter = renderer.GetComponent<MeshFilter>();
                    if (meshFilter) {
                        skins.Add(Tuple.Create(renderer.gameObject, meshFilter.sharedMesh));
                    }
                }
            }
            return skins;
        }

        private static readonly System.Random rand = new System.Random();

        private static string RandomTag() {
            return "TPSVF_" + rand.Next(100_000_000, 999_999_999);
        }

        private static List<Tuple<GameObject,Vector3>> getOrificeLights(GameObject avatarObject) {
            var pairs = new List<Tuple<GameObject,Vector3>>();
            var addedObjs = new HashSet<GameObject>();
            foreach (var light in avatarObject.GetComponentsInChildren<Light>(true)) {
                if (light.range % 0.1 >= 0.005f && light.range % 0.1 <= 0.025f) {
                    Light normal = null;
                    foreach (Transform sibling in light.gameObject.transform.parent) {
                        var siblingLight = sibling.gameObject.GetComponent<Light>();
                        if (siblingLight != null && siblingLight.range % 0.1 >= 0.045f && siblingLight.range % 0.1 <= 0.055f) {
                            normal = siblingLight;
                        }
                    }
                    
                    var obj = light.transform.parent.gameObject;
                    var forward = Vector3.forward;
                    if (normal != null) {
                        forward = (normal.transform.localPosition - light.transform.localPosition).normalized;
                    }
                    
                    addedObjs.Add(obj);
                    pairs.Add(Tuple.Create(obj, forward));
                }
            }

            foreach (var transform in avatarObject.GetComponentsInChildren<Transform>(true)) {
                if (transform.gameObject.name == MARKER_ORF) {
                    var parent = transform.parent.gameObject;
                    if (!addedObjs.Contains(parent)) {
                        addedObjs.Add(parent);
                        pairs.Add(Tuple.Create(parent, Vector3.forward));
                    }
                }
            }

            return pairs;
        }
    }
}
