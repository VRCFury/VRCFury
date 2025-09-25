using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Menu;
using VF.Utils;

namespace VF.Service {
    [VFService]
    internal class BoundingBoxFixService {
        [VFAutowired] private readonly GlobalsService globals;
        private VFGameObject avatarObject => globals.avatarObject;

        [FeatureBuilderAction(FeatureOrder.BoundingBoxFix)]
        public void Apply() {
            if (!BoundingBoxMenuItem.Get()) return;

            // The VRCSDK only counts bounds if the renderer has a mesh assigned
            var renderers = avatarObject.GetComponentsInSelfAndChildren<Renderer>()
                .Where(r => r.GetMesh() != null)
                .ToList();

            DebugLargestBox(renderers);

            foreach (var skin in renderers.OfType<SkinnedMeshRenderer>()) {
                AdjustBoundingBox(skin);
            }
        }

        private void DebugLargestBox(IList<Renderer> renderers) {
            float maxLinear = 0;
            Renderer maxRenderer = null;
            foreach (var renderer in renderers) {
                var bounds = renderer.bounds;
                bounds.Encapsulate(avatarObject.worldPosition);
                var extents = bounds.extents;
                var linear = extents.x + extents.y + extents.z;
                if (linear > maxLinear) {
                    maxLinear = linear;
                    maxRenderer = renderer;
                }
            }

            if (maxRenderer != null) {
                Debug.Log($"Largest renderer is {maxRenderer.owner().GetPath(avatarObject)} with linear size of {maxLinear}");
            }
        }

        private void AdjustBoundingBox(SkinnedMeshRenderer skin) {
            var usesLights = skin.sharedMaterials.Any(mat =>
                DpsConfigurer.IsDps(mat) || TpsConfigurer.IsTps(mat) || SpsConfigurer.IsSps(mat));
            if (usesLights) return;

            skin.updateWhenOffscreen = false;
            var startBounds = CalculateFullBounds(avatarObject);

            var root = HapticUtils.GetMeshRoot(skin);

            void ModifyBounds(float sizeX = 0, float sizeY = 0, float sizeZ = 0, float centerX = 0, float centerY = 0, float centerZ = 0) {
                var original = skin.localBounds;
                var b = original;
                var extents = b.extents;
                extents.x += sizeX;
                extents.y += sizeY;
                extents.z += sizeZ;
                b.extents = extents;
                var center = b.center;
                center.x += centerX;
                center.y += centerY;
                center.z += centerZ;
                b.center = center;

                skin.localBounds = b;
                var newAvatarBounds = startBounds;
                newAvatarBounds.Encapsulate(skin.bounds);
                //if (debug) Debug.Log("Expanding to " + b + " updated world bounds: " + updatedBounds);
                if (startBounds != newAvatarBounds) {
                    //if (debug) Debug.LogError("FAILED");
                    skin.localBounds = original;
                }
            }

            var stepSizeInMeters = 0.05f;
            var maxSteps = 20;
            var stepSize = stepSizeInMeters / root.worldScale.x;
            for (var i = 0; i < maxSteps; i++) {
                ModifyBounds(sizeX: stepSize, centerX: -stepSize);
                ModifyBounds(sizeX: stepSize, centerX: stepSize);
                ModifyBounds(sizeY: stepSize, centerY: -stepSize);
                ModifyBounds(sizeY: stepSize, centerY: stepSize);
                ModifyBounds(sizeZ: stepSize, centerZ: -stepSize);
                ModifyBounds(sizeZ: stepSize, centerZ: stepSize);
            }

            VRCFuryEditorUtils.MarkDirty(skin);
        }

        private static Bounds CalculateFullBounds(VFGameObject avatarObject) {
            var bounds = new Bounds(avatarObject.worldPosition, Vector3.zero);
            foreach (var renderer in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }
    }
}
