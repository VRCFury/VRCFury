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
                if (!skinGroups.ContainsKey(group)) {
                    skinGroups.Add(group, new List<SkinnedMeshRenderer>());
                }
                var entries = skinGroups[group];

                entries.Add(skin);
            }

            foreach (var entry in skinGroups) {
                Debug.Log($"Found Group {entry.Key} ({entry.Value.Count} meshes)");
                if (entry.Value.Count < 2) {
                    continue;
                }

                CombineMeshes(entry.Value);
            }
        }

        private void CombineMeshes(List<SkinnedMeshRenderer> value)
        {
            var baseSkin = value[0];
            var mesh =  mutableManager.MakeMutable(baseSkin.sharedMesh, baseSkin.owner());

            var indices = new List<List<int>>();
            for (var i = 0; i < mesh.subMeshCount; i++) {
                var submeshIndices = new List<int>();
                mesh.GetIndices(submeshIndices, i);
                indices.Add(submeshIndices);
            }
            var materials = new List<Material>(baseSkin.sharedMaterials);
            var vertices = new List<Vector3>();
            mesh.GetVertices(vertices);
            var tangents = new List<Vector4>();
            mesh.GetTangents(tangents);
            var normals = new List<Vector3>();
            mesh.GetNormals(normals);
            // TODO there are 8 UV channels
            var uvs = new List<Vector2>();
            mesh.GetUVs(0, uvs);
            var colors = new List<Color>();
            mesh.GetColors(colors);
            var boneWeights = new List<BoneWeight>();
            mesh.GetBoneWeights(boneWeights);
            var bones = new List<Transform>(baseSkin.bones);

            
            baseSkin.sharedMesh = mesh;
            VRCFuryEditorUtils.MarkDirty(baseSkin);

            foreach (var skin in value.GetRange(1, value.Count - 1)) {
                var nextMesh = skin.sharedMesh;
          
                var materialMapping = new Dictionary<int, int>();
                for (var i = 0; i < skin.sharedMaterials.Length; i++) {
                    var material = skin.sharedMaterials[i];
                    var j = materials.IndexOf(material);
                    if (j == -1) {
                        materials.Add(material);
                        j = materials.Count - 1;
                    }
                    materialMapping.Add(i, j);
                }

                var shiftBy = vertices.Count;
                foreach (var i in Range(0, nextMesh.subMeshCount)) {
                    var nextIndices = nextMesh.GetIndices(i).Select(idx => idx + shiftBy).ToArray();
                    if (materialMapping.TryGetValue(i, out var j)) {
                        if (indices.Count <= j) {
                            foreach (var _ in Range(0, indices.Count - j + 1)) {
                                indices.Add(new List<int>());
                            }
                        }
                        indices[j].AddRange(nextIndices);
                    }
                }

                // TODO maybe use 
                // var skinToBaseSkin = skin.transform.localToWorldMatrix * baseSkin.transform.worldToLocalMatrix;
                vertices.AddRange(nextMesh.vertices.Select(v => 
                    baseSkin.transform.InverseTransformPoint(skin.transform.TransformPoint(v))));    
                tangents.AddRange(nextMesh.tangents);
                normals.AddRange(nextMesh.normals);            
                uvs.AddRange(nextMesh.uv);
                colors.AddRange(nextMesh.colors);

                var boneMapping = new Dictionary<int, int>();
                var nextBones = skin.bones;
                foreach (var i in Range(0, nextBones.Length)) {
                    var bone = nextBones[i];
                    var j = bones.IndexOf(bone);
                    if (j == -1) {
                        bones.Add(bone);
                        j = bones.Count - 1;
                    }
                    boneMapping.Add(i, j);
                }

                BoneWeight MapBoneWeight(BoneWeight weight) {
                    (int, float) Choose(int i, float w) => i >= boneMapping.Count ? (0, 0.0f) : (boneMapping[i], w);
                    (weight.boneIndex0, weight.weight0) = Choose(weight.boneIndex0, weight.weight0);
                    (weight.boneIndex1, weight.weight1) = Choose(weight.boneIndex1, weight.weight1);
                    (weight.boneIndex2, weight.weight2) = Choose(weight.boneIndex2, weight.weight2);
                    (weight.boneIndex3, weight.weight3) = Choose(weight.boneIndex3, weight.weight3);
                    return weight;
                }
                BoneWeight[] MapBoneWeights(BoneWeight[] weights) {
                    return weights.Select(w => MapBoneWeight(w)).ToArray();
                }

                boneWeights.AddRange(MapBoneWeights(nextMesh.boneWeights));


                VFGameObject obj = skin.gameObject;
                obj.Destroy();
            }

            mesh.SetVertices(vertices);
            mesh.SetTangents(tangents);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.subMeshCount = indices.Count;
            foreach (var i in Enumerable.Range(0, indices.Count)) {
                mesh.SetTriangles(indices[i], i);
            }
            mesh.boneWeights = boneWeights.ToArray();
            baseSkin.bones = bones.ToArray();
            baseSkin.sharedMaterials = materials.ToArray();
        }
        

        public struct SkinnedMeshRendererGroup
        {
            public SkinnedMeshRendererGroup(SkinnedMeshRenderer skin) {
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
            ShadowCastingMode shadowCastingMode{ get; set; }
            bool receiveShadows {get; set;}
            bool skinnedMotionVectors {get; set;}
            bool allowOcclusionWhenDynamic {get; set;}

            public override string ToString() {
                return $"(rootBone={rootBone}, lightProbeUsage={lightProbeUsage}, skinQuality={skinQuality}, " +
                    $"shadowCastingMode={shadowCastingMode}, receiveShadows={receiveShadows}, " +
                    $"skinedMotionVectors={skinnedMotionVectors}, allowOcclusionWhenDynamic={allowOcclusionWhenDynamic})";
            }
        }
    }
}
