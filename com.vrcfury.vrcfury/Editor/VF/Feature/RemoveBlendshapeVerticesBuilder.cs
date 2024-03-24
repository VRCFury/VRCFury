using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using VF.Builder.Exceptions;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {

public class RemoveBlendshapeVerticesBuilder : FeatureBuilder<RemoveBlendshapeVertices> {
    [FeatureBuilderAction]
    public void Apply() {
        var renderers = new List<SkinnedMeshRenderer>();
        if (model.allRenderers) {
            renderers.AddRange(avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>());
        } else {
            renderers.Add(model.renderer);
        }

        foreach (var renderer in renderers) {
            var mesh = renderer.sharedMesh;
            if (mesh == null) continue;

            var blendshapeIndex = mesh.GetBlendShapeIndex(model.blendshape);
            if (blendshapeIndex < 0) continue;

            // collect vertices affected by given blendshape, make sure we use `Vector3.Equals` as `Vector3.operator==` uses a delta
            var vertexBuffer = new Vector3[mesh.vertexCount];
            mesh.GetBlendShapeFrameVertices(blendshapeIndex, 0, vertexBuffer, null, null);
            var toRemove = vertexBuffer.Select(v => !v.Equals(Vector3.zero)).ToArray();

            var newMesh = CloneMeshWithRemovedVertices(mesh, toRemove);
            renderer.sharedMesh = newMesh;
        }
    }

    // adapted from: https://github.com/euan142/LazyOptimiser/blob/main/Packages/net.euan.lazyoptimiser/Editor/MeshUtil.cs
    private static Mesh CloneMeshWithRemovedVertices(Mesh original, bool[] verticesToRemove) {
        var indexChangeMap = new Dictionary<int, int>();
        var newIndex = -1;
        for (int i = 0; i < original.vertexCount; i++) {
            if (verticesToRemove[i]) {
                indexChangeMap[i] = -1;
            } else {
                newIndex++;
                indexChangeMap[i] = newIndex;
            }
        }

        // make a new mesh and copy all data except the vertices we want to remove
        var mesh = new Mesh();

        mesh.SetVertices(original.vertices.Where((_, i) => verticesToRemove[i] == false).ToArray());
        if (original.HasVertexAttribute(VertexAttribute.Normal))
            mesh.SetNormals(original.normals.Where((_, i) => verticesToRemove[i] == false).ToArray());
        if (original.HasVertexAttribute(VertexAttribute.Tangent))
            mesh.SetTangents(original.tangents.Where((_, i) => verticesToRemove[i] == false).ToArray());
        if (original.HasVertexAttribute(VertexAttribute.Color))
            mesh.SetColors(original.colors.Where((_, i) => verticesToRemove[i] == false).ToArray());

        mesh.bindposes = original.bindposes;
        mesh.boneWeights = original.boneWeights.Where((_, i) => verticesToRemove[i] == false).ToArray();

        var uvList = new List<Vector4>(original.vertexCount);
        for (int i = 0; i < 8; i++) {
            if (mesh.HasVertexAttribute(VertexAttribute.TexCoord0 + i)) {
                original.GetUVs(i, uvList);
                mesh.SetUVs(i, uvList.Where((_, j) => verticesToRemove[j] == false).ToList());
            }
        }

        for (int subMeshNr = 0; subMeshNr < mesh.subMeshCount; subMeshNr++) {
            var topology = original.GetTopology(subMeshNr);
            if (topology != MeshTopology.Triangles) {
                throw new VRCFBuilderException("Only triangle meshes are supported for RemoveBlendshapeVertices. " +
                    $"Mesh '{original.name}', submesh {subMeshNr}, has topology {topology}.");
            }
            var subMesh = original.GetTriangles(subMeshNr);
            var newTris = new List<int>();
            for (int i = 0; i < subMesh.Length; i += 3) {
                var t1 = indexChangeMap[subMesh[i]];
                var t2 = indexChangeMap[subMesh[i+1]];
                var t3 = indexChangeMap[subMesh[i+2]];
                if (t1 != -1 && t2 != -1 && t3 != -1) {
                    newTris.Add(t1);
                    newTris.Add(t2);
                    newTris.Add(t3);
                }
            }
            mesh.SetTriangles(newTris.ToArray(), subMeshNr);
        }

        var deltaVertices = new Vector3[original.vertexCount];
        var deltaNormals = new Vector3[original.vertexCount];
        var deltaTangents = new Vector3[original.vertexCount];
        for (int i = 0; i < original.blendShapeCount; i++) {
            var name = original.GetBlendShapeName(i);
            var weightCount = original.GetBlendShapeFrameCount(i);
            for (int j = 0; j < weightCount; j++) {
                var weight = original.GetBlendShapeFrameWeight(i, j);
                original.GetBlendShapeFrameVertices(i, j, deltaVertices, deltaNormals, deltaTangents);
                var newDeltaVertices = deltaVertices.Where((_, k) => verticesToRemove[k] == false).ToArray();
                var newDeltaNormals = deltaNormals.Where((_, k) => verticesToRemove[k] == false).ToArray();
                var newDeltaTangents = deltaTangents.Where((_, k) => verticesToRemove[k] == false).ToArray();
                mesh.AddBlendShapeFrame(name, weight, newDeltaVertices, newDeltaNormals, newDeltaTangents);
            }
        }

        mesh.Optimize();
        return mesh;
    }

    private static int CountAffectedVertices([CanBeNull] SkinnedMeshRenderer smr, string blendshapeName) {
        if (smr == null) return 0;
        var mesh = smr.sharedMesh;
        if (mesh == null) return 0;
        var blendshapeIndex = mesh.GetBlendShapeIndex(blendshapeName);
        if (blendshapeIndex < 0) return 0;
        var vertexBuffer = new Vector3[mesh.vertexCount];
        mesh.GetBlendShapeFrameVertices(blendshapeIndex, 0, vertexBuffer, null, null);
        return vertexBuffer.Count(v => !v.Equals(Vector3.zero));
    }

    public override string GetEditorTitle() {
        return "Remove Blendshape Vertices";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();
        var allRenderersProp = prop.FindPropertyRelative("allRenderers");
        var rendererProp = prop.FindPropertyRelative("renderer");
        var blendshapeProp = prop.FindPropertyRelative("blendshape");

        content.Add(VRCFuryEditorUtils.Info("Removes all vertices affected by a given blendshape from the selected mesh. Reduces final polygon count non-destructively."));
        
        content.Add(VRCFuryActionDrawer.RendererSelector(allRenderersProp, rendererProp));
        content.Add(VRCFuryActionDrawer.BlendshapeSelector(avatarObject, blendshapeProp, allRenderersProp, rendererProp));

        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var all = allRenderersProp.boolValue;
                var blendshape = blendshapeProp.stringValue;
                var count = all ?
                    avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()
                        .Sum(smr => CountAffectedVertices(smr, blendshape)) :
                    CountAffectedVertices(rendererProp.objectReferenceValue as SkinnedMeshRenderer, blendshape);
                var vertexCountLabel = VRCFuryEditorUtils.Info($"Will remove {count} vertices");
                return vertexCountLabel;
            },
            allRenderersProp,
            rendererProp,
            blendshapeProp
        ));

        return content;
    }
}

}
