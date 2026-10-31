using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder.Haptics;
using VF.Inspector;
using Object = UnityEngine.Object;

namespace VF.Tests {
    // Like Thry's drawers, this drawer requires its ShaderGUI to be active.
    public class SpsInspectorContextDrawer : MaterialPropertyDrawer {
        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor) {
            if (!SpsTestShaderGui.active) throw new InvalidOperationException("Original ShaderGUI is not active");
            return EditorGUIUtility.singleLineHeight;
        }
        public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor) {
            prop.floatValue = EditorGUI.FloatField(position, label, prop.floatValue);
        }
    }

    public class SpsTestShaderGui : ShaderGUI {
        internal static bool active;
        internal static int draws;
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties) {
            active = true;
            try { draws++; materialEditor.PropertiesDefaultGUI(properties); }
            finally { active = false; }
        }
    }

    [Category("VRCFury")]
    public class SpsMaterialEditorTest {
        [TestCase(false)]
        [TestCase(true)]
        public void KeepsCustomInspectorAndHidesSpsPropertiesFromItsNormalPropertyList(bool keepImports) {
            var path = "Assets/" + Guid.NewGuid() + ".shader";
            var patchedPath = "";
            Material material = null;
            Material unpatched = null;
            Editor editor = null;
            Editor mixedEditor = null;
            Editor rendererEditor = null;
            GameObject rendererObject = null;
            var expanded = SessionState.GetBool("VF.SPS.MaterialProperties", false);
            try {
                File.WriteAllText(path, @"
Shader ""Hidden/VRCFury/Tests/SpsInspector"" {
    Properties {
        [SpsInspectorContext] _Original (""Original"", Float) = 0
    }
    SubShader {
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 vertex : POSITION) : SV_POSITION { return vertex; }
            float4 frag() : SV_Target { return 0; }
            ENDCG
        }
    }
    CustomEditor ""VF.Tests.SpsTestShaderGui""
}");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                material = new Material(shader);
                unpatched = new Material(shader);
#if !VRCSDK_HAS_MATERIAL_CONTACTS
                Assert.That(Assert.Throws<Exception>(() => SpsPatcher.Patch(material, keepImports, false)).Message,
                    Does.Contain("Update the VRCSDK"));
#else
                SpsPatcher.Patch(material, keepImports, false);
                patchedPath = AssetDatabase.GetAssetPath(material.shader);
                Assert.That(File.ReadAllText(patchedPath), Does.Contain("CustomEditor \"VF.Tests.SpsTestShaderGui\""));
                UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(material, true);
                editor = Editor.CreateEditor(material);
                typeof(Editor).GetProperty("firstInspectedEditor", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(editor, true);
                Assert.That(((MaterialEditor)editor).customShaderGUI, Is.TypeOf<SpsTestShaderGui>());
                Assert.That(SpsMaterialEditor.ShouldDraw(editor), Is.True);
                var properties = MaterialEditor.GetMaterialProperties(editor.targets);
                Assert.That(properties.Count(p => p.name.StartsWith("_SPS_")), Is.GreaterThan(0));
                Assert.That(properties.Where(p => p.name.StartsWith("_SPS_"))
                    .All(p => (p.flags & MaterialProperty.PropFlags.HideInInspector) != 0), Is.True);
                Assert.That(properties.Single(p => p.name == "_Original").flags & MaterialProperty.PropFlags.HideInInspector,
                    Is.EqualTo((MaterialProperty.PropFlags)0));
                SessionState.SetBool("VF.SPS.MaterialProperties", false);
                var collapsedHeight = DrawGui(editor);
                SessionState.SetBool("VF.SPS.MaterialProperties", true);
                var draws = SpsTestShaderGui.draws;
                var expandedHeight = DrawGui(editor);
                Assert.That(expandedHeight, Is.GreaterThan(collapsedHeight + EditorGUIUtility.singleLineHeight),
                    "Expanding SPS must actually draw the hidden properties.");
                Assert.That(SpsTestShaderGui.draws, Is.GreaterThan(draws));

                rendererObject = new GameObject("SPS material inspector test");
                var renderer = rendererObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(material, true);
                rendererEditor = Editor.CreateEditorWithContext(new Object[] { material }, renderer, typeof(MaterialEditor));
                typeof(Editor).GetProperty("firstInspectedEditor", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(rendererEditor, false);
                var materialHeaders = 0;
                void OnHeader(Editor drawing) {
                    if (drawing is MaterialEditor && drawing.targets.Contains(material)) materialHeaders++;
                }
                Editor.finishedDefaultHeaderGUI += OnHeader;
                try {
                    draws = SpsTestShaderGui.draws;
                    DrawGui(rendererEditor);
                    Assert.That(materialHeaders, Is.GreaterThan(0), "The embedded material must run the header hook.");
                    Assert.That(SpsTestShaderGui.draws, Is.GreaterThan(draws), "The embedded original inspector must still run.");
                } finally { Editor.finishedDefaultHeaderGUI -= OnHeader; }
                mixedEditor = Editor.CreateEditor(new Object[] { material, unpatched });
                Assert.That(SpsMaterialEditor.ShouldDraw(mixedEditor), Is.False);
#endif
            } finally {
                SessionState.SetBool("VF.SPS.MaterialProperties", expanded);
                if (rendererEditor != null) Object.DestroyImmediate(rendererEditor);
                if (rendererObject != null) Object.DestroyImmediate(rendererObject);
                if (editor != null) Object.DestroyImmediate(editor);
                if (mixedEditor != null) Object.DestroyImmediate(mixedEditor);
                if (material != null) Object.DestroyImmediate(material);
                if (unpatched != null) Object.DestroyImmediate(unpatched);
                AssetDatabase.DeleteAsset(path);
                if (patchedPath != "") AssetDatabase.DeleteAsset(patchedPath);
            }
        }

        // A detached IMGUI container exercises a real Layout pass without opening a window.
        private static float DrawGui(Editor editor) {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var panelType = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.Panel");
            var panel = (IPanel)panelType.GetMethod("CreateEditorPanel", flags).Invoke(null, new object[] { editor });
            var utility = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.UIElementsUtility");
            var container = new IMGUIContainer();
            panel.visualTree.Add(container);
            var cacheType = typeof(GUILayoutUtility).GetNestedType("LayoutCache", BindingFlags.Public | BindingFlags.NonPublic);
            var cache = Activator.CreateInstance(cacheType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { -1 }, null);
            try {
                var evt = new Event { type = EventType.Layout };
                utility.GetMethod("BeginContainerGUI", flags).Invoke(null, new object[] { cache, evt, container });
                try { editor.DrawHeader(); editor.OnInspectorGUI(); }
                finally {
                    utility.GetMethod("EndContainerGUI", flags).Invoke(null, new object[] { evt, new Rect(0, 0, 800, 2000) });
                }
                var top = cacheType.GetField("topLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(cache);
                top.GetType().GetMethod("CalcWidth").Invoke(top, null);
                top.GetType().GetMethod("SetHorizontal").Invoke(top, new object[] { 0f, 800f });
                top.GetType().GetMethod("CalcHeight").Invoke(top, null);
                top.GetType().GetMethod("SetVertical").Invoke(top, new object[] { 0f, 2000f });
                var marginGroups = LayoutEntries(top).Where(entry =>
                    ((GUIStyle)entry.GetType().GetProperty("style").GetValue(entry)).name == EditorStyles.inspectorDefaultMargins.name);
                var foldouts = marginGroups.SelectMany(LayoutEntries).Where(entry =>
                    ((GUIStyle)entry.GetType().GetProperty("style").GetValue(entry)).name == EditorStyles.foldout.name).ToArray();
                Assert.That(foldouts, Is.Not.Empty);
                foreach (var foldout in foldouts) {
                    var rect = (Rect)foldout.GetType().GetField("rect").GetValue(foldout);
                    Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(EditorStyles.inspectorDefaultMargins.padding.left),
                        "The foldout needs an inset so its arrow is not clipped by the header.");
                }
                return (float)top.GetType().GetField("minHeight", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(top);
            } finally { ((IDisposable)panel).Dispose(); }
        }

        private static IEnumerable<object> LayoutEntries(object entry) {
            yield return entry;
            var entries = entry.GetType().GetField("entries")?.GetValue(entry) as IEnumerable;
            if (entries == null) yield break;
            foreach (var child in entries) {
                foreach (var descendant in LayoutEntries(child)) yield return descendant;
            }
        }
    }
}
