using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Injector;
using VF.Inspector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Dynamics.Contact.Components;
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

        public VFBlendTreeDirect GetTree(SelfAndOthersReceiverService.Receivers receivers) =>
            objectTrees.Get(receivers.request.animatorRoot ?? receivers.request.frame);

        private VFAParam RegisterParameter(VRCContactReceiver receiver, string suggestedName) {
            overlapping.Activate();
            var fx = controllers.GetFx();
            if (string.IsNullOrEmpty(receiver.parameter)) receiver.parameter = fx.CreateUniqueFloat(suggestedName);
            foreach (var name in ContactReceiverUtils.GetParameterNames(receiver)) fx.GetOrCreateFloat(name);
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
            var flag = ContactReceiverUtils.GetAdvancedParameterFlag(suffix);
            var parameter = GetParameter(receivers, party, advancedParameters: flag);
            return parameter.Controller.GetOrCreateFloat(parameter.name + fullSuffix);
#else
            throw new InvalidOperationException("Advanced contact parameters require VRCSDK 3.10.6 or newer.");
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
            VRCContactReceiver mergedSelf, VRCContactReceiver mergedOthers) {
            VFClip Select(bool selectSelf) {
                var clip = VFClip.Create(receivers.request.name + (selectSelf ? " select self" : " select others"));
                clip.SetEnabled(mergedSelf.owner(), selectSelf);
                clip.SetEnabled(mergedOthers.owner(), !selectSelf);
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
