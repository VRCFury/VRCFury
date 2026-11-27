using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VF.Utils;

namespace VF.Tests {
    internal class VrcfAnimationDebugInfoTest {
        [Test]
        public void WriteDefaultsInspectionHandlesSharedMachinesAndSyncedOverrides() {
            var controller = new AnimatorController();
            var machine = new AnimatorStateMachine();
            var state = new AnimatorState { name = "Pose", writeDefaultValues = false };
            var clip = new AnimationClip();
            var tree = new BlendTree { blendType = BlendTreeType.Simple1D };
            var direct = new BlendTree { blendType = BlendTreeType.Direct };
            try {
                direct.AddChild(clip);
                tree.AddChild(direct);
                state.motion = tree;
                machine.states = new[] { new ChildAnimatorState { state = state } };

                var synced = new AnimatorControllerLayer {
                    name = "Synced",
                    syncedLayerIndex = 0,
                    blendingMode = AnimatorLayerBlendingMode.Additive
                };
                synced.SetOverrideMotion(state, clip);
                controller.layers = new[] {
                    new AnimatorControllerLayer {
                        name = "Base",
                        syncedLayerIndex = -1,
                        stateMachine = machine
                    },
                    new AnimatorControllerLayer {
                        name = "Duplicate",
                        syncedLayerIndex = -1,
                        stateMachine = machine
                    },
                    synced
                };
                var result = VrcfAnimationDebugInfo.GetWriteDefaultsStates(controller);
                Assert.That(result.Select(info => info.name), Is.EqualTo(new[] { "Base | Pose", "Synced | Pose" }));
                Assert.That(result.Select(info => info.isDirect), Is.EqualTo(new[] { true, false }));
                Assert.That(result.Select(info => info.isAdditive), Is.EqualTo(new[] { false, true }));
                Assert.That(result.Select(info => info.writeDefaults), Is.All.False);

                state.writeDefaultValues = true;
                result = VrcfAnimationDebugInfo.GetWriteDefaultsStates(controller);
                Assert.That(result.Select(info => info.writeDefaults), Is.All.True);
            } finally {
                Object.DestroyImmediate(controller);
                Object.DestroyImmediate(machine);
                Object.DestroyImmediate(state);
                Object.DestroyImmediate(tree);
                Object.DestroyImmediate(direct);
                Object.DestroyImmediate(clip);
            }
        }
    }
}
