using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VF.Utils.Controller;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class ControllerParametersTest {
        [Test]
        public void DuplicateParametersUseUnityFirstEntrySemantics(
            [Values(AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Int,
                AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Trigger)] AnimatorControllerParameterType firstType,
            [Values(AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Int,
                AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Trigger)] AnimatorControllerParameterType secondType
        ) {
            var source = new AnimatorController();
            source.AddLayer("Base");
            var stateMachine = source.layers[0].stateMachine;
            var state = stateMachine.AddState("Idle");
            var target = stateMachine.AddState("Target");
            var transition = state.AddTransition(target);
            transition.hasExitTime = false;
            transition.duration = 0;
            var mode = firstType == AnimatorControllerParameterType.Float || firstType == AnimatorControllerParameterType.Int
                ? AnimatorConditionMode.Greater : AnimatorConditionMode.If;
            transition.AddCondition(mode, firstType == AnimatorControllerParameterType.Float ? 0.4f : 3, "Duplicate");
            var obj = new GameObject("DuplicateParameters");
            try {
                source.parameters = new[] {
                    new AnimatorControllerParameter {
                        name = "Duplicate", type = firstType,
                        defaultFloat = 0.25f, defaultInt = 2, defaultBool = false
                    },
                    new AnimatorControllerParameter { name = "Other", type = AnimatorControllerParameterType.Float },
                    new AnimatorControllerParameter {
                        name = "Duplicate", type = secondType,
                        defaultFloat = 0.75f, defaultInt = 7, defaultBool = true
                    }
                };
                var animator = obj.AddComponent<Animator>();
                animator.runtimeAnimatorController = source;
                animator.Rebind();
                animator.Update(0);
                Assert.That(animator.parameterCount, Is.EqualTo(3), "Unity retains duplicate metadata");
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
                switch (firstType) {
                    case AnimatorControllerParameterType.Float:
                        Assert.That(animator.GetFloat("Duplicate"), Is.EqualTo(0.25f));
                        animator.SetFloat("Duplicate", 0.5f);
                        Assert.That(animator.GetFloat("Duplicate"), Is.EqualTo(0.5f));
                        break;
                    case AnimatorControllerParameterType.Int:
                        Assert.That(animator.GetInteger("Duplicate"), Is.EqualTo(2));
                        animator.SetInteger("Duplicate", 4);
                        Assert.That(animator.GetInteger("Duplicate"), Is.EqualTo(4));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        Assert.That(animator.GetBool("Duplicate"), Is.False);
                        animator.SetBool("Duplicate", true);
                        Assert.That(animator.GetBool("Duplicate"), Is.True);
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        Assert.That(animator.GetBool("Duplicate"), Is.False);
                        animator.SetTrigger("Duplicate");
                        Assert.That(animator.GetBool("Duplicate"), Is.True);
                        break;
                }
                animator.Update(0.1f);
                animator.Update(0.1f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Target"), Is.True,
                    "Transition evaluation must use the first entry too");

                var controller = VFController.Load(source, new VFLoadContext { OwnerObject = obj, AnimatorObject = obj });
                Assert.That(controller.parameters.Select(p => p.name), Is.EqualTo(new[] { "Duplicate", "Other" }));
                var parameter = controller.GetParam("Duplicate");
                Assert.That(parameter.type, Is.EqualTo(firstType));
                Assert.That(parameter.defaultFloat, Is.EqualTo(0.25f));
                Assert.That(parameter.defaultInt, Is.EqualTo(2));
                Assert.That(parameter.defaultBool, Is.False);
                var condition = controller.layers.SelectMany(layer => layer.allTransitions).Single().conditions.Single();
                Assert.That(condition.parameter, Is.EqualTo("Duplicate"));
                Assert.That(condition.mode, Is.EqualTo(mode));
                controller.UpgradeWrongParamTypes();
                Assert.That(controller.GetParam("Duplicate").type, Is.EqualTo(firstType),
                    "A later duplicate must not upgrade the winning parameter's type");
                Assert.That(source.parameters, Has.Length.EqualTo(3), "Do not modify the source controller");
            } finally {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(transition);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(state);
                Object.DestroyImmediate(stateMachine);
                Object.DestroyImmediate(source);
            }
        }
    }
}
