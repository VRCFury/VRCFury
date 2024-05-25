using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Service {
    [VFService]
    public class HapticContactsService {
        [VFAutowired] [CanBeNull] private readonly AvatarManager manager;
        [VFAutowired] [CanBeNull] private readonly MathService math;

        public void AddSender(
            VFGameObject obj,
            Vector3 pos,
            string objName,
            float radius,
            string[] tags,
            float height = 0,
            Quaternion rotation = default,
            bool worldScale = true,
            bool useHipAvoidance = true
        ) {
            var child = GameObjects.Create(objName, obj);
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) return;
            var sender = child.AddComponent<VRCContactSender>();
            sender.position = pos;
            sender.radius = radius;
            if (height > 0) {
                sender.shapeType = ContactBase.ShapeType.Capsule;
                sender.height = height;
                sender.rotation = rotation;
            }
            if (worldScale) {
                sender.position /= child.worldScale.x;
                sender.radius /= child.worldScale.x;
                sender.height /= child.worldScale.x;
            }
            
            void SetTags(params string[] suffixes) {
                sender.collisionTags = tags.SelectMany(tag => {
                    if (!tag.StartsWith("SPSLL_") && !tag.StartsWith("SPS_") && !tag.StartsWith("TPS_")) return new [] { tag };
                    return suffixes.Select(suffix => tag + suffix);
                }).ToList();
            }
            SetTags("");

            if (ClosestBoneUtils.GetClosestHumanoidBone(obj) != HumanBodyBones.Hips || !useHipAvoidance) {
                SetTags("", "_SelfNotOnHips");
            }
        }

        public VFAFloat AddReceiver(
            VFGameObject obj,
            Vector3 pos,
            string paramName,
            string objName,
            float radius,
            string[] tags,
            HapticUtils.ReceiverParty party,
            bool usePrefix = true,
            bool localOnly = false,
            float height = 0,
            Quaternion rotation = default,
            ContactReceiver.ReceiverType type = ContactReceiver.ReceiverType.Proximity,
            bool worldScale = true,
            bool useHipAvoidance = true
        ) {
            if (manager == null || math == null) {
                throw new Exception("Receiver cannot be created in detached mode");
            }

            var fx = manager.GetFx();
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) return fx.Zero();

            if (party == HapticUtils.ReceiverParty.Both) {
                if (!usePrefix) throw new Exception("Cannot create a 'Both' receiver without param prefix");
                var others = AddReceiver(obj, pos, $"{paramName}/Others", $"{objName}Others", radius, tags, HapticUtils.ReceiverParty.Others, true, localOnly, height, rotation, type, worldScale, useHipAvoidance);
                var self = AddReceiver(obj, pos, $"{paramName}/Self", $"{objName}Self", radius, tags, HapticUtils.ReceiverParty.Self, true, localOnly, height, rotation, type, worldScale, useHipAvoidance);
                return math.Max(others, self, $"{paramName}/Both");
            }

            var param = fx.NewFloat(paramName, usePrefix: usePrefix);
            var child = GameObjects.Create(objName, obj);
            var receiver = child.AddComponent<VRCContactReceiver>();
            receiver.position = pos;
            receiver.parameter = param.Name();
            receiver.radius = radius;
            receiver.receiverType = type;
            receiver.collisionTags = new List<string>(tags);
            receiver.allowOthers = party == HapticUtils.ReceiverParty.Others;
            receiver.allowSelf = party == HapticUtils.ReceiverParty.Self;
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

            void SetTags(params string[] suffixes) {
                receiver.collisionTags = tags.SelectMany(tag => {
                    if (!tag.StartsWith("SPSLL_") && !tag.StartsWith("SPS_") && !tag.StartsWith("TPS_")) return new [] { tag };
                    return suffixes.Select(suffix => tag + suffix);
                }).ToList();
            }
            SetTags("");
            if (party == HapticUtils.ReceiverParty.Self && useHipAvoidance && ClosestBoneUtils.GetClosestHumanoidBone(obj) == HumanBodyBones.Hips) {
                SetTags("_SelfNotOnHips");
            }

            return param;
        }
    }
}
