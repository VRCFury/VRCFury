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
        public const string IdLow = "_SPS_IdLow";
        public const string IdHigh = "_SPS_IdHigh";
        public const string PlayerIdLow = "_SPS_PlayerIdLow";
        public const string PlayerIdHigh = "_SPS_PlayerIdHigh";
        public const string SocketHole = "_SPS_SocketHole";
        public const string SocketDoubleSided = "_SPS_SocketDoubleSided";
        public const string SocketPortal = "_SPS_SocketPortal";
        public const string SocketRadiusOffset = "_SPS_SocketRadiusOffset";
        public const string GuidedTargetIdLow = "_SPS_GuidedTargetIdLow";
        public const string GuidedTargetIdHigh = "_SPS_GuidedTargetIdHigh";
        public const string SocketUseTangentIn = "_SPS_SocketUseTangentIn";
        public const string SocketUseTangentOut = "_SPS_SocketUseTangentOut";
        public const string SocketTangentIn = "_SPS_SocketTangentIn";
        public const string SocketTangentOut = "_SPS_SocketTangentOut";
        private readonly Lazy<Mesh> sharedTriggerMesh;
        private readonly Lazy<Material> sharedSocketMaterial;
        private readonly Lazy<Material> sharedResolverMaterial;
        private readonly Lazy<Material> sharedGrabPassMaterial;
        private BinaryContainer detachedOtherAssetsParent;

        public SpsMarkersService() {
            sharedTriggerMesh = new Lazy<Mesh>(CreateTriggerMesh);
            sharedSocketMaterial = new Lazy<Material>(() => CreateSharedMaterial(SocketMarkerShaderName));
            sharedResolverMaterial = new Lazy<Material>(() => CreateSharedMaterial(ResolverShaderName));
            sharedGrabPassMaterial = new Lazy<Material>(() => CreateSharedMaterial(DataGrabPassShaderName));
        }

        public uint NewMarkerId() {
            uint hash = (uint)Guid.NewGuid().GetHashCode();
            if (hash == 0) hash = 1;
            return hash;
        }

        public static uint GetLow(uint value) {
            return value & 0xffffu;
        }

        public static uint GetHigh(uint value) {
            return value >> 16;
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
                new Vector3(-0.005f, -0.005f, 0),
                new Vector3(-0.005f, 0.005f, 0),
                new Vector3(0.005f, 0.005f, 0)
            };
            mesh.uv = new[] {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1)
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * BoundsExtent * 2);
            return mesh;
        }
    }
}
