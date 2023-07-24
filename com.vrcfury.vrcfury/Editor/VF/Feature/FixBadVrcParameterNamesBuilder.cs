using System;
using System.Collections.Generic;
using VF.Feature.Base;
using VF.Utils;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Builder {
    /**
     * VRChat will crash and fail to load the avatar, breaking IK and OSC, in some cases. Some known cases so far:
     * * Two different parameters with the same name when spaces are replaced with _
     * * Parameter name contains '[]'
     */
    public class FixBadVrcParameterNamesBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.FixBadParameters)]
        public void Apply() {
            var usedParams = new HashSet<string>();
            RewriteInAll(name => {
                if (string.IsNullOrEmpty(name)) return name;
                usedParams.Add(name);
                return name;
            });

            var normalizedNames = new HashSet<string>();
            var rewrites = new Dictionary<string, string>();
            foreach (var name in usedParams) {
                var normalized = name.Replace(' ', '_');
                var forceRename = false;
                if (normalized.Contains("[]")) {
                    forceRename = true;
                    normalized = normalized.Replace("[]", "");
                }
                if (normalizedNames.Contains(normalized)) {
                    forceRename = true;
                    for (var i = 2;; i++) {
                        var attempt = $"{normalized}_{i}";
                        if (!normalizedNames.Contains(attempt)) {
                            normalized = attempt;
                            break;
                        }
                    }
                }
                normalizedNames.Add(normalized);
                if (forceRename) {
                    rewrites.Add(name, normalized);
                }
            }

            string RewriteParam(string name) {
                if (string.IsNullOrEmpty(name)) return name;
                if (rewrites.TryGetValue(name, out string rewritten)) {
                    return rewritten;
                }
                return name;
            }
            RewriteInAll(RewriteParam);
        }

        private void RewriteInAll(Func<string, string> each) {
            foreach (var c in manager.GetAllUsedControllersRaw()) {
                c.Item2.RewriteParameters(each);
            }
            manager.GetMenu().GetRaw().RewriteParameters(each);
            manager.GetParams().GetRaw().RewriteParameters(each);
            foreach (var receiver in avatarObject.GetComponentsInSelfAndChildren<VRCContactReceiver>()) {
                receiver.parameter = each(receiver.parameter);
            }
            foreach (var physbone in avatarObject.GetComponentsInSelfAndChildren<VRCPhysBone>()) {
                physbone.parameter = each(physbone.parameter);
            }
        }
    }
}
