using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Injector;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Builder.Haptics {
    [VFService]
    internal class SelfAndOthersReceiverService {
        public enum Party { Self, Others, Merged }

        public class Request {
            public VFGameObject frame;
            public VFGameObject animatorRoot;
            public string name;
            public float worldRadius;
            public string[] selfTags = Array.Empty<string>();
            public string[] othersTags = Array.Empty<string>();
        }

        // Registration is cheap: neither receivers nor Animator parameters exist until requested.
        public Receivers Create(Request request) => new Receivers(request);

        public sealed class Receivers {
            internal readonly Request request;
            private readonly float localRadius;
            private readonly List<VRCContactReceiver> alwaysOnReceivers = new();
            private readonly List<VRCContactReceiver> mergeReceivers = new();
            public bool IsCreated => alwaysOnReceivers.Count > 0 || mergeReceivers.Count > 0;

            internal Receivers(Request request) {
                if (request.selfTags.Length > 16 || request.othersTags.Length > 16)
                    throw new ArgumentException("A contact receiver supports at most 16 collision tags.");
                // Capture dimensions now, before post-bake actions or avatar resizing change the frame.
                localRadius = request.worldRadius / request.frame.worldScale.x;
                this.request = request;
            }

            internal VRCContactReceiver Find(bool self, bool merged, bool preferParameter = false, string parameterName = null) {
                var receivers = merged ? mergeReceivers : alwaysOnReceivers;
                bool Matches(VRCContactReceiver r) => r.allowSelf == self && r.allowOthers != self
                    && (parameterName == null || string.IsNullOrEmpty(r.parameter) || r.parameter == parameterName);
                return (preferParameter ? receivers.FirstOrDefault(r => Matches(r) && !string.IsNullOrEmpty(r.parameter)) : null)
                    ?? receivers.FirstOrDefault(Matches)
                    ?? Make(self, merged);
            }

            private VRCContactReceiver Make(bool self, bool merged) {
                var list = merged ? mergeReceivers : alwaysOnReceivers;
                var obj = GameObjects.Create(request.name + (merged ? "Merged" : "")
                    + (self ? "Self" : "Others") + (list.Count < 2 ? "" : list.Count.ToString()), request.frame);
                var receiver = obj.AddComponent<VRCContactReceiver>();
                receiver.shapeType = ContactBase.ShapeType.Sphere;
                receiver.radius = localRadius;
                receiver.receiverType = ContactReceiver.ReceiverType.Proximity;
                receiver.allowSelf = self;
                receiver.allowOthers = !self;
#if VRCSDK_HAS_CONTACT_CONTENT_TYPES
                receiver.contentTypes = self ? DynamicsUsageFlags.Avatar : DynamicsUsageFlags.Everything;
#endif
                receiver.collisionTags = new List<string>(self ? request.selfTags : request.othersTags);
                receiver.parameter = "";
#if VRCSDK_HAS_MATERIAL_CONTACTS
                receiver.writeToMaterials = false;
                receiver.shaderTargetRenderers = Array.Empty<Renderer>();
#endif
                // Ties select others, matching the comparison tree's false branch.
                obj.active = !merged || !self;
                list.Add(receiver);
                return receiver;
            }

            public void SetMaterials(IEnumerable<Renderer> targets, bool self, bool needsContactTags, string shaderParameter) {
#if VRCSDK_HAS_MATERIAL_CONTACTS
                foreach (var renderer in targets.Where(r => r != null).Distinct()) {
                    bool Matches(VRCContactReceiver r) => r.allowSelf == self && r.allowOthers != self
                        && (string.IsNullOrEmpty(r.separateShaderParameter) || r.separateShaderParameter == shaderParameter);
                    // Repeated consumers can request the same renderer without allocating again.
                    var receiver = alwaysOnReceivers.FirstOrDefault(r => Matches(r) && r.shaderTargetRenderers.Contains(renderer))
                        ?? alwaysOnReceivers.FirstOrDefault(r => Matches(r)
                            && r.shaderTargetRenderers.Length < ContactReceiver.SHADER_MAX_UPDATED_RENDERERS)
                        ?? Make(self, false);
                    receiver.separateShaderParameter = shaderParameter;
                    receiver.writeToMaterials = true;
                    // A consumer that doesn't need tags can still share a writer with one that does.
                    receiver.writeToMaterialsContactTags |= needsContactTags;
                    if (!receiver.shaderTargetRenderers.Contains(renderer)) {
                        receiver.shaderTargetRenderers = receiver.shaderTargetRenderers.Append(renderer).ToArray();
                    }
                }
#else
                throw new InvalidOperationException("Material contact support is required.");
#endif
            }


        }
    }
}
