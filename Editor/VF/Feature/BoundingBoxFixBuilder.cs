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

            var skins = avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var skin in skins) {
                var worldMin = skin.transform.TransformPoint(skin.localBounds.min);
                var worldMax = skin.transform.TransformPoint(skin.localBounds.max);
                if (first) {
                    first = false;
                    overallWorldBounds.SetMinMax(worldMin, worldMax);
                } else {
                    overallWorldBounds.Encapsulate(worldMin);
                    overallWorldBounds.Encapsulate(worldMax);
                }
            }

            foreach (var skin in skins) {
                var localBounds = new Bounds();
                localBounds.SetMinMax(
                    skin.transform.InverseTransformPoint(overallWorldBounds.min),
                    skin.transform.InverseTransformPoint(overallWorldBounds.max)
                );
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
    }
}
