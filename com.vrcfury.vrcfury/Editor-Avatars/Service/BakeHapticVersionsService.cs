using System.Linq;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Menu;
using VF.Utils;

namespace VF.Service {
    /** Adds a parameter to the avatar so OGB can pick up what version of haptics are available */
    [VFService]
    internal class BakeHapticVersionsService {
        [VFAutowired] private readonly ExpressionParamsService expressionParams;
        [VFAutowired] private readonly VFGameObject avatarObject;
        
        // Bump when plug senders or receivers are changed
        private const int LocalVersion = 11;

        [FeatureBuilderAction]
        public void Apply() {
            var hapticsEnabled = HapticsToggleMenuItem.Get();
            var hasOgbComponents = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>().Any(c => !c.fromSpsForAll)
                                  || avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>().Any(c => !c.fromSpsForAll)
                                  || avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticTouchReceiver>().Any();

            if (hasOgbComponents) {
                // Add an expression parameter so it can be picked up by client apps.
                // Because of https://feedback.vrchat.com/bug-reports/p/oscquery-provides-wrong-values-for-avatar-parameters-until-they-are-changed
                // we can't just use an int and set it to the version number.
                expressionParams.AddOrMerge(ExpressionParam.Create($"OGB/Version/{LocalVersion}").Bool().Unsynced());
                if (!hapticsEnabled) {
                    expressionParams.AddOrMerge(ExpressionParam.Create("OGB/HapticsDisabled").Bool().Default(true).Unsynced());
                }
            }
        }
    }
}
