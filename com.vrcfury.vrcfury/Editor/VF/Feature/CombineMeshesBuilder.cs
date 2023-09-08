using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using static System.Linq.Enumerable;

namespace VF.Feature
{
    public class CombineMeshesBuilder : FeatureBuilder<CombineMeshes>
    {
        private static string[] preferredBaseMeshNames = new string[] { "body", "main" };

        public override string GetEditorTitle()
        {
            return "Combine Meshes";
        }

        public override VisualElement CreateEditor(SerializedProperty prop)
        {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will combine all meshes that are not animated to be toggled into one, " +
                " saving extra draw calls!"
            ));

            return content;
        }

        public override bool AvailableOnProps()
        {
            return false;
        }

        [FeatureBuilderAction(FeatureOrder.BlendshapeOptimizer)]
        public void Apply()
        {

            var skinGroups = new Dictionary<SkinnedMeshRendererGroup, List<SkinnedMeshRenderer>>();

            foreach (var (renderer, mesh, setMesh) in RendererIterator.GetRenderersWithMeshes(avatarObject))
            {
                if (!(renderer is SkinnedMeshRenderer skin)) continue;

                var group = new SkinnedMeshRendererGroup(skin);
                if (!skinGroups.ContainsKey(group))
                {
                    skinGroups.Add(group, new List<SkinnedMeshRenderer>());
                }
                var entries = skinGroups[group];

                if (Array.IndexOf(preferredBaseMeshNames, skin.name.ToLower()) != -1)
                {
                    entries.Insert(0, skin);
                }
                else
                {
                    entries.Add(skin);
                }
            }

            var animatedBindings = manager.GetAllUsedControllersRaw()
                      .Select(tuple => tuple.Item2)
                      .SelectMany(controller => GetBindings(avatarObject, controller))
                      .Concat(avatarObject.GetComponentsInSelfAndChildren<Animator>()
                          .SelectMany(animator => GetBindings(animator.gameObject, animator.runtimeAnimatorController as AnimatorController)))
                      .Where(a =>
                      {
                          var (binding, _) = a;
                          return binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive";
                      })
                      .ToList();

            bool IsObjectToggled(GameObject obj)
            {
                var path = clipBuilder.GetPath(obj.transform);

                foreach (var (binding, curve) in animatedBindings)
                {
                    if (binding.path == path)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool IsObjectDisabledPermanently(GameObject obj) {
                var current = obj;
                while (current != null && current != avatarObject) {
                    if (current.activeSelf || IsObjectToggled(obj)) {
                        return false;
                    }
                    current = current.transform.parent?.gameObject;
                }
                return true;
            }

            foreach (var entry in skinGroups)
            {
                var skins = entry.Value
                    .Where(skin => !IsObjectToggled(skin.gameObject))
                    .Where(skin =>
                    {
                        if (IsObjectDisabledPermanently(skin.gameObject))
                        {
                            VFGameObject gameObject = skin.gameObject;
                            gameObject.Destroy();
                            return false;
                        }
                        return true;
                    })
                    .ToList();


                if (skins.Count < 2)
                {
                    // Need at lest 2 meshes to combine
                    continue;
                }

                var basis = skins[0];
                var meshesToAdd = skins.Skip(1).ToList();
                basis.sharedMesh = mutableManager.MakeMutable(basis.sharedMesh, basis.owner()); ;
                VRCFuryEditorUtils.MarkDirty(basis);

                var combinable = new CombinableMesh(basis);
                foreach (var mesh in meshesToAdd)
                {
                    combinable.AddMesh(mesh);
                    VFGameObject obj = mesh.gameObject;
                    obj.Destroy();
                }
                combinable.Combine();
            }
        }

        private ICollection<(EditorCurveBinding, AnimationCurve)> GetBindings(GameObject obj, AnimatorController controller)
        {
            var prefix = AnimationUtility.CalculateTransformPath(obj.transform, avatarObject.transform);

            var clipsInController = new AnimatorIterator.Clips().From(controller);

            return clipsInController
                .SelectMany(clip => clip.GetFloatCurves())
                .Select(pair =>
                {
                    var (binding, curve) = pair;
                    binding.path = ClipRewriter.Join(prefix, binding.path, allowAdvancedOperators: false);
                    return (binding, curve);
                })
                .ToList();
        }

        private class CombinableMesh
        {
            SkinnedMeshRenderer skin;
            List<List<int>> indices;
            List<Material> materials;
            List<Vector3> vertices;
            List<Vector4> tangents;
            List<Vector3> normals;
            List<Vector2> uvs;
            List<Color> colors;
            List<BoneWeight> boneWeights;
            List<Transform> bones;
            Dictionary<string, int> blendshapeMapping;
            List<(string, List<(float, List<Vector3>, List<Vector3>, List<Vector3>)>)> blendshapes;

            public CombinableMesh(SkinnedMeshRenderer skin)
            {
                this.skin = skin;
                var mesh = this.skin.sharedMesh;

                indices = new List<List<int>>();
                for (var i = 0; i < mesh.subMeshCount; i++)
                {
                    var submeshIndices = new List<int>();
                    mesh.GetIndices(submeshIndices, i);
                    indices.Add(submeshIndices);
                }
                materials = new List<Material>(this.skin.sharedMaterials);
                vertices = new List<Vector3>();
                mesh.GetVertices(vertices);
                tangents = new List<Vector4>();
                mesh.GetTangents(tangents);
                normals = new List<Vector3>();
                mesh.GetNormals(normals);
                // TODO there are 8 UV channels
                uvs = new List<Vector2>();
                mesh.GetUVs(0, uvs);
                colors = new List<Color>();
                mesh.GetColors(colors);
                boneWeights = new List<BoneWeight>();
                mesh.GetBoneWeights(boneWeights);
                bones = new List<Transform>(this.skin.bones);

                blendshapeMapping = new Dictionary<string, int>();
                blendshapes = new List<(string, List<(float, List<Vector3>, List<Vector3>, List<Vector3>)>)>();
                foreach (var i in Range(0, mesh.blendShapeCount))
                {
                    var blendshapeName = mesh.GetBlendShapeName(i);
                    blendshapeMapping.Add(blendshapeName, i);
                    var frames = new List<(float, List<Vector3>, List<Vector3>, List<Vector3>)>();
                    foreach (var j in Range(0, mesh.GetBlendShapeFrameCount(i)))
                    {
                        var weight = mesh.GetBlendShapeFrameWeight(i, j);
                        var v = new Vector3[mesh.vertexCount];
                        var n = new Vector3[mesh.vertexCount];
                        var t = new Vector3[mesh.vertexCount];
                        mesh.GetBlendShapeFrameVertices(i, j, v, n, t);
                        frames.Add((weight, new List<Vector3>(v), new List<Vector3>(n), new List<Vector3>(t)));
                    }
                    blendshapes.Add((blendshapeName, frames));
                }
                mesh.ClearBlendShapes();
            }

            public void AddMesh(SkinnedMeshRenderer add)
            {
                var addMesh = add.sharedMesh;

                // Merge materials
                var materialMapping = new Dictionary<int, int>();
                for (var i = 0; i < add.sharedMaterials.Length; i++)
                {
                    var material = add.sharedMaterials[i];
                    var j = materials.IndexOf(material);
                    if (j == -1)
                    {
                        materials.Add(material);
                        j = materials.Count - 1;
                    }
                    materialMapping.Add(i, j);
                }

                // Merge submeshes
                var shiftBy = vertices.Count;
                foreach (var i in Range(0, addMesh.subMeshCount))
                {
                    var nextIndices = addMesh.GetIndices(i).Select(idx => idx + shiftBy).ToArray();
                    if (materialMapping.TryGetValue(i, out var j))
                    {
                        if (indices.Count <= j)
                        {
                            foreach (var _ in Range(0, indices.Count - j + 1))
                            {
                                indices.Add(new List<int>());
                            }
                        }
                        indices[j].AddRange(nextIndices);
                    }
                }

                // Merge remaining mesh data
                var matrix = skin.transform.worldToLocalMatrix * add.transform.localToWorldMatrix;
                if (!matrix.ValidTRS())
                {
                    throw new Exception("matrix is not a valid TRS");
                }
                var rotationScaleMatrix = Matrix4x4.TRS(Vector3.zero, matrix.rotation, matrix.lossyScale);
                var rotationMatrix = Matrix4x4.TRS(Vector3.zero, matrix.rotation, Vector3.one);

                vertices.AddRange(addMesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)));
                tangents.AddRange(addMesh.tangents.Select(v =>
                {
                    Vector3 pos = rotationMatrix.MultiplyPoint3x4(v);
                    return new Vector4(pos.x, pos.y, pos.z, v.w);
                }));
                normals.AddRange(addMesh.normals.Select(v => rotationMatrix.MultiplyPoint3x4(v)));
                uvs.AddRange(addMesh.uv);
                colors.AddRange(addMesh.colors);

                // Merge bones
                var boneMapping = new Dictionary<int, int>();
                var nextBones = add.bones;
                foreach (var i in Range(0, nextBones.Length))
                {
                    var bone = nextBones[i];
                    var j = bones.IndexOf(bone);
                    if (j == -1)
                    {
                        bones.Add(bone);
                        j = bones.Count - 1;
                    }
                    boneMapping.Add(i, j);
                }

                BoneWeight MapBoneWeight(BoneWeight weight)
                {
                    (int, float) Choose(int i, float w) => i >= boneMapping.Count ? (0, 0.0f) : (boneMapping[i], w);
                    (weight.boneIndex0, weight.weight0) = Choose(weight.boneIndex0, weight.weight0);
                    (weight.boneIndex1, weight.weight1) = Choose(weight.boneIndex1, weight.weight1);
                    (weight.boneIndex2, weight.weight2) = Choose(weight.boneIndex2, weight.weight2);
                    (weight.boneIndex3, weight.weight3) = Choose(weight.boneIndex3, weight.weight3);
                    return weight;
                }
                boneWeights.AddRange(addMesh.boneWeights.Select(w => MapBoneWeight(w)));

                // Merge Blendshapes
                var processedBlendshapes = new List<int>();
                foreach (var i in Range(0, addMesh.blendShapeCount))
                {
                    var blendshapeName = addMesh.GetBlendShapeName(i);
                    int blendshapeIndex;
                    List<(float, List<Vector3>, List<Vector3>, List<Vector3>)> frames;
                    // Merge with existing blendshape or create new one
                    if (blendshapeMapping.TryGetValue(blendshapeName, out blendshapeIndex))
                    {
                        (_, frames) = blendshapes[blendshapeIndex];
                    }
                    else
                    {
                        blendshapeIndex = blendshapes.Count;
                        frames = new List<(float, List<Vector3>, List<Vector3>, List<Vector3>)>();
                        blendshapes.Add((blendshapeName, frames));
                        blendshapeMapping.Add(blendshapeName, blendshapeIndex);
                    }
                    processedBlendshapes.Add(blendshapeIndex);
                    // Merge new blendshape frames
                    foreach (var j in Range(0, addMesh.GetBlendShapeFrameCount(i)))
                    {
                        var weight = addMesh.GetBlendShapeFrameWeight(i, j);
                        var v = new Vector3[addMesh.vertexCount];
                        var n = new Vector3[addMesh.vertexCount];
                        var t = new Vector3[addMesh.vertexCount];
                        addMesh.GetBlendShapeFrameVertices(i, j, v, n, t);

                        // Scale/rotate blendshape deltas
                        v = v.Select(x => rotationScaleMatrix.MultiplyPoint3x4(x)).ToArray();
                        n = n.Select(x => rotationMatrix.MultiplyPoint3x4(x)).ToArray();
                        t = t.Select(x => rotationMatrix.MultiplyPoint3x4(x)).ToArray();

                        if (frames.Count == 0)
                        {
                            var vn = new List<Vector3>(new Vector3[shiftBy]);
                            var nn = new List<Vector3>(new Vector3[shiftBy]);
                            var tn = new List<Vector3>(new Vector3[shiftBy]);
                            vn.AddRange(v);
                            nn.AddRange(n);
                            tn.AddRange(t);
                            frames.Add((weight, vn, nn, tn));
                        }
                        else
                        {
                            var insertPos = frames.Select(f => f.Item1).ToList().BinarySearch(weight);
                            if (insertPos >= 0)
                            {
                                // Found a frame with the same weight, add the new mesh's frame to the end
                                var (_, vn, nn, tn) = frames[insertPos];
                                vn.AddRange(v);
                                nn.AddRange(n);
                                tn.AddRange(t);
                            }
                            else
                            {
                                // Didn't find an exact match, insert a new frame, interpolating old values
                                insertPos = ~insertPos;
                                if (insertPos == frames.Count)
                                {
                                    // There should always be a frame at 100%, error out
                                    throw new Exception("error: there should always be a frame at 100%");
                                }
                                // Existing Frames   [-----|-------|---|-----]
                                // New Frames        [--|------|----------|--]
                                var frameBefore = insertPos == 0 ?
                                        (0, new List<Vector3>(new Vector3[shiftBy]), new List<Vector3>(new Vector3[shiftBy]), new List<Vector3>(new Vector3[shiftBy])) :
                                        frames[insertPos - 1];
                                var frameAfter = frames[insertPos];

                                var vn = new List<Vector3>();
                                var nn = new List<Vector3>();
                                var tn = new List<Vector3>();

                                var (weightBefore, vBefore, nBefore, tBefore) = frameBefore;
                                var (weightAfter, vAfter, nAfter, tAfter) = frameAfter;

                                var blend = (weight - weightBefore) / 100f * (weightAfter - weightBefore);
                                IEnumerable<Vector3> Interpolate(List<Vector3> before, List<Vector3> after)
                                {
                                    return Enumerable.Zip(before, after, (a, b) => (b - a) * blend);
                                }
                                vn.AddRange(Interpolate(vBefore, vAfter));
                                nn.AddRange(Interpolate(nBefore, nAfter));
                                tn.AddRange(Interpolate(tBefore, tAfter));

                                // Add the new mesh's frame to the end
                                vn.AddRange(v);
                                nn.AddRange(n);
                                tn.AddRange(t);

                                frames.Insert(insertPos, (weight, vn, nn, tn));
                            }
                        }
                    }
                }
                // Add empty frame data to all blendshapes that were not touched
                foreach (var i in Range(0, blendshapes.Count))
                {
                    if (processedBlendshapes.Contains(i))
                    {
                        continue;
                    }
                    var (_, frames) = blendshapes[i];
                    foreach (var (_, vn, nn, tn) in frames)
                    {
                        vn.AddRange(new Vector3[addMesh.vertexCount]);
                        nn.AddRange(new Vector3[addMesh.vertexCount]);
                        tn.AddRange(new Vector3[addMesh.vertexCount]);
                    }
                }
            }

            public void Combine()
            {
                var mesh = skin.sharedMesh;
                mesh.SetVertices(vertices);
                mesh.SetTangents(tangents);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetColors(colors);
                mesh.subMeshCount = indices.Count;
                foreach (var i in Enumerable.Range(0, indices.Count))
                {
                    mesh.SetTriangles(indices[i], i);
                }
                mesh.boneWeights = boneWeights.ToArray();
                skin.bones = bones.ToArray();
                skin.sharedMaterials = materials.ToArray();
                foreach (var (name, frames) in blendshapes)
                {
                    foreach (var (weight, v, n, t) in frames)
                    {
                        mesh.AddBlendShapeFrame(name, weight, v.ToArray(), n.ToArray(), t.ToArray());
                    }
                }
            }
        }

        public struct SkinnedMeshRendererGroup
        {
            public SkinnedMeshRendererGroup(SkinnedMeshRenderer skin)
            {
                rootBone = skin.rootBone;
                lightProbeUsage = skin.lightProbeUsage;
                reflectionProbeUsage = skin.reflectionProbeUsage;
                skinQuality = skin.quality;
                shadowCastingMode = skin.shadowCastingMode;
                receiveShadows = skin.receiveShadows;
                skinnedMotionVectors = skin.skinnedMotionVectors;
                allowOcclusionWhenDynamic = skin.allowOcclusionWhenDynamic;
            }
            Transform rootBone { get; set; }
            LightProbeUsage lightProbeUsage { get; set; }
            ReflectionProbeUsage reflectionProbeUsage { get; set; }
            SkinQuality skinQuality { get; set; }
            ShadowCastingMode shadowCastingMode { get; set; }
            bool receiveShadows { get; set; }
            bool skinnedMotionVectors { get; set; }
            bool allowOcclusionWhenDynamic { get; set; }
        }
    }
}
