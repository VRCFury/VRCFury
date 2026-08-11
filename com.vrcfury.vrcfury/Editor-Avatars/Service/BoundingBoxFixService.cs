using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Feature.Base;
using VF.Injector;
using VF.Menu;
using VF.Utils;

namespace VF.Service {
    [VFService]
    internal class BoundingBoxFixService {
        private const float MaxStepSizeInMeters = 5f;
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

        private void AdjustBoundingBox(Renderer renderer) {
            if (renderer == null) return;

            var usesLights = renderer.sharedMaterials.Any(mat =>
                DpsConfigurer.IsDps(mat) || TpsConfigurer.IsTps(mat));
            if (usesLights) return;

            if (renderer is SkinnedMeshRenderer skin) {
                skin.updateWhenOffscreen = false;
                skin.Dirty();
            }

            var startBounds = CalculateFullBounds(avatarObject);
            var root = renderer.GetRootBone();
            var currentBounds = renderer.GetLocalBounds();

            void ModifyBounds(float sideX = 0, float sideY = 0, float sideZ = 0) {
                var original = currentBounds;
                var b = original;
                var extents = b.extents;
                extents.x += Mathf.Abs(sideX) / 2;
                extents.y += Mathf.Abs(sideY) / 2;
                extents.z += Mathf.Abs(sideZ) / 2;
                b.extents = extents;
                var center = b.center;
                center.x += sideX / 2;
                center.y += sideY / 2;
                center.z += sideZ / 2;
                b.center = center;

                currentBounds = b;
                renderer.SetLocalBounds(b);
                var newAvatarBounds = startBounds;
                newAvatarBounds.Encapsulate(renderer.bounds);
                if (!Approximately(startBounds, newAvatarBounds)) {
                    currentBounds = original;
                    renderer.SetLocalBounds(original);
                }
            }

            var minStepInMeters = 0.01f;
            var stepSizeInMeters = Mathf.Min(GetMaxSize(startBounds), MaxStepSizeInMeters);
            while (stepSizeInMeters >= minStepInMeters) {
                var stepX = GetLocalStep(stepSizeInMeters, root.worldScale.x);
                var stepY = GetLocalStep(stepSizeInMeters, root.worldScale.y);
                var stepZ = GetLocalStep(stepSizeInMeters, root.worldScale.z);
                ModifyBounds(sideX: -stepX);
                ModifyBounds(sideX: stepX);
                ModifyBounds(sideY: -stepY);
                ModifyBounds(sideY: stepY);
                ModifyBounds(sideZ: -stepZ);
                ModifyBounds(sideZ: stepZ);
                stepSizeInMeters /= 2;
            }
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

        private static float GetLocalStep(float worldStep, float scale) {
            var absScale = Mathf.Abs(scale);
            if (absScale < 0.0001f) return 0;
            return worldStep / absScale;
        }

        private static float GetMaxSize(Bounds bounds) {
            var size = bounds.size;
            return Mathf.Max(size.x, size.y, size.z);
        }

        private static bool Approximately(Bounds a, Bounds b) {
            return Approximately(a.center, b.center) && Approximately(a.extents, b.extents);
        }

        private static bool Approximately(Vector3 a, Vector3 b) {
            const float tolerance = 0.0001f;
            return Mathf.Abs(a.x - b.x) <= tolerance
                && Mathf.Abs(a.y - b.y) <= tolerance
                && Mathf.Abs(a.z - b.z) <= tolerance;
        }
    }
}
