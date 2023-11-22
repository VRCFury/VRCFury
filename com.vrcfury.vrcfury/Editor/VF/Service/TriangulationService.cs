using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    public class TriangulationService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly HapticContactsService hapticContacts;

        public class Triangulator {
            public VFAFloat center;
            public VFAFloat up;
            public VFAFloat forward;
            public VFAFloat right;
        }

        public Triangulator CreateTriangulator(VFGameObject parent, string prefix, string paramName, string[] tags, HapticUtils.ReceiverParty party, bool useHipAvoidance) {
            var tri = new Triangulator {
                center = hapticContacts.AddReceiver(parent, Vector3.zero, $"{paramName}_center", $"{prefix}Center", 3f, tags, party, useHipAvoidance: useHipAvoidance),
                up = hapticContacts.AddReceiver(parent, Vector3.up * 0.1f, $"{paramName}_up", $"{prefix}Up", 3f, tags, party, useHipAvoidance: useHipAvoidance),
                forward = hapticContacts.AddReceiver(parent, Vector3.forward * 0.1f, $"{paramName}_forward", $"{prefix}Forward", 3f, tags, party, useHipAvoidance: useHipAvoidance),
                right = hapticContacts.AddReceiver(parent, Vector3.right * 0.1f, $"{paramName}_right", $"{prefix}Right", 3f, tags, party, useHipAvoidance: useHipAvoidance)
            };

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
