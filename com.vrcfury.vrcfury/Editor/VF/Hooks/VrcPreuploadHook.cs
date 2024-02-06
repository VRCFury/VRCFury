using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Menu;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using Object = UnityEngine.Object;

namespace VF.Hooks {
    internal class VrcPreuploadHook : IVRCSDKPreprocessAvatarCallback {
        // This has to be before -1024 when VRCSDK deletes our components
        public int callbackOrder => -10000;

        public bool OnPreprocessAvatar(GameObject _vrcCloneObject) {
            if (Application.isPlaying && !PlayModeMenuItem.Get()) return true;
            
            VFGameObject vrcCloneObject = _vrcCloneObject;

            if (!VRCFuryBuilder.ShouldRun(vrcCloneObject)) {
                return true;
            }

            var builder = new VRCFuryBuilder();
            var vrcFuryStatus = builder.SafeRun(vrcCloneObject, keepDebugInfo: Application.isPlaying);

            return vrcFuryStatus == VRCFuryBuilder.Status.Success;
        }
    }
}
