using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
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
            var original = renderer.sharedMesh;
            if (original == null)
                continue;
            var blendshapeIndex = original.GetBlendShapeIndex(model.blendshape);
            if (blendshapeIndex < 0)
                continue;

            var mesh = MutableManager.MakeMutable(original, forceCopy: true);
            var vertexBuffer = new Vector3[mesh.vertexCount];
            mesh.GetBlendShapeFrameVertices(blendshapeIndex, 0, vertexBuffer, null, null);
            var toRemove = vertexBuffer.Select(v => !v.Equals(Vector3.zero)).ToArray();
            try
            {
                RemoveVerticesFromMesh(toRemove, original, mesh);
                renderer.sharedMesh = mesh;
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    // adapted from: https://github.com/euan142/LazyOptimiser/blob/main/Packages/net.euan.lazyoptimiser/Editor/MeshUtil.cs
    private static void RemoveVerticesFromMesh(bool[] verticesToRemove, Mesh original, Mesh mesh) {
        var changeMap = new Dictionary<int, int>();
        var newIndex = -1;
        for (int i = 0; i < original.vertexCount; i++)
        {
            if (verticesToRemove[i])
            {
                changeMap[i] = -1;
            }
            else
            {
                newIndex++;
                changeMap[i] = newIndex;
            }
        }

        mesh.Clear();
        mesh.ClearBlendShapes();

        mesh.SetVertices(original.vertices.Where((_, i) => verticesToRemove[i] == false).ToArray());
        mesh.SetNormals(original.normals.Where((_, i) => verticesToRemove[i] == false).ToArray());
        mesh.SetTangents(original.tangents.Where((_, i) => verticesToRemove[i] == false).ToArray());

        mesh.SetColors(original.colors.Where((_, i) => verticesToRemove[i] == false).ToArray());
        mesh.boneWeights = original.boneWeights.Where((_, i) => verticesToRemove[i] == false).ToArray();

        for (int i = 0; i < 8; i++)
        {
            if (mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0 + i)) {
                var uvList = new List<Vector4>(original.vertexCount);
                original.GetUVs(i, uvList);
                mesh.SetUVs(i, uvList.Where((_, j) => verticesToRemove[j] == false).ToList());
            }
        }

        for (int subMeshNr = 0; subMeshNr < mesh.subMeshCount; subMeshNr++)
        {
            int[] subMesh = original.GetTriangles(subMeshNr);
            List<int> newTris = new List<int>();
            for (int i = 0; i < subMesh.Length; i += 3)
            {
                int t1 = changeMap[subMesh[i]];
                int t2 = changeMap[subMesh[i+1]];
                int t3 = changeMap[subMesh[i+2]];
                if (t1 != -1 && t2 != -1 && t3 != -1)
                {
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
        for (int i = 0; i < original.blendShapeCount; i++)
        {
            var name = original.GetBlendShapeName(i);
            var weightCount = original.GetBlendShapeFrameCount(i);
            for (int j = 0; j < weightCount; j++)
            {
                var weight = original.GetBlendShapeFrameWeight(i, j);
                original.GetBlendShapeFrameVertices(i, j, deltaVertices, deltaNormals, deltaTangents);
                var newDeltaVertices = deltaVertices.Where((_, k) => verticesToRemove[k] == false).ToArray();
                var newDeltaNormals = deltaNormals.Where((_, k) => verticesToRemove[k] == false).ToArray();
                var newDeltaTangents = deltaTangents.Where((_, k) => verticesToRemove[k] == false).ToArray();
                mesh.AddBlendShapeFrame(name, weight, newDeltaVertices, newDeltaNormals, newDeltaTangents);
            }
        }
    }

    private int CountAffectedVertices(SkinnedMeshRenderer smr, string blendshapeName) {
        if (smr == null)
            return 0;
        var mesh = smr.sharedMesh;
        if (mesh == null)
            return 0;
        var blendshapeIndex = mesh.GetBlendShapeIndex(blendshapeName);
        if (blendshapeIndex < 0)
            return 0;
        var vertexBuffer = new Vector3[mesh.vertexCount];
        mesh.GetBlendShapeFrameVertices(blendshapeIndex, 0, vertexBuffer, null, null);
        return vertexBuffer.Count(v => !v.Equals(Vector3.zero));
    }

    public override string GetEditorTitle() {
        return "Remove Blendshape Vertices";
    }
    
    public override bool AvailableOnRootOnly() {
        return false;
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
                var smrs = all ? avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>() : new[] {rendererProp.objectReferenceValue as SkinnedMeshRenderer};
                var count = smrs.Sum(smr => CountAffectedVertices(smr, blendshapeProp.stringValue));
                var vertexCountLabel = VRCFuryEditorUtils.Info($"Will remove {count} vertices");
                vertexCountLabel.visible = count > 0;
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
