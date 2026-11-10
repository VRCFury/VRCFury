using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Component;
using VF.Injector;
using VF.Inspector;
using VF.Utils;
using VF.Utils.Controller;
using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
using AdvancedParameter = VRC.Dynamics.ContactReceiver.AdvancedParameter;
#endif

namespace VF.Service {
    [VFService]
    internal class SelfAndOthersReceiverAnimatorService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly ObjectTreeService objectTrees;
        [VFAutowired] private readonly OverlappingContactsFixService overlapping;
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
                        var input = fx.GetOrCreateFloat(source.name + "_CollisionTag_" + formatted);
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
            overlapping.Activate();
            var fx = controllers.GetFx();
            if (string.IsNullOrEmpty(receiver.parameter)) receiver.parameter = fx.CreateUniqueFloat(suggestedName);
            return fx.GetOrCreateFloat(receiver.parameter);
        }

        public VFAParam GetParameter(SelfAndOthersReceiverService.Receivers receivers, bool self, string parameterName = null
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            , AdvancedParameter advancedParameters = 0
#endif
        ) {
            var receiver = receivers.Find(self, false, preferParameter: true, parameterName: parameterName);
            // Explicit names are public outputs (for example OGB)
            // Never rename an existing output already used by another consumer
            if (parameterName != null) receiver.parameter = parameterName;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            receiver.advancedParameters |= advancedParameters;
#endif
            return RegisterParameter(receiver, receivers.request.name + (self ? "/Self" : "/Others"));
        }

        public VFAParam GetParameter(SelfAndOthersReceiverService.Receivers receivers, Party party, string parameterName = null
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            , AdvancedParameter advancedParameters = 0
#endif
        ) {
            if (party == Party.Self || party == Party.Others) return GetParameter(receivers, party == Party.Self, parameterName
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                , advancedParameters
#endif
            );
            if (party != Party.Merged) throw new ArgumentOutOfRangeException(nameof(party));
            if (parameterName != null) throw new ArgumentException("Merged contact parameters cannot have an explicit name.", nameof(parameterName));
            return GetMergedParameter(receivers
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                , advancedParameters
#endif
            );
        }

        public VFAParam GetAdvancedParam(SelfAndOthersReceiverService.Receivers receivers, Party party, string suffix) {
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            var fullSuffix = "_" + suffix;
            var flag = VRCContactReceiverExtensions.GetAdvancedParameterFlag(suffix);
            var parameter = GetParameter(receivers, party, advancedParameters: flag);
            return parameter.Controller.GetOrCreateFloat(parameter.name + fullSuffix);
#else
            throw new InvalidOperationException(VF.Exceptions.VRCFExceptionUtils.NewerVrcsdkRequired);
#endif
        }

        private VFAParam GetMergedParameter(SelfAndOthersReceiverService.Receivers receivers
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            , AdvancedParameter advancedParameters = 0
#endif
        ) {
            var mergedSelf = receivers.Find(true, true);
            var mergedOthers = receivers.Find(false, true);
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            mergedSelf.advancedParameters |= advancedParameters;
            mergedOthers.advancedParameters |= advancedParameters;
#endif
            var mergedParameter = RegisterParameter(mergedSelf, receivers.request.name + "/Merged");
            mergedOthers.parameter = mergedParameter;
            RegisterParameter(mergedOthers, receivers.request.name + "/Merged");
            if (selectors.Add(receivers)) {
                GetTree(receivers).Add(CreateSelector(receivers, mergedSelf, mergedOthers));
            }
            return mergedParameter;
        }

        private VFMotion CreateSelector(SelfAndOthersReceiverService.Receivers receivers,
            SelfAndOthersReceiverService.Receiver mergedSelf, SelfAndOthersReceiverService.Receiver mergedOthers) {
            VFClip Select(bool selectSelf) {
                var clip = VFClip.Create(receivers.request.name + (selectSelf ? " select self" : " select others"));
                clip.SetEnabled(mergedSelf.obj, selectSelf);
                clip.SetEnabled(mergedOthers.obj, !selectSelf);
                return clip;
            }
            var chooseSelf = Select(true);
            var chooseOthers = Select(false);
            // Compare live SDK outputs directly. Only receiver enable
            // states are animated; the selected SDK receiver writes the merged value directly.
            VFMotion Compare(string name, string a, string b) {
                var tree = VFBlendTree2D.CreateSimpleDirectional(receivers.request.name + name, a, b);
                tree.Add(new Vector2(-10000, -10000), chooseOthers);
                tree.Add(new Vector2(10000, 10000), chooseOthers);
                tree.Add(Vector2.zero, chooseOthers);
                tree.Add(new Vector2(-10000, 10000), chooseOthers);
                tree.Add(new Vector2(0.000001f, -0.000001f), chooseSelf);
                return tree;
            }
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            var selfDistance = GetAdvancedParam(receivers, Party.Self, "PointSeparation");
            var othersDistance = GetAdvancedParam(receivers, Party.Others, "PointSeparation");
            var separation = Compare(" closest point separation", othersDistance, selfDistance);
            // Negative separation means no contact. Zero is valid; equal distances and
            // two missing contacts select others. No AAP or proximity conversion is needed.
            var othersPresent = VFBlendTree1D.CreateWithData(receivers.request.name + " others present", othersDistance,
                (VRCFuryEditorUtils.NextFloatDown(0), chooseSelf), (0, separation));
            VFMotion selector = VFBlendTree1D.CreateWithData(receivers.request.name + " self present", selfDistance,
                (VRCFuryEditorUtils.NextFloatDown(0), chooseOthers), (0, othersPresent));
#else
            VFMotion selector = Compare(" closest proximity", GetParameter(receivers, true), GetParameter(receivers, false));
#endif
            return selector;
        }
    }
}
