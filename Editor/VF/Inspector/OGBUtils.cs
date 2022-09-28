using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF.Model {
    public class OGBUtils {
        
        // Bump when pen senders or receivers are changed
        public static int penVersion = 8;
        
        // Bump when orf senders or receivers are changed
        public static int orfVersion = 9;
        
        // Bump when any senders are changed
        public static int beaconVersion = 6;
        
        public static string CONTACT_PEN_MAIN = "TPS_Pen_Penetrating";
        public static string CONTACT_PEN_WIDTH = "TPS_Pen_Width";
        public static string CONTACT_PEN_CLOSE = "TPS_Pen_Close";
        public static string CONTACT_PEN_ROOT = "TPS_Pen_Root";
        public static string CONTACT_ORF_MAIN = "TPS_Orf_Root";
        public static string CONTACT_ORF_NORM = "TPS_Orf_Norm";
        
        public static readonly string[] SelfContacts = {
            "Hand",
            "Finger",
            "Foot"
        };
        public static readonly string[] BodyContacts = {
            "Head",
            "Hand",
            "Foot",
            "Finger"
        };
        
        private static readonly System.Random rand = new System.Random();

        public static string RandomTag() {
            return "TPSVF_" + rand.Next(100_000_000, 999_999_999);
        }
        
        public static void AddSender(
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

        public static void AddVersionContacts(GameObject obj, string paramPrefix, bool baked, bool isPen) {
            // Version Local
            var varName = baked ? "BakedVersion" : "Version";
            var versionLocalTag = RandomTag();
            AddSender(obj, Vector3.zero, varName, 0.01f, versionLocalTag);
            // The "TPS_" + versionTag one is there so that the TPS wizard will delete this version flag if someone runs it
            var versionLocal = isPen ? penVersion : orfVersion;
            AddReceiver(obj, Vector3.one * 0.01f, paramPrefix + "/" + varName + "/" + versionLocal, varName, 0.01f, new []{versionLocalTag, "TPS_" + RandomTag()}, allowOthers:false, localOnly:true);

            // Version Remote
            var versionBeaconTag = "OGB_VERSION_" + beaconVersion;
            AddSender(obj, Vector3.zero, "VersionBeacon", 1f, versionBeaconTag);
            if (!baked) {
                AddReceiver(obj, Vector3.zero, paramPrefix + "/VersionMatch", "VersionBeacon", 1f,
                    new[] { versionBeaconTag, "TPS_" + RandomTag() }, allowSelf: false, localOnly: true);
            }
        }

        public static void AddReceiver(
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

        public static void RemoveTPSSenders(GameObject obj) {
            var remove = new List<Component>();
            foreach (Transform child in obj.transform) {
                foreach (var sender in child.gameObject.GetComponents<VRCContactSender>()) {
                    if (IsTPSSender(sender)) {
                        Debug.Log("Deleting OG TPS sender on " + sender.gameObject);
                        remove.Add(sender);
                    }
                }
            }

            foreach (var c in remove) {
                RemoveComponent(c);
            }

            if (obj.transform.parent) {
                RemoveTPSSenders(obj.transform.parent.gameObject);
            }
        }

        public static bool IsTPSSender(VRCContactSender c) {
            if (c.collisionTags.Any(t => t == CONTACT_PEN_MAIN)) return true;
            if (c.collisionTags.Any(t => t == CONTACT_PEN_WIDTH)) return true;
            if (c.collisionTags.Any(t => t == CONTACT_ORF_MAIN)) return true;
            if (c.collisionTags.Any(t => t == CONTACT_ORF_NORM)) return true;
            return false;
        }

        public static void RemoveComponent(Component c) {
            if (c.gameObject.GetComponents<Component>().Length == 2 && !PrefabUtility.IsPartOfAnyPrefab(c.gameObject) && c.gameObject.transform.childCount == 0) Object.DestroyImmediate(c.gameObject);
            else Object.DestroyImmediate(c);
        }
        
        public static string GetNextName(List<string> usedNames, string prefix) {
            for (int i = 0; ; i++) {
                var next = prefix + (i == 0 ? "" : i+"");
                if (!usedNames.Contains(next)) {
                    usedNames.Add(next);
                    return next;
                }
            }
        }

        public static bool IsZeroScale(GameObject obj) {
            var scale = obj.transform.lossyScale;
            return scale.x == 0 || scale.y == 0 || scale.z == 0;
        }

        public static bool IsNegativeScale(GameObject obj) {
            var scale = obj.transform.lossyScale;
            return scale.x < 0 || scale.y < 0 || scale.z < 0;
        }
        public static bool IsNonUniformScale(GameObject obj) {
            var scale = obj.transform.lossyScale;
            return Math.Abs(scale.x - scale.y) / scale.x > 0.05
                   || Math.Abs(scale.x - scale.z) / scale.x > 0.05;
        }
        public static void AssertValidScale(GameObject obj, string type) {
            if (IsZeroScale(obj)) {
                throw new Exception(
                    "OGB " + type + " exists on object " + obj +
                    ", but the object has zero scale. This object must" +
                    " not be zero scale or size calculation will fail.");
            }
            if (IsNegativeScale(obj)) {
                throw new Exception(
                    "OGB " + type + " exists on object " + obj +
                    ", but the object has negative scale. This object must" +
                    " have a positive scale or size calculation will fail.");
            }
            if (IsNonUniformScale(obj)) {
                throw new Exception(
                    "OGB " + type + " exists on object " + obj +
                    ", but the object has a non-uniform scale. This object (and all parents) must" +
                    " have an X, Y, and Z scale value that match each other, or size calculation will fail.");
            }
        }
    }
}
