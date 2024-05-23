using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Menu;

namespace VF.Service {
    [VFService]
    public class BoundingBoxFixService {
        [VFAutowired] private readonly GlobalsService globals;
        private VFGameObject avatarObject => globals.avatarObject;

        [FeatureBuilderAction(FeatureOrder.BoundingBoxFix)]
        public void Apply() {
            if (!BoundingBoxMenuItem.Get()) return;
            
            var skins = avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()
                .Where(skin => {
                    var usesLights = skin.sharedMaterials.Any(mat =>
                        DpsConfigurer.IsDps(mat) || TpsConfigurer.IsTps(mat) || SpsConfigurer.IsSps(mat));
                    if (usesLights) return false;
                    
                    // Some systems, like the VRLabs IsRendering system, use an empty renderer to determine if you're "looking" at an object or not
                    // They don't have a mesh set at all
                    if (skin.sharedMesh == null) return false;

                    return true;
                });

            float maxLinear = 0;
            Renderer maxRenderer = null;
            foreach (var renderer in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
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

            foreach (var skin in skins) {
                AdjustBoundingBox(skin);
            }
        }

        private void AdjustBoundingBox(SkinnedMeshRenderer skin) {
            var startBounds = CalculateFullBounds(avatarObject);

            //var debug = skin.gameObject.name == "Body";
            var root = HapticUtils.GetMeshRoot(skin);

            bool ModifyBounds(float sizeX = 0, float sizeY = 0, float sizeZ = 0, float centerX = 0, float centerY = 0, float centerZ = 0) {
                var b = skin.localBounds;
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

                var fullBak = startBounds;
                var updatedBounds = GetUpdatedBounds(skin, b);
                var fullNew = fullBak;
                fullNew.Encapsulate(updatedBounds);
                //if (debug) Debug.Log("Expanding to " + b + " updated world bounds: " + updatedBounds);
                if (fullNew != fullBak) {
                    //if (debug) Debug.LogError("FAILED");
                    return false;
                }
                skin.localBounds = b;
                skin.updateWhenOffscreen = false;
                return true;
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

        private static Bounds GetUpdatedBounds(SkinnedMeshRenderer skin, Bounds newBounds) {
            var root = HapticUtils.GetMeshRoot(skin);

            List<Vector3> GetLocalCorners(Bounds obj) {
                var result = new List<Vector3>();
                for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                    result.Add(obj.center + Times(obj.extents, new Vector3(x, y, z)));
                return result;
            }
        
            Vector3 Times(Vector3 self, Vector3 other) =>
                new Vector3(self.x * other.x, self.y * other.y, self.z * other.z);

            var corners = GetLocalCorners(newBounds);
            var b = new Bounds(root.TransformPoint(newBounds.center), Vector3.zero);
            foreach (var corner in corners) {
                b.Encapsulate(root.TransformPoint(corner));
            }

            return b;
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