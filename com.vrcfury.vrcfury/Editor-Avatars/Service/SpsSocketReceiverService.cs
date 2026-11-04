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
    internal class SpsSocketReceiverService {
        internal const float WorldRadius = 1;

        [VFAutowired] private readonly SelfAndOthersReceiverAnimatorService receiverAnimator;
        [VFAutowired] private readonly SelfAndOthersReceiverService receivers;
        [VFAutowired] private readonly SpsConfigurer configurer;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly WorldScaleDetectorService worldScaleService;
        private readonly Dictionary<VRCFuryHapticSocket, SelfAndOthersReceiverService.Receivers> cache = new();

        private HashSet<string> GetChannels(VRCFuryHapticSocket socket, bool self) =>
            configurer.GetSocketChannels(socket, self
                ? SpsConfigurer.ChannelMode.ReceiverSelf : SpsConfigurer.ChannelMode.ReceiverOthers);

        private List<string> GetPlugTags(VRCFuryHapticSocket socket, bool self) {
            var tags = GetChannels(socket, self).OrderBy(c => c, StringComparer.Ordinal)
                .Select(SpsTags.FormatPlug).ToList();
            if (!self && socket.useSharedTag) tags.Add(SpsTags.LegacyPlugSphere);
            return tags;
        }

        public SelfAndOthersReceiverService.Receivers GetReceivers(VRCFuryHapticSocket socket, VFGameObject frame, string name, VFGameObject bakeRoot = null) =>
            cache.GetOrCreate(socket, () => receivers.Create(new SelfAndOthersReceiverService.Request {
                frame = frame, animatorRoot = bakeRoot, name = name + "/Depth", worldRadius = WorldRadius,
                selfTags = GetPlugTags(socket, true).ToArray(),
                othersTags = GetPlugTags(socket, false).ToArray()
            }));

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
                main.request.animatorRoot ?? main.request.frame, name));
            var includeLegacy = socket.useSharedTag;
            var legacyWidthReceiver = includeLegacy ? new Lazy<SelfAndOthersReceiverService.Receivers>(() => receivers.Create(new SelfAndOthersReceiverService.Request {
                frame = main.request.frame,
                name = name + "/LegacyWidth",
                worldRadius = WorldRadius,
                othersTags = new[] { SpsTags.LegacyPlugWidth }
            })) : null;

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
                // Outside the length sphere, infer B = centerDistance - A * (1 - proximity).
                // At saturation this is a lower bound; use it to seed a missing estimate.
                var length = new Lazy<VFAParam>(() => {
                    var output = fx.CreateUniqueFloat(prefix + "/Length", 0.3f);
                    var nativeInput = Field("SenderLossyScaleZ");
                    VFMotion motion;
                    if (legacy) {
                        var estimate = math.Add(prefix + "/LegacyLengthEstimate",
                            (Field("PointSeparation"), 1), (proximity.Value, WorldRadius), (1, -WorldRadius));
                        motion = LegacyEstimate("Length", output, nativeInput, estimate, proximity.Value, 0.3f);
                    } else {
                        motion = present.Value.create(
                            output.MakeCopier(nativeInput), output.MakeSetter(0.3f));
                    }
                    tree.Add(motion);
                    return output;
                });
                var radius = new Lazy<VFAParam>(() => {
                    var output = fx.CreateUniqueFloat(prefix + "/Radius", 0.03f);
                    var nativeInput = Field("SenderLossyScaleX");
                    VFMotion motion;
                    if (legacy) {
                        var helper = receiverAnimator.GetParameter(legacyWidthReceiver.Value, Party.Others);
                        VFAParam HelperField(string suffix) => receiverAnimator.GetAdvancedParam(legacyWidthReceiver.Value, Party.Others, suffix);
                        var helperPresent = BlendtreeMath.GreaterThan(helper, 0);
                        var helperRadius = math.Add(prefix + "/LegacyHelperRadius",
                            (HelperField("PointSeparation"), 1), (helper, WorldRadius), (1, -WorldRadius));
                        var estimate = math.Add(prefix + "/LegacyRadiusEstimate", (proximity.Value, WorldRadius / 2), (helper, -WorldRadius / 2));
                        var measurable = BlendtreeMath.GreaterThan(helperRadius, 0.01001f)
                            .And(BlendtreeMath.LessThan(helper, 1));
                        motion = LegacyEstimate("Radius", output, nativeInput, estimate, proximity.Value, 0.03f, helperPresent, measurable,
                            BlendtreeMath.GreaterThan(proximity.Value, 1, true).Or(BlendtreeMath.GreaterThan(helper, 1, true)));
                    } else {
                        motion = present.Value.create(
                            output.MakeCopier(nativeInput), output.MakeSetter(0.03f));
                    }
                    tree.Add(motion);
                    return output;
                });
                var distance = new Lazy<VFAParam>(() => {
                    var output = fx.CreateUniqueFloat(prefix + "/Dist/Meters", 100);
                    VFMotion Sum(string label, params (BlendtreeMath.Value input, float multiplier)[] terms) =>
                        BlendtreeMath.Add(prefix + "/Dist/" + label, output, terms);
                    var legacyDistance = legacy
                        ? BlendtreeMath.LessThan(proximity.Value, 1).create(
                            Sum("LegacyOutside", (1, WorldRadius), (proximity.Value, -WorldRadius)),
                            Sum("LegacyInside", (Field("PointSeparation"), 1), (length.Value, -1)))
                        : output.MakeSetter(100);
                    var measured = native.Value.create(
                        Sum("Native", (Field("PointSeparation"), 1), (Field("SenderLossyScaleZ"), -1)),
                        legacyDistance);
                    tree.Add(present.Value.create(measured, output.MakeSetter(100)));
                    return output;
                });
                var velocity = new Lazy<VFAParam>(() => {
                    var output = fx.CreateUniqueFloat(prefix + "/Velocity");
                    // Deliberately axial velocity, not total speed or a buffered radial-depth derivative.
                    tree.Add(present.Value.create(
                        output.MakeCopier(Field("SenderVelocityZ"), -100, 100), output.MakeSetter(0)));
                    return output;
                });
                // Read the cached size instead of extending its decoding branches with inversion writers.
                var inverseLength = new Lazy<VFAParam>(() => math.Invert(prefix + "/Length/Inverted", length.Value));
                var outputs = new Dictionary<(string parameter, string units), VFAParam>();
                return source => {
                    var parsed = ParseSpsMagicParam(source);
                    if (parsed == null) return null;
                    var (parameter, units) = parsed.Value;
                    return outputs.GetOrCreate((parameter, units), () => {
                        if (parameter == SpsParamPlugLength && units == SpsUnitsPlugLengths) return fx.One();
                        var input = parameter switch {
                            SpsParamDepth => distance.Value,
                            SpsParamVelocity => velocity.Value,
                            SpsParamPlugLength => length.Value,
                            SpsParamPlugRadius => radius.Value,
                            _ => throw new ArgumentOutOfRangeException(nameof(source))
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
                };

                VFMotion LegacyEstimate(string label, VFAParam output, VFAParam nativeInput, VFAParam estimate,
                    VFAParam proximity, float assumed, BlendtreeMath.Condition extraPresent = null,
                    BlendtreeMath.Condition extraMeasurable = null, BlendtreeMath.Condition saturated = null) {
                    var confidence = fx.CreateUniqueFloat(prefix + "/Legacy" + label + "Valid");
                    var measurable = BlendtreeMath.LessThan(proximity, 1)
                        .Or(BlendtreeMath.GreaterThan(confidence, 0).Not())
                        .And(BlendtreeMath.GreaterThan(estimate, 0)).And(BlendtreeMath.LessThan(estimate, 3, true));
                    if (extraMeasurable != null) measurable = measurable.And(extraMeasurable);
                    VFBlendTreeDirect WithConfidence(VFMotion motion, float value) {
                        var result = VFBlendTreeDirect.Create(prefix + "/Legacy" + label);
                        result.Add(motion);
                        result.Add(confidence.MakeSetter(value));
                        return result;
                    }
                    var fallback = WithConfidence(output.MakeSetter(assumed), 0);
                    var measured = measurable.create(WithConfidence(output.MakeCopier(estimate), 1),
                        (saturated ?? BlendtreeMath.GreaterThan(proximity, 1, true))
                        .And(BlendtreeMath.GreaterThan(confidence, 0)).create(
                            WithConfidence(output.MakeCopier(output), 1), fallback));
                    if (extraPresent != null) measured = extraPresent.create(measured, fallback);
                    return present.Value.create(native.Value.create(
                        WithConfidence(output.MakeCopier(nativeInput), 0), measured), fallback);
                }
            });
#else
            throw new Exception("SPS depth measurements require VRCSDK 3.10.6 or newer. Update the VRCSDK to use SPS.");
#endif
        }

    }
}
