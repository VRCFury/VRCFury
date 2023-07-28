using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Utils;

namespace VF.Feature {
    /** This builder is responsible for moving objects for other builders,
     * then fixing any animations that referenced those objects.
     *
     * The reason we can't just move objects and rewrite the animations immediately when needed,
     * is because some animations may not be present on the avatar yet. Specifically, FullController
     * may add more animations to the avatar later on, and those may use the pre-moved paths.
     */
    public class ObjectMoveBuilder : FeatureBuilder {
        private readonly List<AnimationClip> additionalClips = new List<AnimationClip>();

        public void Move(VFGameObject obj, GameObject newParent = null, string newName = null, bool worldPositionStays = true) {
            var oldPath = clipBuilder.GetPath(obj);
            if (newParent != null)
                obj.transform.SetParent(newParent.transform, worldPositionStays);
            if (newName != null)
                obj.name = newName;
            var newPath = clipBuilder.GetPath(obj);
            PhysboneUtils.RemoveFromPhysbones(obj, true);
            DirectRewrite(oldPath, newPath);
        }

        public void DirectRewrite(GameObject oldObj, GameObject newObj) {
            var oldPath = clipBuilder.GetPath(oldObj);
            var newPath = clipBuilder.GetPath(newObj);
            DirectRewrite(oldPath, newPath);
        }
        
        public void DirectRewrite(string from, string to) {
            string RewritePath(string path) {
                if (path.StartsWith(from + "/") || path == from) {
                    path = to + path.Substring(from.Length);
                }
                return path;
            }
            
            foreach (var controller in manager.GetAllUsedControllers()) {
                controller.GetRaw().RewritePaths(RewritePath);
            }
            foreach (var clip in additionalClips) {
                clip.RewritePaths(RewritePath);
            }
        }

        public void AddAdditionalManagedClip(AnimationClip clip) {
            additionalClips.Add(clip);
        }
    }
}
