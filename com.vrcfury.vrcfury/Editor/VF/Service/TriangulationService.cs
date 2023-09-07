using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    public class TriangulationService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly DirectTreeService directTree;

        public class Triangulator {
            public VFAFloat center;
            public VFAFloat up;
            public VFAFloat forward;
            public VFAFloat right;
        }

        public Triangulator CreateTriangulator(VFGameObject parent, string prefix, string paramName, string[] tags) {
            var fx = manager.GetFx();

            var tri = new Triangulator {
                center = fx.NewFloat($"{paramName}_center"),
                up = fx.NewFloat($"{paramName}_up"),
                forward = fx.NewFloat($"{paramName}_forward"),
                right = fx.NewFloat($"{paramName}_right")
            };
            HapticUtils.AddReceiver(parent, Vector3.zero, tri.center.Name(), $"{prefix}Center", 3f, tags);
            HapticUtils.AddReceiver(parent, Vector3.forward * 0.01f, tri.forward.Name(), $"{prefix}Forward", 3f, tags);
            HapticUtils.AddReceiver(parent, Vector3.up * 0.01f, tri.up.Name(), $"{prefix}Up", 3f, tags);
            HapticUtils.AddReceiver(parent, Vector3.right * 0.01f, tri.right.Name(), $"{prefix}Right", 3f, tags);

            return tri;
        }

        public void SendParamToShader(VFAFloat param, string shaderParam, Renderer renderer) {
            var fx = manager.GetFx();
            var maxClip = fx.NewClip($"{shaderParam}_max");
            var path = renderer.owner().GetPath(manager.AvatarObject);
            var binding = EditorCurveBinding.FloatCurve(path, renderer.GetType(), $"material.{shaderParam}");
            maxClip.SetConstant(binding, 1);
            directTree.Add(param, maxClip);
        }

        public void SendToShader(Triangulator tri, string shaderParamPrefix, Renderer renderer) {
            SendParamToShader(tri.center, $"{shaderParamPrefix}_Center", renderer);
            SendParamToShader(tri.up, $"{shaderParamPrefix}_Up", renderer);
            SendParamToShader(tri.right, $"{shaderParamPrefix}_Right", renderer);
            SendParamToShader(tri.forward, $"{shaderParamPrefix}_Forward", renderer);
        }
    }
}
