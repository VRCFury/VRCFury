using JetBrains.Annotations;
using UnityEngine;
using VF.Model;
using VF.Model.Feature;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace com.vrcfury.api.Components {
    /** Create an instance using <see cref="FuryComponents"/> */
    [PublicAPI]
    public class FuryFullController {
        private readonly FullController c;

        internal FuryFullController(GameObject obj) {
            var vf = obj.AddComponent<VRCFury>();
            c = new FullController();
            vf.content = c;
        }

        public void AddMenu(VRCExpressionsMenu menu, string prefix = "") {
            c.menus.Add(new FullController.MenuEntry() {
                menu = menu,
                prefix = prefix
            });
        }

        public void AddController(
            RuntimeAnimatorController controller,
            VRCAvatarDescriptor.AnimLayerType type = VRCAvatarDescriptor.AnimLayerType.FX
        ) {
            c.controllers.Add(new FullController.ControllerEntry() {
                controller = controller,
                type = type
            });
        }

        public void AddParams(VRCExpressionParameters prms) {
            c.prms.Add(new FullController.ParamsEntry() {
                parameters = prms
            });
        }

        public void AddGlobalParam(string name) {
            c.globalParams.Add(name);
        }

#if VRCSDK_HAS_FACE_TRACKING
        public void AddFaceTrackingRenderer(SkinnedMeshRenderer renderer) {
            c.faceTrackingMeshes.Add(renderer);
        }

        public void AddFaceTrackingBlendshapeSet(VRCAvatarDescriptor.FaceTrackingBlendshapesSet set) {
            c.faceTrackingBlendshapeSets.Add(set);
        }

        public void AddFaceTrackingBehavior(VRCAvatarDescriptor.FaceTrackingParameterBehavior behavior) {
            c.faceTrackingParameterBehaviors.Add(behavior);
        }
#endif

        public void AddInjectedParam(GameObject sourceObject, string sourceParam, string targetParam) {
            c.injectParams.Add(new FullController.InjectParamEntry {
                sourceObject = sourceObject,
                sourceParam = sourceParam,
                targetParam = targetParam
            });
        }

        public enum SpsDepthUnits {
            Meters,
            Local,
            PlugLengths
        }

        [System.Flags]
        public enum SpsSocketTypes {
            Hole = 1,
            Ring = 2,
            RingOneWay = 4,
            Any = ~0
        }

        public void AddInjectedSpsDepthParam(
            GameObject sourcePlug,
            string targetParam,
            SpsDepthUnits units = SpsDepthUnits.Local,
            SpsSocketTypes socketTypes = SpsSocketTypes.Any
        ) {
            var source = units switch {
                SpsDepthUnits.Meters => "__sps_depth_meters",
                SpsDepthUnits.Local => "__sps_depth_local",
                SpsDepthUnits.PlugLengths => "__sps_depth_plug_lengths",
                _ => throw new System.ArgumentOutOfRangeException(nameof(units))
            };
            if ((int)socketTypes != 0 && socketTypes != SpsSocketTypes.Any) {
                if (((int)socketTypes & ~7) != 0) throw new System.ArgumentOutOfRangeException(nameof(socketTypes));
                var modes = new System.Collections.Generic.List<string>();
                if ((socketTypes & SpsSocketTypes.Hole) != 0) modes.Add("hole");
                if ((socketTypes & SpsSocketTypes.Ring) != 0) modes.Add("ring");
                if ((socketTypes & SpsSocketTypes.RingOneWay) != 0) modes.Add("one-way-ring");
                source += "_socket_types_" + string.Join("+", modes);
            }
            AddInjectedParam(sourcePlug, source, targetParam);
        }

        public void AddPathRewrite(string from, string to) {
            c.rewriteBindings.Add(new FullController.BindingRewrite {
                from = from,
                to = to
            });
        }

        public void AppendLocomotion() {
            c.overwriteLocomotion = false;
        }
    }
}
