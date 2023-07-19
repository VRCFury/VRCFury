using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {

    public class BoundingBoxFixBuilder : FeatureBuilder<BoundingBoxFix2> {

        [FeatureBuilderAction(FeatureOrder.BoundingBoxFix)]
        public void Apply() {
            if (this != allBuildersInRun.OfType<BoundingBoxFixBuilder>().First()) {
                return;
            }

            var skins = new HashSet<SkinnedMeshRenderer>();
            var skipSkins = new HashSet<SkinnedMeshRenderer>();
            foreach (var component in allFeaturesInRun.OfType<BoundingBoxFix2>()) {
                if (component.singleRenderer) {
                    skins.Add(component.singleRenderer);
                } else if (component.skipRenderer) {
                    skipSkins.Add(component.skipRenderer);
                } else {
                    skins.UnionWith(avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>());
                }
            }
            skins.RemoveWhere(s => skipSkins.Contains(s));

            float maxLinear = 0;
            Renderer maxRenderer = null;
            foreach (var renderer in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                var bounds = renderer.bounds;
                bounds.Encapsulate(avatarObject.transform.position);
                var extents = bounds.extents;
                var linear = extents.x + extents.y + extents.z;
                if (linear > maxLinear) {
                    maxLinear = linear;
                    maxRenderer = renderer;
                }
            }

            if (maxRenderer != null) {
                Debug.Log($"Largest renderer is {clipBuilder.GetPath(maxRenderer.transform)} with linear size of {maxLinear}");
            }

            foreach (var skin in skins) {
                AdjustBoundingBox(skin);
            }
        }

        public static void AdjustBoundingBox(SkinnedMeshRenderer skin) {
            var avatarObject = skin.owner().GetComponentInSelfOrParent<VRCAvatarDescriptor>()?.gameObject;
            if (avatarObject == null) return;

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
                return true;
            }

            var stepSizeInMeters = 0.05f;
            var maxSteps = 20;
            var stepSize = stepSizeInMeters / root.transform.lossyScale.x;
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
            var bounds = new Bounds(avatarObject.transform.position, Vector3.zero);
            foreach (Renderer renderer in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }

        public override bool AvailableOnProps() {
            return false;
        }
        
        public override string GetEditorTitle() {
            return "Bounding Box Fix";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will ensure a minimum size of your avatar's bounding boxes. " +
                "This will prevent small objects on your avatar from disappearing when near the camera."
            ));
            return content;
        }
    }
}
