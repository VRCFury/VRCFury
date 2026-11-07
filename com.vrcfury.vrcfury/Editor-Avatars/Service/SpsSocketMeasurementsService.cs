using static VF.Inspector.VRCFuryHapticPlugEditor;
using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
using System;
using System.Collections.Generic;
using System.Linq;
using VF.Builder.Haptics;
using VF.Component;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class SpsSocketMeasurementsService {
        [VFAutowired] private readonly SelfAndOthersReceiverAnimatorService receiverAnimator;
        [VFAutowired] private readonly SpsSocketReceiverService contacts;
        [VFAutowired] private readonly SpsConfigurer configurer;
        [VFAutowired] private readonly SpsOptionsService spsOptions;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly WorldScaleDetectorService worldScaleService;
        [VFAutowired] private readonly VF.Inspector.VRCFuryHapticSocketBaker socketBaker;

        private HashSet<string> GetChannels(VRCFuryHapticSocket socket, bool self) =>
            configurer.GetSocketChannels(socket, self
                ? SpsConfigurer.ChannelMode.ReceiverSelf : SpsConfigurer.ChannelMode.ReceiverOthers);

        public SelfAndOthersReceiverService.Receivers GetReceivers(VRCFuryHapticSocket socket, VFGameObject frame, string name) =>
            contacts.GetReceivers(socket, frame, name, spsOptions.GetOptions().legacyModeAvailable);

        public Func<Party, Func<string, VFAParam>> GetMeasurements(
            VRCFuryHapticSocket socket,
            SelfAndOthersReceiverService.Receivers main,
            string name
        ) {

#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS

            var fx = controllers.GetFx();
            var tree = receiverAnimator.GetTree(main);
            var math = new BlendtreeMath(fx, tree);
            var inverseWorldScale = new Lazy<VFAParam>(() => worldScaleService.GetInverseWorldScale(
                socket.owner(), name));
            var worldRadius = main.request.worldRadius;
            var includeLegacy = main.request.othersTags.Contains(SpsTags.LegacyPlugSphere);
            var entranceType = new Lazy<VRCFuryHapticSocket.AddLight>(() => socketBaker.GetEntranceType(socket));

            var byParty = new Dictionary<Party, Func<string, VFAParam>>();
            return party => byParty.GetOrCreate(party, () => {
                var self = party == Party.Self;
                // Local plugs always use SPS3, only remote contacts can need legacy decoding.
                var legacy = includeLegacy && !self;
                var prefix = name + "/" + party;
                var proximity = new Lazy<VFAParam>(() => receiverAnimator.GetParameter(main, party));
                VFAParam Field(string suffix) => receiverAnimator.GetAdvancedParam(main, party, suffix);
                var present = new Lazy<BlendtreeMath.Condition>(() => BlendtreeMath.GreaterThan(proximity.Value, 0)
                    .And(BlendtreeMath.GreaterThan(Field("PointSeparation"), 0, true)));
                var channels = party == Party.Merged
                    ? GetChannels(socket, true).Union(GetChannels(socket, false))
                    : GetChannels(socket, self);
                var native = new Lazy<BlendtreeMath.Condition>(() => !legacy ? BlendtreeMath.True()
                    : channels.Select(SpsTags.FormatPlug).Aggregate(BlendtreeMath.False(),
                        (condition, tag) => condition.Or(BlendtreeMath.GreaterThan(Field("CollisionTag_" + tag), 0))));
                var presence = new Lazy<(VFBlendTreeDirect whenTrue, VFBlendTreeDirect whenFalse)>(() =>
                    present.Value.CreateBranches(tree, prefix + "/Present"));
                var encoding = new Lazy<(VFBlendTreeDirect whenTrue, VFBlendTreeDirect whenFalse)>(() =>
                    native.Value.CreateBranches(presence.Value.whenTrue, prefix + "/Native"));
                // Outside the length sphere, infer B = centerDistance - A * (1 - proximity).
                // At saturation this is a lower bound; use it to seed a missing estimate.
                var length = new Lazy<VFAParam>(() => {
                    var output = fx.CreateUniqueFloat(prefix + "/Length", 0.3f);
                    var nativeInput = Field("SenderLossyScaleZ");
                    var estimate = legacy ? math.Add(prefix + "/LegacyLengthEstimate",
                        (Field("PointSeparation"), 1), (proximity.Value, worldRadius), (1, -worldRadius)) : null;
                    AddSize("Length", output, nativeInput, 0.3f, estimate);
                    return output;
                });
                var radius = new Lazy<VFAParam>(() => {
                    var output = fx.CreateUniqueFloat(prefix + "/Radius", 0.03f);
                    var nativeInput = Field("SenderLossyScaleX");
                    if (legacy) {
                        var legacyWidthReceiver = contacts.GetLegacyWidthReceiver(main);
                        var helper = receiverAnimator.GetParameter(legacyWidthReceiver, Party.Others);
                        VFAParam HelperField(string suffix) => receiverAnimator.GetAdvancedParam(legacyWidthReceiver, Party.Others, suffix);
                        var helperPresent = BlendtreeMath.GreaterThan(helper, 0);
                        var helperRadius = math.Add(prefix + "/LegacyHelperRadius",
                            (HelperField("PointSeparation"), 1), (helper, worldRadius), (1, -worldRadius));
                        var estimate = math.Add(prefix + "/LegacyRadiusEstimate", (proximity.Value, worldRadius / 2), (helper, -worldRadius / 2));
                        var measurable = BlendtreeMath.GreaterThan(helperRadius, 0.01001f)
                            .And(BlendtreeMath.LessThan(helper, 1));
                        AddSize("Radius", output, nativeInput, 0.03f, estimate, helperPresent, measurable,
                            BlendtreeMath.GreaterThan(proximity.Value, 1, true).Or(BlendtreeMath.GreaterThan(helper, 1, true)));
                    } else {
                        AddSize("Radius", output, nativeInput, 0.03f);
                    }
                    return output;
                });
                var distance = new Lazy<VFAParam>(() => {
                    var output = fx.CreateUniqueFloat(prefix + "/Dist/Meters", 100);
                    VFMotion Sum(string label, params (BlendtreeMath.Value input, float multiplier)[] terms) =>
                        BlendtreeMath.Add(prefix + "/Dist/" + label, output, terms);
                    encoding.Value.whenTrue.Add(Sum("Native", (Field("PointSeparation"), 1), (Field("SenderLossyScaleZ"), -1)));
                    if (legacy) {
                        encoding.Value.whenFalse.Add(BlendtreeMath.LessThan(proximity.Value, 1).create(
                            Sum("LegacyOutside", (1, worldRadius), (proximity.Value, -worldRadius)),
                            Sum("LegacyInside", (Field("PointSeparation"), 1), (length.Value, -1))));
                    }
                    presence.Value.whenFalse.Add(output.MakeSetter(100));
                    return output;
                });
                var velocity = new Lazy<VFAParam>(() => {
                    var output = fx.CreateUniqueFloat(prefix + "/Velocity");
                    // Deliberately axial velocity, not total speed or a buffered radial-depth derivative.
                    presence.Value.whenTrue.Add(output.MakeCopier(Field("SenderVelocityZ"), -100, 100));
                    presence.Value.whenFalse.Add(output.MakeSetter(0));
                    return output;
                });
                // Read the cached size instead of extending its decoding branches with inversion writers.
                var inverseLength = new Lazy<VFAParam>(() => math.Invert(prefix + "/Length/Inverted", length.Value));
                var outputs = new Dictionary<(string parameter, string units), VFAParam>();
                VFAParam entryRadius = null;
                VFAParam CreateEntryRadius() {
                    var output = fx.CreateUniqueFloat(prefix + "/PlugEntryRadius");
                    // Modern plugs reach their sampled radius at the entrance, tapering to zero 5% outside.
                    var depth = GetOutput(SpsParamDepth, SpsUnitsPlugLengths);
                    encoding.Value.whenTrue.Add(VFBlendTree1D.CreateWithData(prefix + "/PlugEntryRadius/NativeTaper",
                        depth, (0, output.MakeCopier(Field("SenderLossyScaleY"))), (0.05f, output.MakeSetter(0))));
                    if (legacy) {
                        var helper = receiverAnimator.GetParameter(contacts.GetLegacyWidthReceiver(main), Party.Others);
                        var taper = VFBlendTree1D.CreateWithData(prefix + "/PlugEntryRadius/LegacyTaper",
                            depth,
                            (-0.05f, output.MakeCopier(radius.Value)), (0, output.MakeSetter(0)));
                        encoding.Value.whenFalse.Add(BlendtreeMath.GreaterThan(helper, 0)
                            .create(taper, output.MakeSetter(0)));
                    }
                    presence.Value.whenFalse.Add(output.MakeSetter(0));
                    return output;
                }
                return source => {
                    var parsed = ParseSpsMagicParam(source, isSocket: true);
                    return parsed == null ? null : GetOutput(parsed.Value.param, parsed.Value.units);
                };

                VFAParam GetOutput(string parameter, string units) {
                    return outputs.GetOrCreate((parameter, units), () => {
                        if (parameter == SpsParamInteractingWithOthers) {
                            if (self) return fx.Zero();
                            var othersOutput = fx.CreateUniqueFloat(prefix + "/InteractingWithOthers");
                            // SDK SenderType: 0 = none, 1 = self, 2 = another avatar, 3 = world.
                            var others = party == Party.Others ? BlendtreeMath.True() : BlendtreeMath.GreaterThan(Field("SenderType"), 1);
                            presence.Value.whenTrue.Add(others.create(othersOutput.MakeSetter(1), othersOutput.MakeSetter(0)));
                            presence.Value.whenFalse.Add(othersOutput.MakeSetter(0));
                            return othersOutput;
                        }
                        if (parameter == SpsParamSocketIsHole) return entranceType.Value == VRCFuryHapticSocket.AddLight.Hole ? fx.One() : fx.Zero();
                        if (parameter == SpsParamSocketIsTwoWay) return entranceType.Value == VRCFuryHapticSocket.AddLight.Ring ? fx.One() : fx.Zero();
                        if (parameter == SpsParamInteractingWithLegacy) {
                            if (!legacy) return fx.Zero();
                            var legacyOutput = fx.CreateUniqueFloat(prefix + "/InteractingWithLegacy");
                            encoding.Value.whenTrue.Add(legacyOutput.MakeSetter(0));
                            encoding.Value.whenFalse.Add(legacyOutput.MakeSetter(1));
                            presence.Value.whenFalse.Add(legacyOutput.MakeSetter(0));
                            return legacyOutput;
                        }
                        if (parameter == SpsParamPlugLength && units == SpsUnitsPlugLengths) return fx.One();
                        var input = parameter switch {
                            SpsParamDepth => distance.Value,
                            SpsParamVelocity => velocity.Value,
                            SpsParamPlugLength => length.Value,
                            SpsParamPlugRadius => radius.Value,
                            SpsParamPlugEntryRadius => entryRadius ??= CreateEntryRadius(),
                            _ => throw new ArgumentOutOfRangeException(nameof(parameter))
                        };
                        if (units == SpsUnitsMeters) return input;
                        var multiplier = units == SpsUnitsLocal ? inverseWorldScale.Value : inverseLength.Value;
                        var output = fx.CreateUniqueFloat(input.name + (units == SpsUnitsLocal ? "/Local" : "/PlugLengths"),
                            parameter == SpsParamDepth ? 100 : input.GetDefault() * multiplier.GetDefault());
                        if (parameter == SpsParamPlugLength || parameter == SpsParamPlugRadius) {
                            // Size may settle a frame later. Keep its decoding/cache branches out of unit conversions.
                            tree.Add(multiplier, output.MakeCopier(input));
                        } else {
                            math.MultiplyInPlace(output, multiplier, input);
                        }
                        return output;
                    });
                }

                void AddSize(string label, VFAParam output, VFAParam nativeInput, float assumed,
                    VFAParam estimate = null, BlendtreeMath.Condition extraPresent = null,
                    BlendtreeMath.Condition extraMeasurable = null, BlendtreeMath.Condition saturated = null) {
                    if (!legacy) {
                        encoding.Value.whenTrue.Add(output.MakeCopier(nativeInput));
                        presence.Value.whenFalse.Add(output.MakeSetter(assumed));
                        return;
                    }
                    var confidence = fx.CreateUniqueFloat(prefix + "/Legacy" + label + "Valid");
                    // Check helper/bounds before the OR to avoid repeating the cache fallback.
                    var measurable = (extraMeasurable ?? BlendtreeMath.True())
                        .And(BlendtreeMath.InRange(estimate, 0, 3, includeMin: false))
                        .And(BlendtreeMath.LessThan(proximity.Value, 1)
                            .Or(BlendtreeMath.GreaterThan(confidence, 0).Not()));
                    VFBlendTreeDirect WithConfidence(VFMotion motion, float value) {
                        var result = VFBlendTreeDirect.Create(prefix + "/Legacy" + label);
                        result.Add(motion);
                        result.Add(confidence.MakeSetter(value));
                        return result;
                    }
                    var fallback = WithConfidence(output.MakeSetter(assumed), 0);
                    var measured = measurable.create(WithConfidence(output.MakeCopier(estimate), 1),
                        (saturated ?? BlendtreeMath.GreaterThan(proximity.Value, 1, true))
                        .And(BlendtreeMath.GreaterThan(confidence, 0)).create(
                            WithConfidence(output.MakeCopier(output), 1), fallback));
                    if (extraPresent != null) measured = extraPresent.create(measured, fallback);
                    encoding.Value.whenTrue.Add(WithConfidence(output.MakeCopier(nativeInput), 0));
                    encoding.Value.whenFalse.Add(measured);
                    presence.Value.whenFalse.Add(fallback);
                }
            });
#else
            throw new Exception("SPS depth measurements require VRCSDK 3.10.6 or newer. Update the VRCSDK to use SPS.");
#endif
        }

    }
}
