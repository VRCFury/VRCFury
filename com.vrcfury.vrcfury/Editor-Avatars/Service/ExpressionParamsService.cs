using VF.Builder;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Service {
    [VFService]
    internal class ExpressionParamsService : IEnumerable<VRCExpressionParameters.Parameter> {
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        
        private VRCExpressionParameters _asset;
        private VRCExpressionParameters asset => _asset != null ? _asset : (_asset = MakeParams());

        public VRCExpressionParameters.Parameter AddOrMerge(VRCExpressionParameters.Parameter parameter) {
            var existing = asset.FindParameter(parameter.name);
            if (existing != null) {
                if (parameter.valueType != existing.valueType) {
                    throw new Exception($"VRCF tried to create expression parameter {parameter.name} with type {parameter.valueType},"
                        + $" but parameter already exists with type {existing.valueType}");
                }
                if (!parameter.HasSameNetworkEncoding(existing)) {
                    throw new Exception($"VRCF tried to merge expression parameter {parameter.name}, but its network bit width or signedness"
                        + " conflicts with the existing parameter");
                }
                if (!parameter.HasSameInterpolation(existing)) {
                    throw new Exception($"VRCF tried to merge expression parameter {parameter.name}, but its interpolation duration conflicts with the existing parameter");
                }
                return existing;
            }
            SetParameters(asset.parameters.Append(parameter));
            return parameter;
        }

        public void SetParameters(IEnumerable<VRCExpressionParameters.Parameter> parameters) {
            var entries = parameters.ToArray();
            asset.parameters = entries;
            asset.Dirty();
        }

        public void WorkLog(string message) => asset.WorkLog(message);
        public void Dirty() => asset.Dirty();

        public IEnumerator<VRCExpressionParameters.Parameter> GetEnumerator() =>
            ((IEnumerable<VRCExpressionParameters.Parameter>)asset.parameters).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private IEnumerable<VRCExpressionParameters.Parameter> ReadOnlyParameters =>
            VRCAvatarUtils.GetAvatarParams(avatar)?.parameters ?? Array.Empty<VRCExpressionParameters.Parameter>();

        // Independent copies of all entries, without cloning or assigning an avatar asset.
        public VRCExpressionParameters.Parameter[] GetSnapshot() => ReadOnlyParameters
            .Select(p => p.Clone()).ToArray();

        public bool IsSameAs(IEnumerable<VRCExpressionParameters.Parameter> other) =>
            ReadOnlyParameters.Select(p => JsonUtility.ToJson(p))
                .SequenceEqual(other.Select(p => JsonUtility.ToJson(p)));

        public int CalcTotalCost() => VRCAvatarUtils.GetAvatarParams(avatar)?.CalcTotalCost() ?? 0;

        public static int GetMaxCost() {
            var maxBits = VRCExpressionParameters.MAX_PARAMETER_COST;
            if (maxBits > 9999) {
                // Some modified versions of the VRChat SDK have a broken value for this.
                maxBits = 256;
            }
            if (DexProtectUtils.IsDexProtectPresent()) maxBits -= 19;
            return maxBits;
        }

        public bool Contains(string name) {
            return VRCAvatarUtils.GetAvatarParams(avatar)?.FindParameter(name) != null;
        }

        private VRCExpressionParameters MakeParams() {
            var origParams = VRCAvatarUtils.GetAvatarParams(avatar);
            VRCExpressionParameters prms;
            if (VrcfObjectFactory.DidCreate(origParams)) {
                // We probably made this in an earlier preprocessor hook, so we can just adopt it
                prms = origParams;
            } else if (origParams != null) {
                prms = origParams.Clone();
                prms.WorkLog($"Loaded expression parameters for mutation from {origParams.GetPathAndName()} ({origParams.parameters.Length} entries)");
            } else {
                prms = VrcfObjectFactory.Create<VRCExpressionParameters>();
                prms.parameters = new VRCExpressionParameters.Parameter[] { };
            }
            VRCAvatarUtils.SetAvatarParams(avatar, prms);
            prms.parameters = prms.parameters.GroupBy(p => p.name).Select(group => group.First()).ToArray();
            return prms;
        }

        public void ClearCache() {
            _asset = null;
        }
    }
}
