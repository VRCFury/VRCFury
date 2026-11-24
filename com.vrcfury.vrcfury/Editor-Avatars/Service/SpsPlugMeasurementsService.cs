using static VF.Inspector.VRCFuryHapticPlugEditor;
using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
using System;
using System.Collections.Generic;
using VF.Builder.Haptics;
using VF.Injector;
using VF.Inspector;
using BakeResult = VF.Inspector.VRCFuryHapticPlugEditor.BakeResult;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class SpsPlugMeasurementsService {
        [VFAutowired] private readonly SelfAndOthersReceiverAnimatorService receiverAnimator;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly WorldScaleDetectorService worldScaleService;
        public Func<Party, Func<string, VFAParam>> GetMeasurements(BakeResult bake, string name) {
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            var main = bake.socketReceivers;
            var fx = controllers.GetFx();
            var localLength = bake.localLength;
            var localRadius = bake.localRadius;
            var scaleInput = new Lazy<(VFAParam raw, float multiplier)>(() =>
                worldScaleService.GetRawWorldScaleWithMultiplier(bake.bakeRoot, name));
            var inverseWorldScale = new Lazy<VFAParam>(() => worldScaleService.GetInverseWorldScale(bake.bakeRoot, name));
            var tree = receiverAnimator.GetTree(main);
            var math = new BlendtreeMath(fx, tree);
            var length = new Lazy<VFAParam>(() => ToMeters(name + "/LengthMeters", localLength));
            var radius = new Lazy<VFAParam>(() => ToMeters(name + "/RadiusMeters", localRadius));
            VFAParam ToMeters(string label, float localSize) {
                var (raw, multiplier) = scaleInput.Value;
                return math.Multiply(label, raw, localSize * multiplier);
            }
            var localLengthParam = new Lazy<VFAParam>(() => fx.CreateUniqueFloat(name + "/PlugLengthLocal", localLength));
            var localRadiusParam = new Lazy<VFAParam>(() => fx.CreateUniqueFloat(name + "/PlugRadiusLocal", localRadius));
            var normalizedRadius = new Lazy<VFAParam>(() => fx.CreateUniqueFloat(name + "/PlugRadiusInPlugLengths",
                localLength == 0 ? 0 : localRadius / localLength));
            var byParty = new Dictionary<Party, Func<string, VFAParam>>();
            return party => byParty.GetOrCreate(party, () => {
                var prefix = name + "/" + party;
                VFAParam Field(string suffix) => receiverAnimator.GetAdvancedParam(main, party, suffix);
                // Point separation is normally -1 with no contact, but can be 0 until the receiver first turns on.
                var present = new Lazy<BlendtreeMath.Condition>(() => BlendtreeMath.GreaterThan(Field("PointSeparation"), 0));
                var presence = new Lazy<(VFBlendTreeDirect whenTrue, VFBlendTreeDirect whenFalse)>(() =>
                    present.Value.CreateBranches(tree, prefix + "/Present"));
                var legacy = party != Party.Self && main.request.supportsLegacy;
                var native = new Lazy<BlendtreeMath.Condition>(() => receiverAnimator.GetNative(main, party));
                var encoding = new Lazy<(VFBlendTreeDirect whenTrue, VFBlendTreeDirect whenFalse)>(() =>
                    native.Value.CreateBranches(presence.Value.whenTrue, prefix + "/Native"));
                var distance = new Lazy<VFAParam>(() => {
                    var output = fx.CreateUniqueFloat(prefix + "/Dist/Meters", 100);
                    var (raw, multiplier) = scaleInput.Value;
                    presence.Value.whenTrue.Add(BlendtreeMath.Add(
                        prefix + "/Dist/Socket",
                        output,
                        (Field("PointSeparation"), 1),
                        (raw, -localLength * multiplier)
                    ));
                    presence.Value.whenFalse.Add(output.MakeSetter(100));
                    return output;
                });
                var velocity = new Lazy<VFAParam>(() => {
                    var output = fx.CreateUniqueFloat(prefix + "/Velocity");
                    presence.Value.whenTrue.Add(output.MakeCopier(Field("SenderVelocityZ"), -100, 100));
                    presence.Value.whenFalse.Add(output.MakeSetter(0));
                    return output;
                });
                var outputs = new Dictionary<(string parameter, string units), VFAParam>();
                return source => {
                    var parsed = ParseSpsMagicParam(source, isSocket: false);
                    if (parsed == null) return null;
                    var (parameter, units) = parsed.Value;
                    return outputs.GetOrCreate((parameter, units), () => {
                        if (parameter == SpsParamInteractingWithOthers) {
                            if (party == Party.Self) return fx.Zero();
                            var othersOutput = fx.CreateUniqueFloat(prefix + "/InteractingWithOthers");
                            // SDK SenderType: 0 = none, 1 = self, 2 = another avatar, 3 = world.
                            var others = party == Party.Others ? BlendtreeMath.True() : BlendtreeMath.GreaterThan(Field("SenderType"), 1);
                            presence.Value.whenTrue.Add(others.create(othersOutput.MakeSetter(1), othersOutput.MakeSetter(0)));
                            presence.Value.whenFalse.Add(othersOutput.MakeSetter(0));
                            return othersOutput;
                        }
                        if (parameter == SpsParamInteractingWithLegacy) {
                            if (!legacy) return fx.Zero();
                            var legacyOutput = fx.CreateUniqueFloat(prefix + "/InteractingWithLegacy");
                            encoding.Value.whenTrue.Add(legacyOutput.MakeSetter(0));
                            encoding.Value.whenFalse.Add(legacyOutput.MakeSetter(1));
                            presence.Value.whenFalse.Add(legacyOutput.MakeSetter(0));
                            return legacyOutput;
                        }
                        if (parameter == SpsParamSocketIsHole) {
                            var flagOutput = fx.CreateUniqueFloat(prefix + "/SocketIsHole");
                            var off = flagOutput.MakeSetter(0);
                            var on = flagOutput.MakeSetter(1);
                            // Collapse is the highest bit in the packed X scale.
                            var nativeCollapse = BlendtreeMath.GreaterThan(
                                Field("SenderLossyScaleX"),
                                (int)SpsTags.SocketScaleFlagsX.Collapse - 0.5f,
                                orEqual: true,
                                name: flagOutput.name + "/NativeFlag"
                            );
                            var animatorHole = BlendtreeMath.GreaterThan(
                                Field("SenderLossyScaleZ"),
                                (int)SpsTags.SocketScaleFlagsZ.AnimatorHole - 0.5f,
                                orEqual: true
                            );
                            encoding.Value.whenTrue.Add(animatorHole.Or(nativeCollapse).create(on, off));
                            if (legacy) {
                                var holeTag = BlendtreeMath.GreaterThan(Field("CollisionTag_" + SpsTags.LegacySocketHole), 0);
                                var ringTag = BlendtreeMath.GreaterThan(Field("CollisionTag_" + SpsTags.LegacySocketRing), 0);
                                var legacyFlag = holeTag.And(ringTag.Not());
                                encoding.Value.whenFalse.Add(legacyFlag.create(flagOutput.MakeSetter(1), flagOutput.MakeSetter(0)));
                            }
                            presence.Value.whenFalse.Add(flagOutput.MakeSetter(0));
                            return flagOutput;
                        }
                        if (parameter == SpsParamPlugLength && units == SpsUnitsPlugLengths) return fx.One();
                        if (parameter == SpsParamPlugLength && units == SpsUnitsLocal) return localLengthParam.Value;
                        if (parameter == SpsParamPlugRadius && units == SpsUnitsPlugLengths) return normalizedRadius.Value;
                        if (parameter == SpsParamPlugRadius && units == SpsUnitsLocal) return localRadiusParam.Value;
                        var input = parameter switch {
                            SpsParamDepth => distance.Value,
                            SpsParamVelocity => velocity.Value,
                            SpsParamPlugLength => length.Value,
                            SpsParamPlugRadius => radius.Value,
                            _ => throw new ArgumentOutOfRangeException(nameof(source))
                        };
                        if (units == SpsUnitsMeters) return input;
                        var multiplier = inverseWorldScale.Value;
                        var factor = units == SpsUnitsLocal ? 1 : localLength == 0 ? 0 : 1 / localLength;
                        var output = fx.CreateUniqueFloat(input.name + (units == SpsUnitsLocal ? "/Local" : "/PlugLengths"),
                            parameter == SpsParamDepth ? 100 : input.GetDefault() * multiplier.GetDefault() * factor);
                        math.MultiplyInPlace(output, multiplier, input, factor);
                        return output;
                    });
                };
            });
#else
            throw new Exception(VF.Exceptions.VRCFExceptionUtils.NewerVrcsdkRequired);
#endif
        }
    }
}
