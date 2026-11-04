using System;
using System.Collections.Generic;
using System.Linq;
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
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly OverlappingContactsFixService overlappingService;
        [VFAutowired] private readonly OgbEnabledService ogbEnabled;


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
            public bool enableWithOgb;
            public float height = 0;
            public Vector3? size;
            public bool useFaceProximity;
            public Quaternion rotation = default;
            public ContactReceiver.ReceiverType type = ContactReceiver.ReceiverType.Proximity;
            public bool worldScale = true;

            public ReceiverRequest Clone() {
                return (ReceiverRequest)MemberwiseClone();
            }
        }

        public VFAParam AddReceiver(ReceiverRequest req) {
            var fx = controllers.GetFx();
            if (!BuildTargetUtils.IsDesktop()) return fx.Zero();

            var param = req.usePrefix
                ? fx.CreateUniqueFloat(req.paramName)
                : fx.GetOrCreateFloat(req.paramName);
            var child = GameObjects.Create(req.objName, req.obj);
            
            overlappingService.Activate();
            var receiver = child.AddComponent<VRCContactReceiver>();
            receiver.position = req.pos;
            receiver.parameter = param;
            receiver.radius = req.radius;
            receiver.receiverType = req.type;
            receiver.collisionTags = new List<string>(req.tags);
            receiver.allowOthers = req.party == HapticUtils.ReceiverParty.Others;
            receiver.allowSelf = req.party == HapticUtils.ReceiverParty.Self;
            receiver.localOnly = req.localOnly;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            receiver.advancedParameters = ContactReceiver.AdvancedParameter.SenderVelocity;
#endif
            if (req.size.HasValue) {
#if VRCSDK_HAS_ADVANCED_CONTACT_PARAMETERS
                receiver.shapeType = ContactBase.ShapeType.Box;
                receiver.size = req.size.Value;
                receiver.rotation = req.rotation;
                receiver.useFaceProximity = req.useFaceProximity;
#else
                throw new InvalidOperationException("Box contact receivers require VRCSDK 3.10.6 or newer.");
#endif
            } else if (req.height > 0) {
                receiver.shapeType = ContactBase.ShapeType.Capsule;
                receiver.height = req.height;
                receiver.rotation = req.rotation;
            }
            if (req.worldScale) {
                receiver.position /= child.worldScale.x;
                receiver.radius /= child.worldScale.x;
                receiver.height /= child.worldScale.x;
#if VRCSDK_HAS_ADVANCED_CONTACT_PARAMETERS
                receiver.size /= child.worldScale.x;
#endif
            }

            if (req.enableWithOgb) ogbEnabled.Register(child);
            return param;
        }
    }
}
