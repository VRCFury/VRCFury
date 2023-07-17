using System.Collections.Immutable;
using System.Linq;
using UnityEngine;

namespace VF.Builder.Haptics {
    /** Automatically finds the renderer to use for a plug */
    public class PlugRendererFinder {
        public static IImmutableList<Renderer> GetAutoRenderer(VFGameObject obj, bool newSearchAlgo) {
            if (newSearchAlgo) {
                var current = obj;

                while (current != null) {
                    var foundOnObject = current.GetComponents<Renderer>().ToImmutableList();
                    if (foundOnObject.Count == 1) {
                        return foundOnObject;
                    } else if (foundOnObject.Count > 1) {
                        return ImmutableList<Renderer>.Empty;
                    }

                    var bestRenderer = current.root.GetComponentsInSelfAndChildren<Renderer>()
                        .Where(r => !r.owner().IsChildOf(current))
                        .Select(r => (r, GetVertsWeightedToBone(r, current)))
                        .Where(pair => pair.Item2 > 0)
                        .OrderByDescending(pair => pair.Item2)
                        .Select(pair => pair.Item1)
                        .FirstOrDefault();
                    if (bestRenderer != null) {
                        return ImmutableList.Create(bestRenderer);
                    }

                    current = current.parent;
                }

                return ImmutableList<Renderer>.Empty;
            }

            var foundWithDps = GetAutoRendererInner(obj, true);
            if (foundWithDps.Count > 0) return foundWithDps;
            return GetAutoRendererInner(obj, false);
        }

        private static IImmutableList<Renderer> GetAutoRendererInner(VFGameObject obj, bool dpsOrTpsOnly) {
            bool IsDps(Renderer r) => !dpsOrTpsOnly || TpsConfigurer.HasDpsOrTpsMaterial(r);

            var foundOnObject = obj.GetComponents<Renderer>().Where(IsDps).ToImmutableList();
            if (foundOnObject.Count > 0) return foundOnObject;

            var foundInChildren = obj.GetComponentsInSelfAndChildren<Renderer>().Where(IsDps).ToImmutableList();
            if (foundInChildren.Count > 0) return foundInChildren;

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

        private static long GetVertsWeightedToBone(Renderer r, VFGameObject bone) {
            if (!(r is SkinnedMeshRenderer skin)) return 0;
            var mesh = skin.sharedMesh;
            if (!mesh) return 0;
            var children = bone.GetSelfAndAllChildren()
                .ToImmutableHashSet();
            var childrenBoneIds = skin.bones.Select((b, i) => (b, i))
                // TODO: Verify .contains works when it's two different instances
                .Where(pair => children.Contains(pair.b))
                .Select(pair => pair.i)
                .ToImmutableHashSet();
            if (childrenBoneIds.Count == 0) return 0;
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
