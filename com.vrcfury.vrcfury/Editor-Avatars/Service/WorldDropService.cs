using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.Dynamics;
#if VRCSDK_HAS_VRCCONSTRAINTS
using VRC.SDK3.Dynamics.Constraint.Components;
#endif

namespace VF.Service {
    [VFService]
    internal class WorldDropService {
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly DbtLayerService directTreeService;
        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly ClipFactoryService clipFactory;

        private readonly VFMultimapSet<VFGameObject, string> drops = new VFMultimapSet<VFGameObject, string>();

        public VFAFloat Add(VFGameObject obj, string name) {
            var param = fx.NewFloat(name + "_Drop");
            drops.Put(obj, param);
            return param;
        }

        [FeatureBuilderAction(FeatureOrder.WorldConstraintBuilder)]
        public void Apply() {
#if ! VRCSDK_HAS_VRCCONSTRAINTS
            if (!BuildTargetUtils.IsDesktop()) {
                return;
            }
#endif

            if (!drops.Any()) {
                return;
            }

            var directTree = directTreeService.Create();

            foreach (var obj in drops.GetKeys()) {
                var dropClip = clipFactory.NewClip("Drop");
                foreach (var p in drops.Get(obj)) {
                    directTree.Add(p, dropClip);
                }
                foreach (var constriant in obj.GetConstraints()) {
                    dropClip.SetEnabled(constriant.GetComponent(), false);
                }

                var parent = obj.parent;

#if VRCSDK_HAS_VRCCONSTRAINTS
                var droppable = GameObjects.Create("Droppable", parent);
                mover.Move(obj, droppable);
                var constraint = droppable.AddComponent<VRCParentConstraint>();
                constraint.IsActive = true;
                constraint.Locked = true;
                constraint.Sources.Add(new VRCConstraintSource(parent, 1, Vector3.zero, Vector3.zero));
                dropClip.SetCurve(constraint, "FreezeToWorld", 1);
#else
                var worldSpaceObj = GameObjects.Create("Droppable (WorldSpace)", parent);
                var worldConstraint = worldSpaceObj.AddComponent<ParentConstraint>();
                worldConstraint.constraintActive = true;
                worldConstraint.locked = true;
                worldConstraint.AddSource(new ConstraintSource() { sourceTransform = VRCFuryEditorUtils.GetResource<Transform>("world.prefab"), weight = 1 });

                var resetObj = GameObjects.Create("Droppable (Reset)", worldSpaceObj);
                var resetConstraint = resetObj.AddComponent<ParentConstraint>();
                resetConstraint.constraintActive = true;
                resetConstraint.locked = true;
                resetConstraint.AddSource(new ConstraintSource() { sourceTransform = parent, weight = 1 });

                mover.Move(obj, resetObj);

                dropClip.SetEnabled(resetConstraint, false);
#endif
            }
        }
    }
}
