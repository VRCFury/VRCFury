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

        public static string GetDebugChars([CanBeNull] VFGameObject avatarObject = null, bool? isStillBroken = null) {
            var output = "";

            bool wdBroken;
            if (isStillBroken.HasValue) {
                wdBroken = isStillBroken.Value;
            } else {
                ICollection<VRCFury> vrcfComponents;
                if (avatarObject == null) {
                    vrcfComponents = Resources.FindObjectsOfTypeAll<VRCFury>();
                } else {
                    vrcfComponents = avatarObject.GetComponentsInSelfAndChildren<VRCFury>();
                }

                wdBroken = vrcfComponents
                    .SelectMany(c => c.GetAllFeatures())
                    .OfType<FixWriteDefaults>()
                    .Any(fwd => fwd.mode == FixWriteDefaults.FixWriteDefaultsMode.Disabled);
            }

            if (wdBroken) {
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

            if (!AutoUpgradeConstraintsMenuItem.Get()) {
                output += "A";
            }

            if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("0ad731f6b84696142a169af045691c7b"))
                || !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("ba7e30ad00ad0c247a3f4e816f1f7d53"))
                || !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("cc05f54cef1ff194fb23f8c1d552c492"))
                || !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("0679cc74827adad45ace27875d62ef6e"))
                || Directory.Exists("Packages/" + VRCFuryEditorUtils.Rev("enrobdoolb.nisaeca.moc"))
            ) {
                output += "B";
            }
            
            if (!File.Exists("Packages/vpm-manifest.json")) {
                output += "V";
            }

            return output;
        }

        public static IList<string> GetParts([CanBeNull] VFGameObject avatarObject = null, bool? isStillBroken = null) {
            var parts = new List<string>();

            parts.Add(GetDebugChars(avatarObject, isStillBroken));

            parts.Add(Application.unityVersion);

            var vrcsdkAvatar = VRCFPackageUtils.GetVersionFromId("com.vrchat.avatars");
            var vrcsdkBase = VRCFPackageUtils.GetVersionFromId("com.vrchat.base");
            
            parts.Add(vrcsdkAvatar);
            if (vrcsdkBase != vrcsdkAvatar) parts.Add("x" + vrcsdkBase);

            parts.Add(VRCFPackageUtils.Version);

            return parts.Where(p => !string.IsNullOrEmpty(p)).ToArray();
        }

        public static string GetOutputString([CanBeNull] VFGameObject avatarObject = null) {
            return GetParts(avatarObject).Join(" ");
        }
    }
}
