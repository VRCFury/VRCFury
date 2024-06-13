using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Menu;
using VF.Model;
using VF.Model.Feature;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Inspector {
    internal static class VrcfDebugLine {

        private static readonly bool ndmfPresent =
            ReflectionUtils.GetTypeFromAnyAssembly("nadena.dev.ndmf.AvatarProcessor") != null;

        public static string GetOutputString([CanBeNull] VFGameObject avatarObject = null) {
            var output = "";

            IEnumerable<VRCFury> vrcfComponents;
            if (avatarObject == null) {
                vrcfComponents = Object.FindObjectsOfType<VRCFury>();
            } else {
                vrcfComponents = avatarObject.GetComponentsInSelfAndChildren<VRCFury>();
            }

            var wdDisabled = vrcfComponents
                .SelectMany(c => c.GetAllFeatures())
                .OfType<FixWriteDefaults>()
                .Any(fwd => fwd.mode == FixWriteDefaults.FixWriteDefaultsMode.Disabled);
            if (wdDisabled) {
                output += "W";
            }

            if (avatarObject != null && MaterialLocker.UsesD4rk(avatarObject, false)) {
                output += "D";
            }
            
            if (!HapticsToggleMenuItem.Get()) {
                output += "H";
            }

            if (ndmfPresent) {
                output += "N";
            }

            if (!PlayModeMenuItem.Get()) {
                output += "P";
            }
            
            if (!UseInUploadMenuItem.Get()) {
                output += "U";
            }

            if (!ConstrainedProportionsMenuItem.Get()) {
                output += "C";
            }

            if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("0ad731f6b84696142a169af045691c7b"))
                || !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("ba7e30ad00ad0c247a3f4e816f1f7d53"))
                || !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("cc05f54cef1ff194fb23f8c1d552c492"))) {
                output += "B";
            }
            
            if (!File.Exists("Packages/vpm-manifest.json")) {
                output += "V";
            }

            if (output != "") output += " ";
            
            output += Application.unityVersion;

            var vrcsdkAvatar = VRCFPackageUtils.GetVersionFromId("com.vrchat.avatars");
            var vrcsdkBase = VRCFPackageUtils.GetVersionFromId("com.vrchat.base");
            output += " " + vrcsdkAvatar;
            if (vrcsdkBase != vrcsdkAvatar) output += "x" + vrcsdkBase;

            output += " " + VRCFPackageUtils.Version;

            return output;
        }
    }
}
