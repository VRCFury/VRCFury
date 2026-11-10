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
            public string name;
            public Func<bool, bool, string> objectName;
            public float worldRadius;
            public bool supportsLegacy;
            public SpsChannelContacts channels;
            public Func<SpsChannelsService.ChannelSet, bool, IEnumerable<string>> getTags = (_, _) => Array.Empty<string>();
            public bool omitEmpty;
        }

        internal class Receiver {
            public readonly VFGameObject obj;
            public readonly bool allowSelf;
            public string parameter = "";
            public VRCContactReceiver component;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            public ContactReceiver.AdvancedParameter advancedParameters;
#endif
#if VRCSDK_HAS_MATERIAL_CONTACTS
            public string separateShaderParameter = "";
            public bool writeToMaterialsContactTags;
            public readonly List<Renderer> shaderTargetRenderers = new();
#endif
            public Receiver(VFGameObject obj, bool self) { this.obj = obj; allowSelf = self; }
        }

        // Consumers reserve receiver configuration; SDK contacts are built when channel sets are finalized.
        public Receivers Create(Request request) => new Receivers(request);

        public sealed class Receivers {
            internal readonly Request request;
            private readonly float localRadius;
            private readonly List<Receiver> alwaysOnReceivers = new();
            private readonly List<Receiver> mergeReceivers = new();
            public bool IsCreated => alwaysOnReceivers.Count > 0 || mergeReceivers.Count > 0;

            internal Receivers(Request request) {
                // Capture dimensions now, before post-bake actions or avatar resizing change the frame.
                localRadius = request.worldRadius / request.frame.worldScale.x;
                this.request = request;
            }

            internal Receiver Find(bool self, bool merged, bool preferParameter = false, string parameterName = null) {
                request.channels.CheckOpen();
                var receivers = merged ? mergeReceivers : alwaysOnReceivers;
                bool Matches(Receiver r) => r.allowSelf == self
                    && (parameterName == null || string.IsNullOrEmpty(r.parameter) || r.parameter == parameterName);
                return (preferParameter ? receivers.FirstOrDefault(r => Matches(r) && !string.IsNullOrEmpty(r.parameter)) : null)
                    ?? receivers.FirstOrDefault(Matches)
                    ?? Make(self, merged);
            }

            private Receiver Make(bool self, bool merged) {
                var list = merged ? mergeReceivers : alwaysOnReceivers;
                var name = request.objectName?.Invoke(self, merged)
                    ?? request.name + (merged ? "Merged" : "") + (self ? "Self" : "Others");
                var obj = GameObjects.Create(name + (list.Count < 2 ? "" : " " + list.Count), request.frame);
                var receiver = new Receiver(obj, self);
                // Ties select others, matching the comparison tree's false branch.
                obj.active = !merged || !self;
                list.Add(receiver);
                string[] Tags(SpsChannelsService.ChannelSet set) => request.getTags(set, self).Distinct().ToArray();
                request.channels.OnAllSetsAvailable(sets => {
                    if (request.omitEmpty && sets.All(s => Tags(s).Length == 0)) {
                        obj.Destroy();
                        return;
                    }
                    var variants = new Dictionary<string, VFGameObject>();
                    foreach (var set in sets) {
                        var tags = Tags(set);
                        if (tags.Length > 16) throw new ArgumentException("A contact receiver supports at most 16 collision tags.");
                        var owner = variants.GetOrCreate(string.Join("\n", tags), () => {
                            var owner = sets.Count == 1 ? obj : GameObjects.Create(
                                "Channel (" + string.Join(", ", (self ? set.self : set.others).DefaultIfEmpty("none")) + ")", obj);
                            var contact = owner.AddComponent<VRCContactReceiver>();
                            contact.shapeType = ContactBase.ShapeType.Sphere;
                            contact.radius = localRadius;
                            contact.receiverType = ContactReceiver.ReceiverType.Proximity;
                            contact.allowSelf = self;
                            contact.allowOthers = !self;
#if VRCSDK_HAS_CONTACT_CONTENT_TYPES
                            contact.contentTypes = self ? DynamicsUsageFlags.Avatar : DynamicsUsageFlags.Everything;
#endif
                            contact.collisionTags = tags.ToList();
                            contact.parameter = receiver.parameter;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                            contact.advancedParameters = receiver.advancedParameters;
#endif
#if VRCSDK_HAS_MATERIAL_CONTACTS
                            contact.writeToMaterials = receiver.shaderTargetRenderers.Count > 0;
                            contact.separateShaderParameter = receiver.separateShaderParameter;
                            contact.writeToMaterialsContactTags = receiver.writeToMaterialsContactTags;
                            contact.shaderTargetRenderers = receiver.shaderTargetRenderers.ToArray();
#endif
                            return owner;
                        });
                        if (set == request.channels.Default) receiver.component = owner.GetComponent<VRCContactReceiver>();
                        set.Register(owner);
                    }
                });
                return receiver;
            }

            public void SetMaterials(IEnumerable<Renderer> targets, bool self, bool needsContactTags, string shaderParameter) {
#if VRCSDK_HAS_MATERIAL_CONTACTS
                request.channels.CheckOpen();
                foreach (var renderer in targets.Where(r => r != null).Distinct()) {
                    bool Matches(Receiver r) => r.allowSelf == self
                        && (string.IsNullOrEmpty(r.separateShaderParameter) || r.separateShaderParameter == shaderParameter);
                    // Repeated consumers can request the same renderer without allocating again.
                    var receiver = alwaysOnReceivers.FirstOrDefault(r => Matches(r) && r.shaderTargetRenderers.Contains(renderer))
                        ?? alwaysOnReceivers.FirstOrDefault(r => Matches(r)
                            && r.shaderTargetRenderers.Count < ContactReceiver.SHADER_MAX_UPDATED_RENDERERS)
                        ?? Make(self, false);
                    receiver.separateShaderParameter = shaderParameter;
                    // A consumer that doesn't need tags can still share a writer with one that does.
                    receiver.writeToMaterialsContactTags |= needsContactTags;
                    if (!receiver.shaderTargetRenderers.Contains(renderer)) {
                        receiver.shaderTargetRenderers.Add(renderer);
                    }
                }
#else
                throw new InvalidOperationException("Material contact support is required.");
#endif
            }


        }
    }
}
