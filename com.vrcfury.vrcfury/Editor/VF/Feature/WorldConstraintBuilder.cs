using System.Collections.Generic;
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

namespace VF.Feature {
    public class WorldConstraintBuilder : FeatureBuilder<WorldConstraint> {

        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly ObjectMoveService mover;

        private VFABool toggle;
        
        [FeatureBuilderAction]
        public void ApplyToggle() {
            toggle = fx.NewBool($"{model.menuPath}", true);
            manager.GetMenu().NewMenuToggle(model.menuPath, toggle);
        }
        
        [FeatureBuilderAction(FeatureOrder.WorldConstraintBuilder)]
        public void ApplyMove() {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) {
                return;
            }

            var resetTarget = GameObjects.Create("Reset Target", featureBaseObject.parent, featureBaseObject.parent);

            var worldSpace = GameObjects.Create("Worldspace", resetTarget);
            var worldConstraint = worldSpace.AddComponent<ParentConstraint>();
            worldConstraint.AddSource(new ConstraintSource() {
                sourceTransform = VRCFuryEditorUtils.GetResource<Transform>("world.prefab"),
                weight = 1
            });
            worldConstraint.weight = 1;
            worldConstraint.constraintActive = true;
            worldConstraint.locked = true;

            var inner = GameObjects.Create("Droppable", worldSpace);
            var resetConstraint = inner.AddComponent<ParentConstraint>();
            resetConstraint.AddSource(new ConstraintSource() {
                sourceTransform = resetTarget,
                weight = 1
            });
            resetConstraint.weight = 1;
            resetConstraint.constraintActive = true;
            resetConstraint.locked = true;

            var dropClip = new AnimationClip();
            clipBuilder.Enable(dropClip, resetConstraint, false);
            foreach (var constriant in featureBaseObject.GetComponents<IConstraint>()) {
                clipBuilder.Enable(dropClip, constriant, false);
            }

            directTree.Add(toggle.AsFloat(), dropClip);

            mover.Move(featureBaseObject, inner);
        }

        public override string GetEditorTitle() {
            return "Droppable (World Constraint)";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("This component will allow you to 'drop' this object in the world, whenever you enable the toggle added in your menu."));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("menuPath"), "Menu Path", tooltip: ToggleBuilder.menuPathTooltip));
            return content;
        }
    }
}
