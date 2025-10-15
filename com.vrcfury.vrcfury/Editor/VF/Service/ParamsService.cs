using JetBrains.Annotations;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Service {
    [VFService]
    internal class ParamsService {
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        
        private ParamManager _params;
        public ParamManager GetParams() {
            if (_params == null) {
                var origParams = VRCAvatarUtils.GetAvatarParams(avatar);
                VRCExpressionParameters prms;
                if (origParams != null) {
                    prms = origParams.Clone();
                } else {
                    prms = VrcfObjectFactory.Create<VRCExpressionParameters>();
                    prms.parameters = new VRCExpressionParameters.Parameter[]{};
                }
                VRCAvatarUtils.SetAvatarParams(avatar, prms);
                _params = new ParamManager(prms);
            }
            return _params;
        }

        [CanBeNull]
        public VRCExpressionParameters GetReadOnlyParams() {
            return VRCAvatarUtils.GetAvatarParams(avatar);
        }
    }
}
