using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Model.Feature;

namespace VF.Feature {

    public class BoundingBoxFixBuilder : FeatureBuilder<BoundingBoxFix> {

        [FeatureBuilderAction(applyToVrcClone:true,priority:10)]
        public void ApplyOnClone() {
            var first = true;
            var overallWorldBounds = new Bounds();

            var skins = avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (var skin in skins) {
                if (skin.rootBone == null) continue;
                if (first) {
                    first = false;
                    overallWorldBounds.SetMinMax(skin.bounds.min, skin.bounds.max);
                } else {
                    overallWorldBounds.Encapsulate(skin.bounds.min);
                    overallWorldBounds.Encapsulate(skin.bounds.max);
                }
            }

            foreach (var skin in skins) {
                if (skin.rootBone == null) continue;
                var localBounds = skin.localBounds;
                foreach (var corner in GetCorners(overallWorldBounds)) {
                    localBounds.Encapsulate(skin.rootBone.InverseTransformPoint(corner));
                }
                skin.localBounds = localBounds;
            }
        }

        public override bool AvailableOnProps() {
            return false;
        }
        
        public override string GetEditorTitle() {
            return "Bounding Box Fix";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(new Label() {
                text = "This feature will resize the bounding box of all meshes to your avatar's size, preventing small objects from disappearing when near your camera in game.",
                style = {
                    whiteSpace = WhiteSpace.Normal
                }
            });
            return content;
        }
        
        private static List<Vector3> GetCorners(Bounds obj, bool includePosition = true)
        {
            var result = new List<Vector3>();
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2) {
                var offset = (obj.size / 2);
                offset.Scale(new Vector3(x, y, z));
                result.Add((includePosition ? obj.center : Vector3.zero) + offset);
            }
            return result;
        }
    }
}
