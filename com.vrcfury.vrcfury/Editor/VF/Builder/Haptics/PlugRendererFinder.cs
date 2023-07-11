using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;

namespace VF.Builder.Haptics {
    /** Automatically finds the renderer to use for a plug */
    public class PlugRendererFinder {
        public class Params {
            public bool PreferDps = true;
            public bool SearchChildren = true;
            public bool PreferWeightedToBone = false;
            public bool EmptyIfMultiple = false;
        }
        
        public static IImmutableList<Renderer> GetAutoRenderer(GameObject obj, Params p) {
            if (p.PreferDps) {
                var foundWithDps = GetAutoRendererInner(obj, p, true);
                if (foundWithDps.Count > 0) return foundWithDps;
            }
            var found = GetAutoRendererInner(obj, p, false);
            if (p.PreferWeightedToBone) {
                found = PreferWeightedToBone(found, obj.transform);
            }
            if (found.Count > 1 && p.EmptyIfMultiple) found = ImmutableList<Renderer>.Empty;
            return found;
        }

        private static IImmutableList<Renderer> GetAutoRendererInner(GameObject obj, Params p, bool dpsOnly) {
            bool IsDps(Renderer r) => !dpsOnly || PlugSizeDetector.HasDpsMaterial(r);

            var foundOnObject = obj.GetComponents<Renderer>().Where(IsDps).ToImmutableList();
            if (foundOnObject.Count > 0) return foundOnObject;

            if (p.SearchChildren) {
                var foundInChildren = obj.GetComponentsInChildren<Renderer>(true).Where(IsDps).ToImmutableList();
                if (foundInChildren.Count > 0) return foundInChildren;
            }

            var parent = obj.transform.parent;
            while (parent != null) {
                var foundOnParent = parent.GetComponents<Renderer>().Where(IsDps).ToImmutableList();
                if (foundOnParent.Count > 0) return foundOnParent;

                var foundOnChildOfParent = Enumerable.Range(0, parent.childCount)
                    .Select(childNum => parent.GetChild(childNum))
                    .SelectMany(child => child.GetComponents<Renderer>().Where(IsDps))
                    .ToImmutableList();
                if (foundOnChildOfParent.Count > 0) return foundOnChildOfParent;
                parent = parent.parent;
            }

            return ImmutableList.Create<Renderer>();
        }

        private static IImmutableList<Renderer> PreferWeightedToBone(IImmutableList<Renderer> inputs, Transform bone) {
            if (inputs.Count <= 1) return inputs.ToImmutableList();
            var rendererToCount = inputs.ToDictionary(r => r, r => GetVertsWeightedToBone(r, bone));
            var maxCount = rendererToCount.Values.Max();
            var filtered = rendererToCount.Where(pair => pair.Value == maxCount).Select(pair => pair.Key).ToImmutableList();
            if (filtered.Count <= 1) return filtered;
            if (bone.parent == null) return filtered;
            return PreferWeightedToBone(inputs, bone.parent);
        }
        
        private static long GetVertsWeightedToBone(Renderer r, Transform bone) {
            if (!(r is SkinnedMeshRenderer skin)) return 0;
            var mesh = skin.sharedMesh;
            if (!mesh) return 0;
            var children = bone.GetComponentsInChildren<Transform>(true)
                .ToImmutableHashSet();
            var childrenBoneIds = skin.bones.Select((b, i) => (b, i))
                .Where(pair => children.Contains(pair.b))
                .Select(pair => pair.i)
                .ToImmutableHashSet();
            return mesh.boneWeights.Count(weight => {
                var match = weight.weight0 > 0 && childrenBoneIds.Contains(weight.boneIndex0);
                match |= weight.weight1 > 0 && childrenBoneIds.Contains(weight.boneIndex1);
                match |= weight.weight2 > 0 && childrenBoneIds.Contains(weight.boneIndex2);
                match |= weight.weight3 > 0 && childrenBoneIds.Contains(weight.boneIndex3);
                return match;
            });
        }
    }
}