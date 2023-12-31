using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Inspector;

namespace VF.Utils {
    public static class RendererExtensions {
        public static ShaderUtil.ShaderPropertyType? GetPropertyType(this Renderer renderer, string propertyName) {
            return renderer.sharedMaterials
                .NotNull()
                .Select(m => m.GetPropertyType(propertyName))
                .Where(type => type != null)
                .DefaultIfEmpty(null)
                .FirstOrDefault();
        }

        [CanBeNull]
        public static Mesh GetMesh(this Renderer renderer) {
            if (renderer is SkinnedMeshRenderer skin) {
                if (skin.sharedMesh == null) return null;
                return skin.sharedMesh;
            }

            if (renderer is MeshRenderer) {
                var owner = renderer.owner();
                var filter = owner.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) return null;
                return filter.sharedMesh;
            }

            return null;
        }
        
        [CanBeNull]
        public static Mesh GetMutableMesh(this Renderer renderer) {
            var mesh = renderer.GetMesh();
            if (mesh == null) return null;
            var copy = MutableManager.MakeMutable(mesh);
            renderer.SetMesh(copy);
            return copy;
        }

        public static void SetMesh(this Renderer renderer, Mesh mesh) {
            if (renderer is SkinnedMeshRenderer skin) {
                skin.sharedMesh = mesh;
                VRCFuryEditorUtils.MarkDirty(skin);
                return;
            }

            if (renderer is MeshRenderer) {
                var owner = renderer.owner();
                var filter = owner.GetComponent<MeshFilter>();
                if (filter == null)
                    throw new Exception(
                        "Cannot set mesh on MeshRenderer because it does not contain a MeshFilter: " +
                        owner.GetPath()
                    );
                filter.sharedMesh = mesh;
                VRCFuryEditorUtils.MarkDirty(filter);
            }

            throw new Exception("Cannot set mesh on renderer with unknown type: " + renderer.owner().GetPath());
        }
        
        public static bool HasBlendshape(this Renderer renderer, string name) {
            return renderer.GetBlendShapeIndex(name) >= 0;
        }
        
        public static int GetBlendShapeIndex(this Renderer renderer, string name) {
            var mesh = renderer.GetMesh();
            if (mesh == null) return -1;
            return mesh.GetBlendShapeIndex(name);
        }
        
        public static ISet<String> GetBlendshapeNames(this Renderer skin) {
            var mesh = skin.GetMesh();
            if (mesh == null) return ImmutableHashSet.Create<string>();
            return Enumerable.Range(0, mesh.blendShapeCount)
                .Select(i => mesh.GetBlendShapeName(i))
                .ToImmutableHashSet();
        }
        
        [CanBeNull]
        public static string GetBlendshapeName(this Renderer skin, int index) {
            var mesh = skin.GetMesh();
            if (mesh == null || index < 0 || index >= mesh.blendShapeCount) return null;
            return mesh.GetBlendShapeName(index);
        }

        public static int GetVertexCount(this Renderer renderer) {
            var mesh = renderer.GetMesh();
            if (mesh == null) return 0;
            return mesh.vertexCount;
        }
    }
}
