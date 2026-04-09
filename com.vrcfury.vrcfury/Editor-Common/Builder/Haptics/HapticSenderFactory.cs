using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using VF.Builder;
using VF.Inspector;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Service {
    internal static class HapticSenderFactory {
        public class SenderRequest {
            public VFGameObject obj;
            public Vector3 pos;
            public string objName;
            public float radius;
            public string[] tags;
            public float height = 0;
            public Quaternion rotation = default;
            public bool worldScale = true;
            public bool useHipAvoidance = true;
        }

        [CanBeNull]
        public static VFGameObject AddSender(SenderRequest req) {
            var child = GameObjects.Create(req.objName, req.obj);
            if (!BuildTargetUtils.IsDesktop()) return null;
            var sender = child.AddComponent<VRCContactSender>();
            sender.position = req.pos;
            sender.radius = req.radius;
            if (req.height > 0) {
                sender.shapeType = ContactBase.ShapeType.Capsule;
                sender.height = req.height;
                sender.rotation = req.rotation;
            }
            if (req.worldScale) {
                sender.position /= child.worldScale.x;
                sender.radius /= child.worldScale.x;
                sender.height /= child.worldScale.x;
            }

            var tags = req.tags;
            if (VRCFuryHapticSocketEditor.getClosestBone?.Invoke(req.obj) != HumanBodyBones.Hips || !req.useHipAvoidance) {
                tags = AddSuffixes(tags, "", "_SelfNotOnHips");
            }
            sender.collisionTags = tags.ToList();
            return child;
        }

        public static string[] AddSuffixes(string[] tags, params string[] suffixes) {
            return tags.SelectMany(tag => {
                if (!tag.StartsWith("SPSLL_") && !tag.StartsWith("SPS_") && !tag.StartsWith("TPS_")) return new [] { tag };
                return suffixes.Select(suffix => tag + suffix);
            }).ToArray();
        }
    }
}
