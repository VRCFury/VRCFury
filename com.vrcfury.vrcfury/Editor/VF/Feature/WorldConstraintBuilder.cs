using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.Dynamics;
#if VRCSDK_HAS_VRCCONSTRAINTS
using VRC.SDK3.Dynamics.Constraint.Components;
#endif

namespace VF.Feature {
    [FeatureTitle("Droppable (World Constraint)")]
    internal class WorldConstraintBuilder : FeatureBuilder<WorldConstraint> {

        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly ClipFactoryService clipFactory;

        private VFABool toggle;
        
        [FeatureBuilderAction]
        public void ApplyToggle() {
            toggle = fx.NewBool($"{model.menuPath}", true);
            manager.GetMenu().NewMenuToggle(model.menuPath, toggle);
        }
        
        [FeatureBuilderAction(FeatureOrder.WorldConstraintBuilder)]
        public void ApplyMove() {
#if ! VRCSDK_HAS_VRCCONSTRAINTS
            if (!BuildTargetUtils.IsDesktop()) {
                return;
            }
#endif

            var dropClip = clipFactory.NewClip("Drop");
            directTree.Add(toggle.AsFloat(), dropClip);
            foreach (var constriant in featureBaseObject.GetConstraints()) {
                dropClip.SetEnabled(constriant.GetComponent(), false);
            }

            var parent = featureBaseObject.parent;

#if VRCSDK_HAS_VRCCONSTRAINTS
            var droppable = GameObjects.Create("Droppable", parent);
            mover.Move(featureBaseObject, droppable);
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

            mover.Move(featureBaseObject, resetObj);

            dropClip.SetEnabled(resetConstraint, false);
#endif
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("This component will allow you to 'drop' this object in the world, whenever you enable the toggle added in your menu."));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("menuPath"), "Menu Path", tooltip: ToggleBuilder.menuPathTooltip));
            return content;
        }
    }
}
