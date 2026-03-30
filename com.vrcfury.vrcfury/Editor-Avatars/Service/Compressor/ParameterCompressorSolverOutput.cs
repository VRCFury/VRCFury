using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Service.Compressor {
    internal class ParameterCompressorSolverOutput {
        public OptimizationDecision decision = new OptimizationDecision();
        public ParameterCompressorSolverService.ParamSelectionOptions options = new ParameterCompressorSolverService.ParamSelectionOptions();
        public IList<VRCExpressionParameters.Parameter> warnUnusedParams = new List<VRCExpressionParameters.Parameter>();
        public IList<VRCExpressionParameters.Parameter> warnContactParams = new List<VRCExpressionParameters.Parameter>();
        public IList<VRCExpressionParameters.Parameter> warnButtonParams = new List<VRCExpressionParameters.Parameter>();
        public IList<VRCExpressionParameters.Parameter> warnOscOnlyParams = new List<VRCExpressionParameters.Parameter>();

        public string FormatWarnings(int maxCount) {
            var lines = new [] {
                FormatWarnings("These params are totally unused in all controllers:", warnUnusedParams, maxCount),
                FormatWarnings("These params are generated from a contact or physbone, which usually should happen on each remote, NOT synced:", warnContactParams, maxCount),
                FormatWarnings("These params are used by a momentary button in your menu, which are often used by presets and often shouldn't be synced:", warnButtonParams, maxCount),
                FormatWarnings("These params can only change using OSC, they cannot change if you are not running an OSC app:", warnOscOnlyParams, maxCount),
            }.NotNull().Join("\n\n");
            if (!string.IsNullOrEmpty(lines)) {
                return "Want to improve performance and reduce sync? VRCFury has detected that these params should possibly be marked as not network synced in your Parameters file:\n\n" + lines;
            }
            return "";
        }

        [CanBeNull]
        private static string FormatWarnings(string title, IList<VRCExpressionParameters.Parameter> list, int maxCount) {
            if (list.Count == 0) return null;
            return title + "\n" + list.Select(p => $"{p.name} ({ParameterCompressorService.FormatBitsPlural(VRCExpressionParameterExtensions.TypeCost(p))})").JoinWithMore(maxCount);
        }
    }
}
