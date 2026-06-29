using System;
using UnityEngine;
using VF.Injector;
using VF.Utils;

namespace VF.Builder.Haptics {
    [VFService]
    internal class SpsMarkersService {
        private const float BoundsExtent = 5f;
        public const string SocketMarkerShaderName = "Hidden/VRCFury/SpsSocketMarker";
        public const string ResolverShaderName = "Hidden/VRCFury/SpsResolver";
        public const string DataGrabPassShaderName = "Hidden/VRCFury/SpsDataGrabPass";
        public const string Configured = "_SPS_Configured";
        public const string Id = "_SPS_Id";
        public const string PlayerId = "_SPS_PlayerId";
        public const string SocketHole = "_SPS_SocketHole";
        public const string SocketDoubleSided = "_SPS_SocketDoubleSided";
        public const string SocketPortal = "_SPS_SocketPortal";
        public const string SocketRadiusOffset = "_SPS_SocketRadiusOffset";
        public const string SocketNextId = "_SPS_SocketNextId";

        private readonly Lazy<Mesh> sharedTriggerMesh;
        private readonly Lazy<Material> sharedSocketMaterial;
        private readonly Lazy<Material> sharedResolverMaterial;
        private readonly Lazy<Material> sharedGrabPassMaterial;

        public SpsMarkersService() {
            sharedTriggerMesh = new Lazy<Mesh>(CreateTriggerMesh);
            sharedSocketMaterial = new Lazy<Material>(() => CreateSharedMaterial(SocketMarkerShaderName));
            sharedResolverMaterial = new Lazy<Material>(() => CreateSharedMaterial(ResolverShaderName));
            sharedGrabPassMaterial = new Lazy<Material>(() => CreateSharedMaterial(DataGrabPassShaderName));
        }

        public float NewMarkerId() {
            uint hash = (uint)Guid.NewGuid().GetHashCode();
            hash &= 0x00ffffffu;
            if (hash == 0) hash = 1;
            return hash;
        }

        private Material CreateSharedMaterial(string shaderName) {
            var shader = Shader.Find(shaderName);
            if (shader == null) {
                throw new Exception($"Failed to find SPS shader {shaderName}");
            }

            var material = VrcfObjectFactory.CreateMaterial(shader);
            material.enableInstancing = true;
            SpsConfigurer.MarkSpsPropertiesAnimated(material);
            return material;
        }

        public Material GetSharedSocketMaterial() {
            return sharedSocketMaterial.Value;
        }

        public Material GetSharedResolverMaterial() {
            return sharedResolverMaterial.Value;
        }

        public Material GetSharedGrabPassMaterial() {
            return sharedGrabPassMaterial.Value;
        }

        public Mesh GetTriggerMesh() {
            return sharedTriggerMesh.Value;
        }

        public void ConfigureSocketRenderer(MeshRenderer renderer) {
            ConfigureRenderer(renderer, GetSharedSocketMaterial());
        }

        public void ConfigureResolverRenderer(MeshRenderer renderer) {
            ConfigureRenderer(renderer, GetSharedResolverMaterial());
        }

        private void ConfigureRenderer(MeshRenderer renderer, Material markerMaterial) {
            if (renderer == null) return;

            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter != null) {
                meshFilter.sharedMesh = GetTriggerMesh();
            }
            renderer.sharedMaterials = new[] { markerMaterial, GetSharedGrabPassMaterial() };
        }

        private Mesh CreateTriggerMesh() {
            var mesh = VrcfObjectFactory.Register(new Mesh());
            mesh.name = "SpsTriggerMesh";
            mesh.vertices = new[] {
                BoundsExtent * new Vector3(-1, -1, -1),
                BoundsExtent * new Vector3(-1, -1, 1),
                BoundsExtent * new Vector3(1, 1, 1)
            };
            mesh.uv = new[] {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1)
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
