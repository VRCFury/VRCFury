using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Component;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.Dynamics;
using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
using AdvancedParameter = VRC.Dynamics.ContactReceiver.AdvancedParameter;
#endif

namespace VF.Service {
    [VFService]
    internal class SelfAndOthersReceiverAnimatorService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly ObjectTreeService objectTrees;
        private readonly HashSet<SelfAndOthersReceiverService.Receivers> selectors = new();

        private readonly Dictionary<string, VFAParam> nativeParameters = new();

        // Classification can settle a frame later; keep all-channel tag checks out of each measurement branch.
        public BlendtreeMath.Condition GetNative(SelfAndOthersReceiverService.Receivers main, Party party) {
            main.request.channels.CheckOpen();
            if (party == Party.Self) return BlendtreeMath.True();
            if (party != Party.Others && party != Party.Merged) throw new ArgumentOutOfRangeException(nameof(party));
            var data = main.request.channels;
            if (!main.request.supportsLegacy) return BlendtreeMath.True();
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            var source = GetParameter(main, party, advancedParameters: AdvancedParameter.CollisionTags);
            var parameter = nativeParameters.GetOrCreate(source.name, () => {
                var fx = controllers.GetFx();
                var output = fx.CreateUniqueFloat(source.name + "/Native");
                data.OnAllSetsAvailable(sets => {
                    if (!sets.Any(s => s.isPublic)) {
                        output.SetDefault(1);
                        return;
                    }
                    var tree = GetTree(main);
                    var tags = sets.SelectMany(s => party == Party.Merged ? s.self.Union(s.others)
                        : s.others).Distinct();
                    tree.Add(output.MakeSetter(0));
                    foreach (var tag in tags) {
                        var formatted = data.component is VRCFuryHapticPlug ? SpsTags.FormatSocket(tag) : SpsTags.FormatPlug(tag);
                        var input = fx.GetOrCreateFloat(source.name + ContactReceiver.PARAM_COLLISION_TAG_PREFIX + formatted);
                        tree.Add(input, output.MakeSetter(1));
                    }
                });
                return output;
            });
            return BlendtreeMath.GreaterThan(parameter, 0);
#else
            throw new Exception(VF.Exceptions.VRCFExceptionUtils.NewerVrcsdkRequired);
#endif
        }

        public VFBlendTreeDirect GetTree(SelfAndOthersReceiverService.Receivers receivers) =>
            objectTrees.Get(receivers.request.frame);

        private VFAParam RegisterParameter(SelfAndOthersReceiverService.Receiver receiver, string suggestedName) {
            var fx = controllers.GetFx();
            if (string.IsNullOrEmpty(receiver.parameter)) receiver.parameter = fx.CreateUniqueFloat(suggestedName);
            return fx.GetOrCreateFloat(receiver.parameter);
        }

        public void RegisterOgbDepth(SelfAndOthersReceiverService.Receivers receivers, string prefix) {
            foreach (var party in new[] { Party.Self, Party.Others }) {
                GetParameter(
                    receivers,
                    party,
                    prefix + (party == Party.Self ? "/DepthSelf" : "/DepthOthers"),
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                    advancedParameters: AdvancedParameter.All,
#endif
                    localOnly: true
                );
            }
        }

        public VFAParam GetParameter(
            SelfAndOthersReceiverService.Receivers receivers,
            Party party,
            string parameterName = null,
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            AdvancedParameter advancedParameters = 0,
            string suffix = null,
#endif
            bool localOnly = false
        ) {
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            if (suffix != null) advancedParameters |= VRCContactReceiverExtensions.GetAdvancedParameterFlag(suffix);
#endif
            VFAParam parameter;
            if (party == Party.Self || party == Party.Others) {
                var self = party == Party.Self;
                var receiver = receivers.Find(self, false, preferParameter: true, parameterName: parameterName, localOnly: localOnly);
                // Explicit names are public outputs (for example OGB)
                // Never rename an existing output already used by another consumer
                if (parameterName != null) receiver.parameter = parameterName;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                receiver.advancedParameters |= advancedParameters;
#endif
                parameter = RegisterParameter(receiver, receivers.request.name + (self ? "/Self" : "/Others"));
            } else {
                if (party != Party.Merged) throw new ArgumentOutOfRangeException(nameof(party));
                if (parameterName != null) throw new ArgumentException("Merged contact parameters cannot have an explicit name.", nameof(parameterName));
                if (localOnly) throw new ArgumentException("Merged contact parameters cannot be local-only.", nameof(localOnly));
                parameter = GetMergedParameter(receivers
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                    , advancedParameters
#endif
                );
            }
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            if (suffix != null) return parameter.Controller.GetOrCreateFloat(parameter.name + suffix);
#endif
            return parameter;
        }

        private VFAParam GetMergedParameter(
            SelfAndOthersReceiverService.Receivers receivers
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            , AdvancedParameter advancedParameters = 0
#endif
        ) {
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            var mergedSelf = receivers.Find(true, true);
            var mergedOthers = receivers.Find(false, true);
            mergedSelf.advancedParameters |= advancedParameters;
            mergedOthers.advancedParameters |= advancedParameters;
            var mergedParameter = RegisterParameter(mergedSelf, receivers.request.name + "/Merged");
            mergedOthers.parameter = mergedParameter;
            RegisterParameter(mergedOthers, receivers.request.name + "/Merged");
            if (selectors.Add(receivers)) {
                GetTree(receivers).Add(CreateSelector(receivers, mergedSelf, mergedOthers));
            }
            return mergedParameter;
#else
            throw new InvalidOperationException(VF.Exceptions.VRCFExceptionUtils.NewerVrcsdkRequired);
#endif
        }

#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
        private VFMotion CreateSelector(
            SelfAndOthersReceiverService.Receivers receivers,
            SelfAndOthersReceiverService.Receiver mergedSelf,
            SelfAndOthersReceiverService.Receiver mergedOthers
        ) {
            VFClip Select(string name, bool self = false, bool others = false) {
                var clip = VFClip.Create(receivers.request.name + " " + name);
                clip.SetEnabled(mergedSelf.obj, self);
                clip.SetEnabled(mergedOthers.obj, others);
                return clip;
            }
            var chooseSelf = Select("select self", self: true);
            var chooseOthers = Select("select others", others: true);
            var chooseNeither = Select("no contact");
            var selfDistance = GetParameter(receivers, Party.Self, suffix: ContactReceiver.PARAM_POINT_SEPARATION);
            var othersDistance = GetParameter(receivers, Party.Others, suffix: ContactReceiver.PARAM_POINT_SEPARATION);

            // Point separation is normally -1 with no contact, but can start at zero.
            var closest = VFBlendTree2D.CreateFreeformCartesian(
                receivers.request.name + " closest contact",
                othersDistance,
                selfDistance
            );
            // Keep the interpolation between stops far below any measurable contact distance.
            const float step = 1e-15f;
            const float right = 1.5f * step;
            const float top = step * 1.0000002f;
            closest.Add(Vector2.zero, chooseNeither);
            // On either axis only the sender with positive separation is present.
            closest.Add(new Vector2(right, 0), chooseOthers);
            closest.Add(new Vector2(0, top), chooseSelf);
            // Inside the quadrant the smaller distance wins. Bias the diagonal tie toward Others.
            closest.Add(new Vector2(right, 0.5f * step), chooseSelf);
            closest.Add(new Vector2(step, top), chooseOthers);
            return closest;
        }
#endif
    }
}
