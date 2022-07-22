using System;
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
            var skins = avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var skin in skins) {
                var root = skin.rootBone == null ? skin.transform : skin.rootBone;
                var avgScale = (root.lossyScale.x + root.lossyScale.y + root.lossyScale.z) / 3;
                var minExtentWorld = 0.5f; // 0.5 meters
                var minExtentLocal = minExtentWorld / avgScale;
                var bounds = skin.localBounds;
                var extents = bounds.extents;
                var changed = false;
                if (extents.x < minExtentLocal) { changed = true; extents.x = minExtentLocal; }
                if (extents.y < minExtentLocal) { changed = true; extents.y = minExtentLocal; }
                if (extents.z < minExtentLocal) { changed = true; extents.z = minExtentLocal; }
                if (changed) {
                    bounds.extents = extents;
                    skin.localBounds = bounds;
                }
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
