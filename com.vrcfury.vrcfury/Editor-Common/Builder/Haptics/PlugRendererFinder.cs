using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using VF.Component;
using VF.Utils;

namespace VF.Builder.Haptics {
    /** Resolves a plug's configured or automatically selected renderers. */
    internal static class PlugRendererFinder {
        public static ICollection<Renderer> GetRenderers(VRCFuryHapticPlug plug) {
            var renderers = new List<Renderer>();
            if (plug.autoRenderer) {
                renderers.AddRange(GetAutoRenderer(plug.owner(), !plug.useLegacyRendererFinder));
            } else {
                renderers.AddRange(plug.configureTpsMesh.Where(r => r != null));
            }
            return renderers;
        }

        private static IImmutableList<Renderer> GetAutoRenderer(VFGameObject obj, bool newSearchAlgo) {
            if (newSearchAlgo) {
                var current = obj;

                while (current != null) {
                    var foundOnObject = current.GetComponents<Renderer>().ToImmutableList();
                    if (foundOnObject.Count == 1) {
                        return foundOnObject;
                    } else if (foundOnObject.Count > 1) {
                        return ImmutableList<Renderer>.Empty;
                    }

                    var children = current.GetSelfAndAllChildren().ToImmutableHashSet();
                    var bestRenderer = current.uploadRoots
                        .SelectMany(root => root.GetComponentsInSelfAndChildren<Renderer>())
                        .Where(r => !r.owner().IsSameOrChildOf(current))
                        .Select(r => (r, GetVertsWeightedToBone(r, children)))
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
            bool IsDps(Renderer r) => !dpsOrTpsOnly || TpsUtils.HasDpsOrTpsMaterial(r);

            var foundOnObject = obj.GetComponents<Renderer>().Where(IsDps).ToImmutableList();
            if (foundOnObject.Count > 0) return foundOnObject;

            var foundInChildren = obj.GetComponentsInSelfAndChildren<Renderer>().Where(IsDps).ToImmutableList();
            if (foundInChildren.Count > 0) return foundInChildren;

            var parent = obj.parent;
            while (parent != null) {
                var foundOnParent = parent.GetComponents<Renderer>().Where(IsDps).ToImmutableList();
                if (foundOnParent.Count > 0) return foundOnParent;

                var foundOnChildOfParent = parent.Children()
                    .SelectMany(child => child.GetComponents<Renderer>().Where(IsDps))
                    .ToImmutableList();
                if (foundOnChildOfParent.Count > 0) return foundOnChildOfParent;
                parent = parent.parent;
            }

            return ImmutableList.Create<Renderer>();
        }

        private static long GetVertsWeightedToBone(Renderer r, IImmutableSet<VFGameObject> children) {
            if (!(r is SkinnedMeshRenderer skin)) return 0;
            var mesh = skin.GetMesh();
            if (mesh == null) return 0;
            var bones = skin.bones;
            bool[] included = null;
            for (var index = 0; index < bones.Length; index++) {
                if (!children.Contains(bones[index])) continue;
                (included ??= new bool[bones.Length])[index] = true;
            }
            if (included == null) return 0;

            bool Matches(int index) => (uint)index < (uint)included.Length && included[index];
            var count = 0;
            foreach (var weight in mesh.boneWeights) {
                if ((weight.weight0 > 0 && Matches(weight.boneIndex0))
                    || (weight.weight1 > 0 && Matches(weight.boneIndex1))
                    || (weight.weight2 > 0 && Matches(weight.boneIndex2))
                    || (weight.weight3 > 0 && Matches(weight.boneIndex3))) {
                    count++;
                }
            }
            return count;
        }
    }
}
