using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Component;
using VF.Exceptions;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class SpsPlugPreparation {
        public static void ImportSettingsFromLegacySystems(IEnumerable<VRCFuryHapticPlug> plugs, bool shouldSaveMasks = false) {
            foreach (var plug in plugs) {
                try {
                    ImportSettingsFromLegacySystems(plug, PlugRendererFinder.GetRenderers(plug), shouldSaveMasks);
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to migrate legacy inputs for SPS Plug: {plug.owner().GetDebugPath()}", e);
                }
            }
        }

        private static void SetPlugPose(VRCFuryHapticPlug plug, Vector3 position, Quaternion rotation) {
            plug.position = plug.owner().InverseTransformPoint(position);
            plug.rotation = (Quaternion.Inverse(plug.owner().worldRotation) * rotation).eulerAngles;
        }

        public static void ImportSettingsFromLegacySystems(VRCFuryHapticPlug plug, ICollection<Renderer> renderers, bool shouldSaveMasks = false) {
            var renderer = renderers.FirstOrDefault(TpsUtils.HasDpsOrTpsMaterial);
            var testBase = plug.owner().Find("OGBTestBase");
            if (renderer == null && testBase == null) return;
            var scale = plug.unitsInMeters ? 1 : plug.owner().worldScale.x;
            if (plug.autoRenderer) {
                plug.configureTpsMesh = renderers.ToList();
                plug.autoRenderer = false;
            }
            if (renderer != null) {
                SetPlugPose(plug, renderer.GetRootBone().worldPosition, GetLegacyRotation(renderer));
                // Resolve the legacy frame before switching to SPS deformation.
                plug.enableSps = true;
                if (plug.spsAnimatedEnabled == 1 && TpsUtils.GetAnimatedEnabled(renderer) is float animatedEnabled) {
                    plug.spsAnimatedEnabled = animatedEnabled;
                }
                if (plug.autoLength && DpsUtils.GetWorldLength(renderer) is float worldLength) {
                    plug.length = worldLength / scale;
                    plug.autoLength = false;
                }
            }
            if (testBase != null) {
                SetPlugPose(plug, testBase.worldPosition, testBase.worldRotation);
            }
            // Capture dimensions before importing a mask that may exclude the entire plug.
            if (renderers.Count > 0 && (plug.autoLength || plug.autoRadius)) {
                var size = PlugSizeDetector.GetWorldSize(plug, renderers);
                if (plug.autoLength) {
                    plug.length = size.worldLength / scale;
                    plug.autoLength = false;
                }
                if (plug.autoRadius) {
                    plug.radius = size.worldRadius / scale;
                    plug.autoRadius = false;
                }
            }
            ImportMask(plug, renderers, shouldSaveMasks);
        }

        public static Quaternion GetLegacyRotation(Renderer renderer) {
            var rootRotation = renderer.GetRootBone().worldRotation;
            foreach (var material in renderer.sharedMaterials) {
                if (DpsUtils.IsDps(material)) return rootRotation;
                if (TpsUtils.IsTps(material)) return rootRotation * TpsUtils.GetTpsRotation(material);
            }
            return rootRotation;
        }

        private static void ImportMask(VRCFuryHapticPlug plug, IEnumerable<Renderer> renderers, bool shouldSaveMasks) {
            if (plug.textureMask.Get() != null) return;
            foreach (var renderer in renderers) {
                foreach (var material in renderer.sharedMaterials) {
                    var mask = TpsUtils.GetMask(material, renderer.GetMesh());
                    if (mask == null) continue;
                    if (shouldSaveMasks) {
                        var materialPath = AssetDatabase.GetAssetPath(material);
                        var path = !materialPath.StartsWith("Assets/") ? "Assets/SPS Mask.asset"
                            : Path.GetDirectoryName(materialPath) + "/" + Path.GetFileNameWithoutExtension(materialPath) + " SPS Mask.asset";
                        mask.hideFlags = HideFlags.None;
                        AssetDatabase.CreateAsset(mask, AssetDatabase.GenerateUniqueAssetPath(path));
                    }
                    plug.textureMask = mask;
                    return;
                }
            }
        }
    }
}
