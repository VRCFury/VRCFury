using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Utils;
using VF.Utils.Controller;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal class ClipRotationOrderTest {
        [Test]
        public void SavePreservesRotationOrdersAndCurvesAcrossRemappedCopies([Values(0, 1, 2, 3, 4, 5)] int order) {
            var root = new GameObject("Avatar");
            var source = new AnimationClip();
            var context = new VFSaveContext(root, false);
            try {
                foreach (var path in new[] { "Bone", "Other" }) {
                    foreach (var axis in new[] { "x", "y", "z" }) {
                        AnimationUtility.SetEditorCurve(
                            source,
                            EditorCurveBinding.FloatCurve(path, typeof(Transform), "localEulerAnglesRaw." + axis),
                            AnimationCurve.Linear(0, 10, 1, 45)
                        );
                    }
                }
                using (var serialized = new SerializedObject(source)) {
                    foreach (var arrayName in new[] { "m_EulerCurves", "m_EditorCurves", "m_EulerEditorCurves" }) {
                        var array = serialized.FindProperty(arrayName);
                        for (var i = 0; i < (array?.arraySize ?? 0); i++) {
                            var element = array.GetArrayElementAtIndex(i);
                            var rotation = element.FindPropertyRelative("curve.m_RotationOrder");
                            if (rotation == null) continue;
                            rotation.intValue = element.FindPropertyRelative("path").stringValue == "Bone"
                                ? order : (order + 1) % 6;
                        }
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                var originalOrders = ReadOrders(source);
                Assert.That(originalOrders["Bone"], Is.All.EqualTo(order));
                Assert.That(originalOrders["Other"], Is.All.EqualTo((order + 1) % 6));
                var before = EditorJsonUtility.ToJson(source);

                foreach (var name in new[] { "First", "Second" }) {
                    var owner = new GameObject(name);
                    owner.transform.SetParent(root.transform, false);
                    foreach (var path in new[] { "Bone", "Other" }) {
                        new GameObject(path).transform.SetParent(owner.transform, false);
                    }
                    var loaded = VFClip.Load(source, new VFLoadContext {
                        OwnerObject = owner,
                        AnimatorObject = root,
                        ObjectPaths = new VRCFObjectPathCache()
                    });
                    owner.name += "Moved";
                    var saved = (AnimationClip)loaded.Save(context);
                    var savedOrders = ReadOrders(saved);
                    foreach (var pair in originalOrders) {
                        Assert.That(savedOrders[owner.name + "/" + pair.Key], Is.EqualTo(pair.Value));
                    }
                    var bindings = AnimationUtility.GetCurveBindings(source);
                    Assert.That(AnimationUtility.GetCurveBindings(saved).Length, Is.EqualTo(bindings.Length));
                    foreach (var binding in bindings) {
                        var remapped = binding;
                        remapped.path = owner.name + "/" + binding.path;
                        Assert.That(
                            AnimationUtility.GetEditorCurve(saved, remapped).keys,
                            Is.EqualTo(AnimationUtility.GetEditorCurve(source, binding).keys)
                        );
                    }
                }
                Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(before));
            } finally {
                foreach (var asset in context.NewAssets) Object.DestroyImmediate(asset);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(root);
            }
        }

        private static Dictionary<string, List<int>> ReadOrders(AnimationClip clip) {
            var output = new Dictionary<string, List<int>>();
            using var serialized = new SerializedObject(clip);
            foreach (var arrayName in new[] { "m_EulerCurves", "m_EditorCurves", "m_EulerEditorCurves" }) {
                var array = serialized.FindProperty(arrayName);
                for (var i = 0; i < (array?.arraySize ?? 0); i++) {
                    var element = array.GetArrayElementAtIndex(i);
                    var rotation = element.FindPropertyRelative("curve.m_RotationOrder");
                    if (rotation == null) continue;
                    var path = element.FindPropertyRelative("path").stringValue;
                    if (!output.TryGetValue(path, out var orders)) output[path] = orders = new List<int>();
                    orders.Add(rotation.intValue);
                }
            }
            return output;
        }
    }
}
