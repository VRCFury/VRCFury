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

namespace VF.Tests {
    [Category("VRCFury")]
    public class BlendTreeEditorTest {
        private readonly List<Object> owned = new();
        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

        [TearDown]
        public void TearDown() {
            foreach (var value in owned.AsEnumerable().Reverse()) if (value != null) Object.DestroyImmediate(value);
            owned.Clear();
        }

        private AnimatorController Controller() {
            var controller = Own(new AnimatorController());
            controller.AddParameter("Value", AnimatorControllerParameterType.Float);
            controller.AddParameter("Number", AnimatorControllerParameterType.Int);
            controller.AddParameter("Enabled", AnimatorControllerParameterType.Bool);
            controller.AddLayer("Base");
            var machine = Own(controller.layers[0].stateMachine);
            var state = Own(machine.AddState("Idle"));
            state.motion = Own(new AnimationClip());
            return controller;
        }

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
        public void ParameterRangeCacheRefreshesAfterEditingASharedDescendant() {
            var graphType = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Graphs.AnimationBlendTree.Graph");
            var graph = Own(ScriptableObject.CreateInstance(graphType));
            var child = Own(new BlendTree { blendType = BlendTreeType.Simple1D, blendParameter = "Value", useAutomaticThresholds = false });
            child.AddChild(Own(new AnimationClip()), 0);
            child.AddChild(Own(new AnimationClip()), 1);
            var root = Own(new BlendTree { blendType = BlendTreeType.Direct });
            root.AddChild(child); root.AddChild(child);
            var build = graphType.VFMethod("BuildFromBlendTree");
            int Index(string name = "Value") {
                var count = (int)typeof(BlendTree).GetProperty("recursiveBlendParameterCount",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(root);
                return Enumerable.Range(0, count).Single(i => (string)typeof(BlendTree).VFMethod("GetRecursiveBlendParameter")
                    .Invoke(root, new object[] { i }) == name);
            }
            float Bound(string method) => (float)typeof(CacheBlendTreeEditorParameterRangesHook).VFStaticMethod(method)
                .Invoke(null, new object[] { root, Index() });
            try {
                build.Invoke(graph, new object[] { root });
                var node = graphType.GetProperty("rootNode").GetValue(graph);
                // Creating the preview registers Unity's dependency tracking for the whole subtree.
                node.GetType().GetProperty("animator").GetValue(node);
                Assert.That(Bound("Min"), Is.EqualTo(0));
                Assert.That(Bound("Max"), Is.EqualTo(1));
                var children = child.children;
                children[0].threshold = -2;
                children[1].threshold = 4;
                child.children = children;
                Assert.That(node.GetType().GetProperty("controllerDirty").GetValue(node), Is.True,
                    "Editing a descendant must make Unity rebuild the graph on its next GUI event");
                build.Invoke(graph, new object[] { root });
                Assert.That(Bound("Min"), Is.EqualTo(-2));
                Assert.That(Bound("Max"), Is.EqualTo(4));
                child.blendParameter = "Renamed";
                build.Invoke(graph, new object[] { root });
                Assert.That(typeof(CacheBlendTreeEditorParameterRangesHook).VFStaticMethod("Name")
                    .Invoke(null, new object[] { root, Index("Renamed") }), Is.EqualTo("Renamed"));
            } finally { graphType.VFMethod("Clear").Invoke(graph, new object[] { true }); }
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
        public void ShortenedLabelsCacheByTextWidthAndStyleAndClearOnGraphRebuild() {
            var graphType = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Graphs.AnimationBlendTree.Graph");
            var guiType = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Graphs.AnimationBlendTree.GraphGUI");
            var graph = Own(ScriptableObject.CreateInstance(graphType));
            var gui = Own(ScriptableObject.CreateInstance(guiType));
            var method = guiType.VFMethod("LimitStringWidth");
            var prefix = typeof(CacheBlendTreeEditorParameterRangesHook).VFStaticMethod("LabelPrefix");
            var style = new GUIStyle(EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).label);
            var otherStyle = new GUIStyle(style) { fontSize = 24 };
            var root = Own(new BlendTree());
            root.AddChild(Own(new AnimationClip()));
            const string title = "A long blendtree node title that needs shortening to fit";
            void Rebuild() => graphType.VFMethod("BuildFromBlendTree").Invoke(graph, new object[] { root });
            try {
                Rebuild();
                foreach (var (text, width, currentStyle) in new[] {
                             (title, 80f, style), (title + " edited", 80f, style),
                             (title, 160f, style), (title, 80f, otherStyle) }) {
                    var lookup = new object[] { text, width, currentStyle, null };
                    Assert.That(prefix.Invoke(null, lookup), Is.True, "Changed text, width, or style must be measured");
                    var result = (string)method.Invoke(gui, new object[] { text, width, currentStyle });
                    Assert.That(prefix.Invoke(null, lookup), Is.False, "Repeated labels should use the cache");
                    Assert.That(lookup[3], Is.EqualTo(result));
                    Rebuild();
                    Assert.That(prefix.Invoke(null, lookup), Is.True, "Rebuilding must invalidate label measurements");
                    Assert.That(method.Invoke(gui, new object[] { text, width, currentStyle }), Is.EqualTo(result));
                }
            } finally { graphType.VFMethod("Clear").Invoke(graph, new object[] { true }); }
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
