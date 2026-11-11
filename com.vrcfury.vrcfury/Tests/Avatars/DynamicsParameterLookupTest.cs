using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.Dynamics;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal class DynamicsParameterLookupTest {
        private static object Read(AnimParameterAccessAvatarSDK access, string field) =>
            typeof(AnimParameterAccessAvatarSDK).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(access);

        [TestCase(AnimatorControllerParameterType.Float)]
        [TestCase(AnimatorControllerParameterType.Int)]
        [TestCase(AnimatorControllerParameterType.Bool)]
        [TestCase(AnimatorControllerParameterType.Trigger)]
        public void DynamicsLookupPreservesMatchingAndRefreshesForControllerChanges(AnimatorControllerParameterType type) {
            var obj = new GameObject("Avatar");
            var controller = new AnimatorController();
            var other = new AnimatorController();
            controller.AddLayer("Base");
            other.AddLayer("Base");
            var machine = controller.layers[0].stateMachine;
            var otherMachine = other.layers[0].stateMachine;
            var state = machine.AddState("Idle");
            var otherState = otherMachine.AddState("Idle");
            machine.defaultState = state;
            otherMachine.defaultState = otherState;
            try {
                controller.parameters = Enumerable.Range(0, 128)
                    .Select(i => new AnimatorControllerParameter { name = "Parameter" + i, type = type })
                    .ToArray();
                other.parameters = new[] { new AnimatorControllerParameter { name = "Replacement", type = AnimatorControllerParameterType.Float } };
                var animator = obj.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.Rebind();
                animator.Update(0);
                Assert.That(animator.parameters.Length, Is.EqualTo(128));
                foreach (var index in new[] { 0, 64, 127 }) {
                    var name = "Parameter" + index;
                    var access = new AnimParameterAccessAvatarSDK(animator, name);
                    Assert.That(Read(access, "valid"), Is.EqualTo(type != AnimatorControllerParameterType.Trigger));
                    if (type != AnimatorControllerParameterType.Trigger) {
                        Assert.That(Read(access, "paramHash"), Is.EqualTo(Animator.StringToHash(name)));
                        Assert.That(Read(access, "paramType"), Is.EqualTo(type));
                    }
                }
                Assert.That(Read(new AnimParameterAccessAvatarSDK(animator, "Missing"), "valid"), Is.False);
                animator.runtimeAnimatorController = other;
                animator.Rebind();
                animator.Update(0);
                Assert.That(Read(new AnimParameterAccessAvatarSDK(animator, "Parameter0"), "valid"), Is.False);
                Assert.That(Read(new AnimParameterAccessAvatarSDK(animator, "Replacement"), "valid"), Is.True);
            } finally {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(controller);
                Object.DestroyImmediate(other);
                Object.DestroyImmediate(machine);
                Object.DestroyImmediate(otherMachine);
                Object.DestroyImmediate(state);
                Object.DestroyImmediate(otherState);
            }
        }

        [Test]
        public void NullAnimatorAndMissingControllerRemainInvalid() {
            Assert.That(Read(new AnimParameterAccessAvatarSDK(null, "Parameter"), "valid"), Is.False);
            var obj = new GameObject("Avatar");
            try {
                var animator = obj.AddComponent<Animator>();
                Assert.That(Read(new AnimParameterAccessAvatarSDK(animator, "Parameter"), "valid"), Is.False);
            } finally {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
