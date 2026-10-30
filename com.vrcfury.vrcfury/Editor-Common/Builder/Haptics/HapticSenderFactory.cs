using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using VF.Builder;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Builder.Haptics {
    internal static class HapticSenderFactory {
        public class SenderRequest {
            public VFGameObject obj;
            public Vector3 pos;
            public string objName;
            public float radius;
            public string[] tags;
            public float height = 0;
            public Quaternion rotation = default;
            public bool useWorldUnits = true;
            public Vector3? worldScale;
        }

        [CanBeNull]
        public static VFGameObject AddSender(SenderRequest req) {
            var child = GameObjects.Create(req.objName, req.obj);
            if (!BuildTargetUtils.IsDesktop()) return null;
            if (req.worldScale.HasValue) child.worldScale = req.worldScale.Value;
            var sender = child.AddComponent<VRCContactSender>();
            sender.position = req.pos;
            sender.radius = req.radius;
            if (req.height > 0) {
                sender.shapeType = ContactBase.ShapeType.Capsule;
                sender.height = req.height;
                sender.rotation = req.rotation;
            }
            if (req.useWorldUnits) {
                var scale = child.worldScale;
                sender.position = new Vector3(sender.position.x / scale.x,
                    sender.position.y / scale.y, sender.position.z / scale.z);
                var shapeScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                sender.radius /= shapeScale;
                sender.height /= shapeScale;
            }

            sender.collisionTags = req.tags.ToList();
            return child;
        }

    }
}
