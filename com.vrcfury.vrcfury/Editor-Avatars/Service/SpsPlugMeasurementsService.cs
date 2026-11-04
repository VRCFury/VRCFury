using static VF.Inspector.VRCFuryHapticPlugEditor;
using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
using System;
using System.Collections.Generic;
using VF.Builder.Haptics;
using VF.Injector;
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
            var worldScale = new Lazy<VFAParam>(() => worldScaleService.GetWorldScale(bake.bakeRoot, name));
            var inverseWorldScale = new Lazy<VFAParam>(() => worldScaleService.GetInverseWorldScale(bake.bakeRoot, name));
            var tree = receiverAnimator.GetTree(main);
            var math = new BlendtreeMath(fx, tree);
            var length = new Lazy<VFAParam>(() => math.Multiply(name + "/LengthMeters", worldScale.Value, localLength));
            var radius = new Lazy<VFAParam>(() => math.Multiply(name + "/RadiusMeters", worldScale.Value, localRadius));
            var localLengthParam = new Lazy<VFAParam>(() => fx.CreateUniqueFloat(name + "/PlugLengthLocal", localLength));
            var localRadiusParam = new Lazy<VFAParam>(() => fx.CreateUniqueFloat(name + "/PlugRadiusLocal", localRadius));
            var normalizedRadius = new Lazy<VFAParam>(() => fx.CreateUniqueFloat(name + "/PlugRadiusInPlugLengths",
                localLength == 0 ? 0 : localRadius / localLength));
            var byParty = new Dictionary<Party, Func<string, VFAParam>>();
            return party => byParty.GetOrCreate(party, () => {
                var prefix = name + "/" + party;
                var proximity = new Lazy<VFAParam>(() => receiverAnimator.GetParameter(main, party));
                VFAParam Field(string suffix) => receiverAnimator.GetAdvancedParam(main, party, suffix);
                var present = new Lazy<BlendtreeMath.Condition>(() => BlendtreeMath.GreaterThan(proximity.Value, 0)
                    .And(BlendtreeMath.GreaterThan(Field("PointSeparation"), 0, true)));
                var distance = new Lazy<VFAParam>(() => {
                    var output = fx.CreateUniqueFloat(prefix + "/Dist/Meters", 100);
                    tree.Add(present.Value.create(BlendtreeMath.Add(prefix + "/Dist/Socket", output,
                        (Field("PointSeparation"), 1), (worldScale.Value, -localLength)), output.MakeSetter(100)));
                    return output;
                });
                var velocity = new Lazy<VFAParam>(() => {
                    var output = fx.CreateUniqueFloat(prefix + "/Velocity");
                    tree.Add(present.Value.create(
                        output.MakeCopier(Field("SenderVelocityZ"), -100, 100), output.MakeSetter(0)));
                    return output;
                });
                var outputs = new Dictionary<(string parameter, string units), VFAParam>();
                return source => {
                    var parsed = ParseSpsMagicParam(source);
                    if (parsed == null) return null;
                    var (parameter, units) = parsed.Value;
                    return outputs.GetOrCreate((parameter, units), () => {
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
            throw new Exception("SPS depth measurements require VRCSDK 3.10.6 or newer. Update the VRCSDK to use SPS.");
#endif
        }
    }
}
