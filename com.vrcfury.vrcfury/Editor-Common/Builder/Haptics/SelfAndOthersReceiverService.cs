using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Injector;
using VF.Inspector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
using AdvancedParameter = VRC.Dynamics.ContactReceiver.AdvancedParameter;
#endif

namespace VF.Builder.Haptics {
    [VFService]
    internal class SelfAndOthersReceiverService {
        // Implemented by the avatar assembly; shader-only/world consumers don't need it.
        internal interface AnimatorSupport {
            VFAFloat GetParameter(VRCContactReceiver receiver, string suggestedName);
            void AddSelector(VFMotion selector);
        }
        [VFAutowired] [JetBrains.Annotations.CanBeNull] private readonly AnimatorSupport animator;
        public class Request {
            public VFGameObject frame;
            public string name;
            public float worldRadius;
            public string[] selfTags = Array.Empty<string>();
            public string[] othersTags = Array.Empty<string>();
        }

        // Registration is cheap: neither receivers nor Animator parameters exist until requested.
        public Receivers Create(Request request) => new Receivers(request, animator);

        public sealed class Receivers {
            private readonly Request request;
            private readonly AnimatorSupport animator;
            private readonly List<VRCContactReceiver> alwaysOnReceivers = new();
            private readonly List<VRCContactReceiver> mergeReceivers = new();
            private VFAFloat mergedParameter;
            public bool IsCreated => alwaysOnReceivers.Count > 0 || mergeReceivers.Count > 0;
            private VRCContactReceiver MergedSelf => Find(true, true);
            private VRCContactReceiver MergedOthers => Find(false, true);

            internal Receivers(Request request, AnimatorSupport animator) {
                this.animator = animator;
                if (request.selfTags.Length > 16 || request.othersTags.Length > 16)
                    throw new ArgumentException("A contact receiver supports at most 16 collision tags.");
                // Capture dimensions now, before post-bake actions or avatar resizing change the frame.
                this.request = new Request {
                    frame = request.frame, name = request.name,
                    worldRadius = request.worldRadius / request.frame.worldScale.x,
                    selfTags = request.selfTags.ToArray(), othersTags = request.othersTags.ToArray()
                };
            }

            private VRCContactReceiver Find(bool self, bool merged, bool preferParameter = false, string parameterName = null) {
                var receivers = merged ? mergeReceivers : alwaysOnReceivers;
                bool Matches(VRCContactReceiver r) => r.allowSelf == self && r.allowOthers != self
                    && (parameterName == null || string.IsNullOrEmpty(r.parameter) || r.parameter == parameterName);
                return (preferParameter ? receivers.FirstOrDefault(r => Matches(r) && !string.IsNullOrEmpty(r.parameter)) : null)
                    ?? receivers.FirstOrDefault(Matches)
                    ?? Make(self, merged);
            }

            public VFAFloat GetParameter(bool self, string parameterName = null
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                , AdvancedParameter advancedParameters = 0
#endif
            ) {
                if (animator == null) throw new InvalidOperationException("Animator receiver parameters require avatar support.");
                var receiver = Find(self, false, preferParameter: true, parameterName: parameterName);
                // Explicit names are public outputs (for example OGB)
                // Never rename an existing output already used by another consumer
                if (parameterName != null) receiver.parameter = parameterName;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                receiver.advancedParameters |= advancedParameters;
#endif
                return animator.GetParameter(receiver, request.name + (self ? "/Self" : "/Others"));
            }

            private VRCContactReceiver Make(bool self, bool merged) {
                var list = merged ? mergeReceivers : alwaysOnReceivers;
                var obj = GameObjects.Create(request.name + (merged ? "Merged" : "")
                    + (self ? "Self" : "Others") + (list.Count < 2 ? "" : list.Count.ToString()), request.frame);
                var receiver = obj.AddComponent<VRCContactReceiver>();
                receiver.shapeType = ContactBase.ShapeType.Sphere;
                receiver.radius = request.worldRadius;
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

            public VFAFloat GetMergedParameter(
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                AdvancedParameter advancedParameters = 0
#endif
            ) {
                if (animator == null) throw new InvalidOperationException("Animator receiver parameters require avatar support.");
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                MergedSelf.advancedParameters |= advancedParameters;
                MergedOthers.advancedParameters |= advancedParameters;
#endif
                if (mergedParameter != null) {
                    animator.GetParameter(MergedSelf, request.name + "/Merged");
                    animator.GetParameter(MergedOthers, request.name + "/Merged");
                    return mergedParameter;
                }
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                var self = GetParameter(true, advancedParameters: AdvancedParameter.PointSeparation);
                var others = GetParameter(false, advancedParameters: AdvancedParameter.PointSeparation);
#else
                var self = GetParameter(true);
                var others = GetParameter(false);
#endif
                mergedParameter = animator.GetParameter(MergedSelf, request.name + "/Merged");
                MergedOthers.parameter = mergedParameter;
                animator.GetParameter(MergedOthers, request.name + "/Merged");

                VFClip Select(bool selectSelf) {
                    var clip = VFClip.Create(request.name + (selectSelf ? " select self" : " select others"));
                    clip.SetEnabled(MergedSelf.owner(), selectSelf);
                    clip.SetEnabled(MergedOthers.owner(), !selectSelf);
                    return clip;
                }
                var chooseSelf = Select(true);
                var chooseOthers = Select(false);
                // A direct comparison of the two live proximity parameters. Only receiver enable
                // states are animated; the selected SDK receiver writes the merged value directly.
                VFMotion Compare(string name, string a, string b) {
                    var tree = VFBlendTree2D.CreateSimpleDirectional(request.name + name, a, b);
                    tree.Add(new Vector2(-10000, -10000), chooseOthers);
                    tree.Add(new Vector2(10000, 10000), chooseOthers);
                    tree.Add(Vector2.zero, chooseOthers);
                    tree.Add(new Vector2(-10000, 10000), chooseOthers);
                    tree.Add(new Vector2(0.000001f, -0.000001f), chooseSelf);
                    return tree;
                }
                VFMotion selector = Compare(" closest proximity", self, others);
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                // Proximity clamps at 1 inside a sender. Break that tie using the SDK's
                // unclamped inner-point distance, with smaller separation selecting self.
                var separation = Compare(" closest point separation", others.Name() + "_PointSeparation",
                    self.Name() + "_PointSeparation");
                var onlyOthersSaturated = VFBlendTree1D.CreateWithData(request.name + " only others saturated", others,
                    (VRCFuryEditorUtils.NextFloatDown(1), selector), (1, chooseOthers));
                var selfSaturated = VFBlendTree1D.CreateWithData(request.name + " both saturated", others,
                    (VRCFuryEditorUtils.NextFloatDown(1), chooseSelf), (1, separation));
                selector = VFBlendTree1D.CreateWithData(request.name + " self saturated", self,
                    (VRCFuryEditorUtils.NextFloatDown(1), onlyOthersSaturated), (1, selfSaturated));
#endif
                animator.AddSelector(selector);
                return mergedParameter;
            }
        }
    }
}
