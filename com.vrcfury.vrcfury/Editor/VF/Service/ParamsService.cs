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
            if (_params == null) _params = MakeParams();
            return _params;
        }

        private ParamManager MakeParams() {
            var origParams = VRCAvatarUtils.GetAvatarParams(avatar);
            VRCExpressionParameters prms;
            if (VrcfObjectFactory.DidCreate(origParams)) {
                // We probably made this in an earlier preprocessor hook, so we can just adopt it
                prms = origParams;
            } else if (origParams != null) {
                prms = origParams.Clone();
            } else {
                prms = VrcfObjectFactory.Create<VRCExpressionParameters>();
            }
            VRCAvatarUtils.SetAvatarParams(avatar, prms);
            prms.RemoveDuplicates();
            return new ParamManager(prms);
        }

        public void ClearCache() {
            _params = null;
        }

        public VRCExpressionParameters GetReadOnlyParams() {
            var p = VRCAvatarUtils.GetAvatarParams(avatar);
            if (p == null) {
                p = VrcfObjectFactory.Create<VRCExpressionParameters>();
            }
            return p;
        }
    }
}
