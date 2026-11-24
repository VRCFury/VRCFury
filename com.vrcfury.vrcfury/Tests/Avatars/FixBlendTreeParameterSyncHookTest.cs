namespace VF.Tests {
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.Animations;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.Animations;
    using UnityEngine.Playables;
    using UnityEngine.SceneManagement;
    using VF.Utils;
    using VF.Hooks.UnityFixes;
    using Object = UnityEngine.Object;

    internal class FixBlendTreeParameterSyncHookTest : AnimatorEditorTestBase {
        [Test]
        public void ParameterPollingHandlesChangesExplicitEditsAndRebuiltNodes() {
            var graphType = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Graphs.AnimationBlendTree.Graph");
            var graph = Own(ScriptableObject.CreateInstance(graphType));
            var root = Own(new BlendTree { blendType = BlendTreeType.Direct });
            BlendTree Child() {
                var tree = Own(new BlendTree { blendType = BlendTreeType.Simple1D, blendParameter = "Value", useAutomaticThresholds = false });
                tree.AddChild(Own(new AnimationClip()), 0);
                tree.AddChild(Own(new AnimationClip()), 1);
                return tree;
            }
            var child = Child();
            root.AddChild(child);
            var populate = graphType.VFMethod("PopulateParameterValues");
            var build = graphType.VFMethod("BuildFromBlendTree");
            var set = graphType.VFMethod("SetParameterValue");
            var setInput = typeof(BlendTree).VFMethod("SetInputBlendValue");
            float Input(BlendTree tree) => (float)typeof(BlendTree).VFMethod("GetInputBlendValue").Invoke(tree, new object[] { "Value" });
            try {
                build.Invoke(graph, new object[] { root });
                set.Invoke(graph, new object[] { "Value", 0.75f });
                populate.Invoke(graph, null);
                Assert.That(Input(child), Is.EqualTo(0.75f));

                setInput.Invoke(root, new object[] { "Value", 0.25f });
                populate.Invoke(graph, null);
                Assert.That(Input(child), Is.EqualTo(0.25f), "Changed inputs must propagate");

                setInput.Invoke(child, new object[] { "Value", 1f });
                set.Invoke(graph, new object[] { "Value", 0.25f });
                Assert.That(Input(child), Is.EqualTo(0.25f), "Explicit edits must propagate even when the cached root value is unchanged");

                var added = Child();
                root.AddChild(added);
                build.Invoke(graph, new object[] { root });
                Assert.That(Input(added), Is.EqualTo(0.25f), "Rebuilding must initialize new nodes with existing inputs");
            } finally { graphType.VFMethod("Clear").Invoke(graph, new object[] { true }); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HiddenPreviewAnimatorsKeepTheirOwnParameters(bool previewScene) {
            var scene = previewScene ? EditorSceneManager.NewPreviewScene() : default;
            try {
                GameObject obj;
                if (previewScene) {
                    obj = Own(new GameObject("Preview"));
                    SceneManager.MoveGameObjectToScene(obj, scene);
                } else {
                    var template = Own(new GameObject("Preview template"));
                    obj = Own((GameObject)typeof(EditorUtility).VFStaticMethod("InstantiateForAnimatorPreview")
                        .Invoke(null, new object[] { template }));
                    Assert.That(obj.scene.IsValid(), Is.False);
                }
                var animator = obj.GetComponent<Animator>();
                if (animator == null) animator = obj.AddComponent<Animator>();
                animator.runtimeAnimatorController = Controller();
                animator.Update(0);
                animator.SetFloat("Value", 0.7f);
                animator.SetInteger("Number", 12);
                animator.SetBool("Enabled", true);
                Assert.That(animator.GetFloat("Value"), Is.EqualTo(0.7f));
                Assert.That(animator.GetInteger("Number"), Is.EqualTo(12));
                Assert.That(animator.GetBool("Enabled"), Is.True);
                Assert.That(animator.IsParameterControlledByCurve("Value"), Is.False);
            } finally {
                if (previewScene) EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void SceneAnimatorStillForwardsParametersToEveryMixerController() {
            var animator = Own(new GameObject("Avatar mixer")).AddComponent<Animator>();
            var graph = PlayableGraph.Create("Avatar mixer");
            try {
                var mixer = AnimationLayerMixerPlayable.Create(graph, 2);
                var controllers = new[] { AnimatorControllerPlayable.Create(graph, Controller()), AnimatorControllerPlayable.Create(graph, Controller()) };
                for (var i = 0; i < controllers.Length; i++) {
                    graph.Connect(controllers[i], 0, mixer, i);
                    mixer.SetInputWeight(i, 1);
                }
                AnimationPlayableOutput.Create(graph, "Avatar", animator).SetSourcePlayable(mixer);
                graph.Play();
                graph.Evaluate(0);
                animator.SetFloat("Value", 0.7f);
                animator.SetInteger("Number", 12);
                animator.SetBool("Enabled", true);
                foreach (var controller in controllers) {
                    Assert.That(controller.GetFloat("Value"), Is.EqualTo(0.7f));
                    Assert.That(controller.GetInteger("Number"), Is.EqualTo(12));
                    Assert.That(controller.GetBool("Enabled"), Is.True);
                }
            } finally { graph.Destroy(); }
        }

        [Test]
        public void SceneAnimatorParameterLookupTracksLiveValuesControllerReplacementAndRebinding() {
            var animator = Own(new GameObject("Scene parameter list")).AddComponent<Animator>();
            var controller = Controller();
            animator.runtimeAnimatorController = controller;
            animator.Update(0);
            animator.SetFloat("Value", 0.3f);
            Assert.That(animator.GetFloat("Value"), Is.EqualTo(0.3f));
            animator.SetFloat("Value", 0.7f);
            animator.SetInteger("Number", 12);
            animator.SetBool("Enabled", true);
            Assert.That(animator.GetFloat("Value"), Is.EqualTo(0.7f), "Only metadata may be cached, not values");
            Assert.That(animator.GetInteger("Number"), Is.EqualTo(12));
            Assert.That(animator.GetBool("Enabled"), Is.True);
            Assert.That(animator.IsParameterControlledByCurve("Value"), Is.False);
            Assert.That(animator.GetFloat("Missing"), Is.Zero, "Missing parameters must not produce native warnings");
            animator.SetFloat("Missing", 1);

            var replacement = Controller();
            var parameters = replacement.parameters;
            parameters[0].name = "Replacement";
            replacement.parameters = parameters;
            animator.runtimeAnimatorController = replacement;
            animator.Update(0);
            animator.SetFloat("Replacement", 0.8f);
            Assert.That(animator.GetFloat("Replacement"), Is.EqualTo(0.8f), "Same-size controller replacement must invalidate metadata");
            Assert.That(animator.GetFloat("Value"), Is.Zero);

            replacement.AddParameter("Added", AnimatorControllerParameterType.Float);
            animator.Rebind();
            animator.Update(0);
            animator.SetFloat("Added", 0.9f);
            Assert.That(animator.GetFloat("Added"), Is.EqualTo(0.9f), "Rebinding after parameter changes must refresh metadata");
        }

        [Test]
        public void SceneAnimatorParameterLookupPreservesAnimatedParameterStatus() {
            var animator = Own(new GameObject("Animated parameter list")).AddComponent<Animator>();
            var controller = Controller();
            var clip = (AnimationClip)controller.layers[0].stateMachine.states[0].state.motion;
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "Value"),
                AnimationCurve.Constant(0, 1, 0.25f));
            animator.runtimeAnimatorController = controller;
            animator.Update(0);
            Assert.That(animator.IsParameterControlledByCurve(Animator.StringToHash("Value")), Is.True);
            Assert.That(animator.IsParameterControlledByCurve("Value"), Is.True);
            Assert.That(animator.GetFloat("Value"), Is.EqualTo(0.25f));
        }
    }
}
