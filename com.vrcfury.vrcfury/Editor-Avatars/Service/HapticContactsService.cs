using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
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
    internal class HapticContactsService {
        [VFAutowired] [CanBeNull] private readonly ControllersService controllers;
        [VFAutowired] [CanBeNull] private readonly OverlappingContactsFixService overlappingService;

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
        public VFGameObject AddSender(SenderRequest req) {
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
            if (ClosestBoneUtils.GetClosestHumanoidBone(req.obj) != HumanBodyBones.Hips || !req.useHipAvoidance) {
                tags = AddSuffixes(tags, "", "_SelfNotOnHips");
            }
            sender.collisionTags = tags.ToList();
            return child;
        }

        public class ReceiverRequest {
            public VFGameObject obj;
            public Vector3 pos = Vector3.zero;
            public string paramName;
            public string objName;
            public float radius = 0;
            public string[] tags;
            public HapticUtils.ReceiverParty party;
            public bool usePrefix = true;
            public bool localOnly = false;
            public float height = 0;
            public Quaternion rotation = default;
            public ContactReceiver.ReceiverType type = ContactReceiver.ReceiverType.Proximity;
            public bool worldScale = true;
            public bool useHipAvoidance = true;

            public ReceiverRequest Clone() {
                return (ReceiverRequest)MemberwiseClone();
            }
        }

        public VFAFloat AddReceiver(ReceiverRequest req) {
            if (controllers == null) {
                throw new Exception("Receiver cannot be created in detached mode");
            }

            var fx = controllers.GetFx();
            if (!BuildTargetUtils.IsDesktop()) return fx.Zero();

            var param = fx.NewFloat(req.paramName, usePrefix: req.usePrefix);
            var child = GameObjects.Create(req.objName, req.obj);
            
            overlappingService?.Activate();
            var receiver = child.AddComponent<VRCContactReceiver>();
            receiver.position = req.pos;
            receiver.parameter = param;
            receiver.radius = req.radius;
            receiver.receiverType = req.type;
            receiver.collisionTags = new List<string>(req.tags);
            receiver.allowOthers = req.party == HapticUtils.ReceiverParty.Others;
            receiver.allowSelf = req.party == HapticUtils.ReceiverParty.Self;
            receiver.localOnly = req.localOnly;
            if (req.height > 0) {
                receiver.shapeType = ContactBase.ShapeType.Capsule;
                receiver.height = req.height;
                receiver.rotation = req.rotation;
            }
            if (req.worldScale) {
                receiver.position /= child.worldScale.x;
                receiver.radius /= child.worldScale.x;
                receiver.height /= child.worldScale.x;
            }

            var tags = req.tags;
            if (req.party == HapticUtils.ReceiverParty.Self && req.useHipAvoidance && ClosestBoneUtils.GetClosestHumanoidBone(req.obj) == HumanBodyBones.Hips) {
                tags = AddSuffixes(tags, "_SelfNotOnHips");
            }
            receiver.collisionTags = tags.ToList();

            return param;
        }

        private string[] AddSuffixes(string[] tags, params string[] suffixes) {
            return tags.SelectMany(tag => {
                if (!tag.StartsWith("SPSLL_") && !tag.StartsWith("SPS_") && !tag.StartsWith("TPS_")) return new [] { tag };
                return suffixes.Select(suffix => tag + suffix);
            }).ToArray();
        }
    }
}
