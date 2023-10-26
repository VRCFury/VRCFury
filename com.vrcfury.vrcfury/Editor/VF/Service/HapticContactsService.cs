using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Injector;
using VF.Utils.Controller;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Service {
    [VFService]
    public class HapticContactsService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly MathService math;

        public VFAFloat AddReceiver(
            Transform obj,
            Vector3 pos,
            String paramName,
            String objName,
            float radius,
            string[] tags,
            HapticUtils.ReceiverParty party,
            bool usePrefix = true,
            bool localOnly = false,
            float height = 0,
            Quaternion rotation = default,
            ContactReceiver.ReceiverType type = ContactReceiver.ReceiverType.Proximity,
            bool worldScale = true
        ) {
            var fx = manager.GetFx();
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) return fx.Zero();
            var isOnHips = HapticUtils.IsDirectChildOfHips(obj);

            if (party == HapticUtils.ReceiverParty.Both && isOnHips) {
                if (!usePrefix) throw new Exception("Cannot create a 'Both' receiver without param prefix");
                var others = AddReceiver(obj, pos, $"{paramName}/Others", $"{objName}Others", radius, tags, HapticUtils.ReceiverParty.Others, true, localOnly, height, rotation, type, worldScale);
                var self = AddReceiver(obj, pos, $"{paramName}/Self", $"{objName}Self", radius, tags, HapticUtils.ReceiverParty.Self, true, localOnly, height, rotation, type, worldScale);
                return math.Max(others, self);
            }

            var suffixes = new List<string>();
            if (party == HapticUtils.ReceiverParty.Others) {
                suffixes.Add("");
            } else if (party == HapticUtils.ReceiverParty.Self) {
                if (isOnHips) {
                    suffixes.Add("_SelfNotOnHips");
                } else {
                    suffixes.Add("");
                }
            }

            tags = tags.SelectMany(tag => {
                if (!tag.StartsWith("SPS_") && !tag.StartsWith("TPS_")) return new [] { tag };
                return suffixes.Select(suffix => tag + suffix);
            }).ToArray();

            var param = fx.NewFloat(paramName, usePrefix: usePrefix);
            var child = GameObjects.Create(objName, obj);
            var receiver = child.AddComponent<VRCContactReceiver>();
            receiver.position = pos;
            receiver.parameter = param.Name();
            receiver.radius = radius;
            receiver.receiverType = type;
            receiver.collisionTags = new List<string>(tags);
            receiver.allowOthers = party == HapticUtils.ReceiverParty.Others || party == HapticUtils.ReceiverParty.Both;
            receiver.allowSelf = party == HapticUtils.ReceiverParty.Self || party == HapticUtils.ReceiverParty.Both;
            receiver.localOnly = localOnly;
            if (height > 0) {
                receiver.shapeType = ContactBase.ShapeType.Capsule;
                receiver.height = height;
                receiver.rotation = rotation;
            }
            if (worldScale) {
                receiver.position /= child.worldScale.x;
                receiver.radius /= child.worldScale.x;
                receiver.height /= child.worldScale.x;
            }
            return param;
        }
    }
}
