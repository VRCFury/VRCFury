using System;
using System.Collections.Generic;
using System.Linq;
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
using VF.Injector;
using VF.Service;
using VRC.SDKBase;
using static System.Linq.Enumerable;
using VRC.SDK3.Avatars.Components;

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
                "This feature will combine all similar meshes that are not animated to be toggled into one, " +
                " saving extra draw calls!"
            ));
            content.Add(new VisualElement { style = { paddingTop = 10 } });
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("keepFaceMeshSeparate"), "Keep Face Mesh separate"));
            content.Add(new VisualElement { style = { paddingTop = 10 } });
            return content;
        }

        public override bool AvailableOnProps()
        {
            return false;
        }

        [FeatureBuilderAction(FeatureOrder.BlendshapeOptimizer)]
        public void Apply()
        {
            var changes = CalculateMeshChanges();
            foreach (var group in changes.Groups)
            {
                var (basis, meshesToAdd) = group.Value;
                var basisObject = basis.owner();
                basis.sharedMesh = mutableManager.MakeMutable(basis.sharedMesh, basis.owner());
                VRCFuryEditorUtils.MarkDirty(basis);

                var combinable = new MeshCombiner(basis);
                foreach (var mesh in meshesToAdd)
                {
                    combinable.AddMesh(mesh);
                    VFGameObject obj = mesh.gameObject;
                    obj.Destroy();
                }
                combinable.Combine();
            }
            foreach (var skin in changes.Removed)
            {
                VFGameObject obj = skin.gameObject;
                obj.Destroy();
            }
        }

        private struct MeshChanges
        {
            public Dictionary<SkinnedMeshRendererGroup, (SkinnedMeshRenderer, List<SkinnedMeshRenderer>)> Groups { get; set; }
            public List<SkinnedMeshRenderer> Toggled { get; set; }
            public List<SkinnedMeshRenderer> Removed { get; set; }
            public List<SkinnedMeshRenderer> NotCombinable { get; set; }
        }

        private MeshChanges CalculateMeshChanges()
        {
            var task = new MeshChanges();
            task.Groups = new Dictionary<SkinnedMeshRendererGroup, (SkinnedMeshRenderer, List<SkinnedMeshRenderer>)>();
            task.Removed = new List<SkinnedMeshRenderer>();
            task.Toggled = new List<SkinnedMeshRenderer>();
            task.NotCombinable = new List<SkinnedMeshRenderer>();

            var descriptor = avatarObject.GetComponent<VRCAvatarDescriptor>();
            var faceMesh = descriptor.VisemeSkinnedMesh;

            var animatedBindings = manager.GetAllUsedControllersRaw()
                      .Select(tuple => tuple.Item2)
                      .SelectMany(controller => ((AnimatorController) controller).GetBindings(avatarObject))
                      .Concat(avatarObject.GetComponentsInSelfAndChildren<Animator>()
                          .SelectMany(animator => (animator.runtimeAnimatorController as AnimatorController).GetBindings(animator.gameObject)))
                      .ToLookup(x => x.Item1.path, x => x.Item1);

            bool IsObjectToggleBinding(EditorCurveBinding binding) => 
                    binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive";
            bool IsIncompatibleBinding(EditorCurveBinding binding) => 
                    IsObjectToggleBinding(binding) || 
                    binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName == "m_IsActive" ||
                    binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("material.") ||
                    binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape.") ||
                    binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("m_Materials.");
            
            bool IsObjectToggled(GameObject obj)
            {
                var path = clipBuilder.GetPath(obj.transform);
                foreach (var binding in animatedBindings[path])
                {
                    if (IsObjectToggleBinding(binding)) 
                    {
                        return true;
                    }
                }

                return false;
            }

            bool IsObjectToggledInHierarchy(GameObject obj) 
            {
                var current = obj;

                while (current != null && current != avatarObject) 
                {
                    if (IsObjectToggled(obj)) 
                    {
                        return true;
                    }
                    current = current.transform.parent?.gameObject;
                }

                return false;
            }

            bool IsObjectDisabledPermanently(GameObject obj)
            {
                var current = obj;
                while (current != null && current != avatarObject)
                {
                    if (current.activeSelf || IsObjectToggled(obj))
                    {
                        return false;
                    }
                    current = current.transform.parent?.gameObject;
                }
                return true;
            }

            bool ObjectHasIncompatibleBindings(GameObject obj) 
            {
                var path = clipBuilder.GetPath(obj.transform);
                foreach (var binding in animatedBindings[path])
                {
                    if (IsIncompatibleBinding(binding)) 
                    {
                        return true;
                    }
                }

                return false;
            }

            foreach (var (renderer, mesh, setMesh) in RendererIterator.GetRenderersWithMeshes(avatarObject))
            {
                if (!(renderer is SkinnedMeshRenderer skin)) continue;

                if (IsObjectToggledInHierarchy(skin.gameObject))
                {
                    task.Toggled.Add(skin);
                    continue;
                }
                if (IsObjectDisabledPermanently(skin.gameObject))
                {
                    task.Removed.Add(skin);
                    continue;
                }
                var isFaceMesh = skin == faceMesh;
                if (model.keepFaceMeshSeparate && isFaceMesh)
                {
                    task.NotCombinable.Add(skin);
                    continue;
                }

                var group = new SkinnedMeshRendererGroup(skin);
                if (!task.Groups.ContainsKey(group))
                {
                    task.Groups.Add(group, (skin, new List<SkinnedMeshRenderer>()));
                }
                else 
                {
                    //Only have to do this if we already initialized this group
                    var (preferred, entries) = task.Groups[group];

                    var isPreferredBase = Array.IndexOf(preferredBaseMeshNames, skin.name.ToLower()) != -1;
                    var hasFaceMesh = !isFaceMesh && entries.Count > 0 && entries[0] == faceMesh;
                    if (isFaceMesh || (isPreferredBase && !hasFaceMesh))
                    {
                        //Make this mesh the merge base
                        task.Groups[group] = (skin, entries);
                        entries.Add(preferred);
                    } else {
                        entries.Add(skin);
                    }
                }
            }

            foreach (var entry in task.Groups.ToList())
            {
                var (preferred, skins) = entry.Value;
                //Only check meshes for incompatbile bindings once we know the merge base
                //TODO: Allow bindings that are compatible with identical bindings on the merge base
                foreach (var skin in skins) 
                {
                    if (ObjectHasIncompatibleBindings(skin.gameObject))
                    {
                        skins.Remove(skin);
                        task.NotCombinable.Add(skin);
                    }
                }
                if (skins.Count < 2)
                {
                    task.Groups.Remove(entry.Key);
                    task.NotCombinable.AddRange(skins);
                    continue;
                }
            }
            return task;
        }

        private class MeshCombiner
        {
            SkinnedMeshRenderer skin;
            List<List<int>> indices;
            List<Material> materials;
            List<Vector3> vertices;
            List<Vector4> tangents;
            List<Vector3> normals;
            List<List<Vector2>> uvs;
            List<Color> colors;
            List<BoneWeight> boneWeights;
            List<Transform> bones;
            List<Matrix4x4> bindposes;
            Dictionary<string, int> blendshapeMapping;
            List<(string, List<(float, List<Vector3>, List<Vector3>, List<Vector3>)>)> blendshapes;

            public MeshCombiner(SkinnedMeshRenderer skin)
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
                uvs = new List<List<Vector2>>();
                foreach (var i in Range(0, 8))
                {
                    uvs.Add(new List<Vector2>());
                    mesh.GetUVs(i, uvs[i]);
                }
                colors = new List<Color>();
                mesh.GetColors(colors);
                boneWeights = new List<BoneWeight>();
                mesh.GetBoneWeights(boneWeights);
                bones = new List<Transform>(this.skin.bones);
                bindposes = new List<Matrix4x4>();
                mesh.GetBindposes(bindposes);

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
                foreach (var i in Range(0, 8))
                {
                    var addUvs = new List<Vector2>();
                    addMesh.GetUVs(i, addUvs);
                    // Add missing UVs for a channel that had no data yet
                    if (addUvs.Count > 0 && uvs[i].Count < shiftBy)
                    {
                        uvs[i].AddRange(new Vector2[shiftBy - uvs[i].Count]);
                    }
                    uvs[i].AddRange(addUvs);
                }
                colors.AddRange(addMesh.colors);

                // Merge bones
                var boneMapping = new Dictionary<int, int>();
                var addBones = add.bones;
                foreach (var i in Range(0, addBones.Length))
                {
                    var bone = addBones[i];
                    var j = bones.IndexOf(bone);
                    if (j == -1)
                    {
                        bones.Add(bone);
                        bindposes.Add(addMesh.bindposes[i]);
                        j = bones.Count - 1;
                    }
                    boneMapping.Add(i, j);
                }

                BoneWeight MapBoneWeight(BoneWeight weight)
                {
                    var newWeight = new BoneWeight();
                    (newWeight.boneIndex0, newWeight.weight0) = (boneMapping[weight.boneIndex0], weight.weight0);
                    (newWeight.boneIndex1, newWeight.weight1) = (boneMapping[weight.boneIndex1], weight.weight1);
                    (newWeight.boneIndex2, newWeight.weight2) = (boneMapping[weight.boneIndex2], weight.weight2);
                    (newWeight.boneIndex3, newWeight.weight3) = (boneMapping[weight.boneIndex3], weight.weight3);
                    return newWeight;
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
                foreach (var i in Range(0, 8))
                {
                    if (uvs[i].Count > 0 && uvs[i].Count < vertices.Count)
                    {
                        //Fill up to the correct amount of vertices
                        uvs[i].AddRange(new Vector2[vertices.Count - uvs[i].Count]);
                    }
                    mesh.SetUVs(i, uvs[i]);
                }
                mesh.SetColors(colors);
                mesh.subMeshCount = indices.Count;
                foreach (var i in Enumerable.Range(0, indices.Count))
                {
                    mesh.SetTriangles(indices[i], i);
                }
                skin.bones = bones.ToArray();
                mesh.boneWeights = boneWeights.ToArray();
                mesh.bindposes = bindposes.ToArray();
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
