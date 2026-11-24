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

    internal class CacheBlendTreeEditorParameterRangesHookTest : AnimatorEditorTestBase {
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
    }
}
