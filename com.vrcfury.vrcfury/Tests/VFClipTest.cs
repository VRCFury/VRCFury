using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Utils;
using VF.Utils.Controller;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal class VFClipTest {
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

namespace VF.Tests {
    using System.Linq;
    using NUnit.Framework;
    using UnityEngine;
    using VF.Utils;
    using VF.Utils.Controller;

    internal class VFClipCurveTest {
        [TestCase(0f, 0f, false, true, false, true)]
        [TestCase(0f, 1f, false, true, true, false)]
        [TestCase(0f, 1f, true, false, true, false)]
        [TestCase(1f, 2f, false, false, false, false)]
        [TestCase(0f, 2f, false, true, true, false)]
        [TestCase(1f, 1f, false, false, false, false)]
        public void ClipClassificationPreservesFloatAndObjectKeys(
            float firstTime,
            float lastTime,
            bool changes,
            bool isStatic,
            bool isTwoState,
            bool isEmptyOrZeroLength
        ) {
            var owner = new GameObject("Renderer");
            var first = new Material(Shader.Find("Standard"));
            var last = new Material(Shader.Find("Standard"));
            try {
                var renderer = owner.AddComponent<MeshRenderer>();
                foreach (var objects in new[] { false, true }) {
                    var clip = VFClip.Create("Classification");
                    var times = new[] { firstTime, lastTime }.Distinct().ToArray();
                    if (objects) {
                        clip.SetCurve(
                            renderer,
                            "m_Materials.Array.data[0]",
                            times.Select((time, i) => new UnityEditor.ObjectReferenceKeyframe {
                                time = time,
                                value = changes && i > 0 ? last : first
                            }).ToArray()
                        );
                    } else {
                        clip.SetAap(
                            "Value",
                            new AnimationCurve(times.Select((time, i) => new Keyframe(time, changes && i > 0 ? 2 : 1)).ToArray())
                        );
                    }
                    var before = clip.GetAllCurves().Single();
                    Assert.That(clip.IsStatic(), Is.EqualTo(isStatic));
                    Assert.That(clip.IsTwoState(), Is.EqualTo(isTwoState));
                    Assert.That(clip.IsEmptyOrZeroLength(), Is.EqualTo(isEmptyOrZeroLength));
                    var after = clip.GetAllCurves().Single();
                    Assert.That(after.Item1, Is.EqualTo(before.Item1));
                    if (objects) Assert.That(after.Item2.ObjectCurve, Is.EqualTo(before.Item2.ObjectCurve));
                    else Assert.That(after.Item2.FloatCurve.keys, Is.EqualTo(before.Item2.FloatCurve.keys));
                }
                var empty = VFClip.Create();
                Assert.That(empty.IsStatic(), Is.True);
                Assert.That(empty.IsTwoState(), Is.False);
                Assert.That(empty.IsEmptyOrZeroLength(), Is.True);
            } finally {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(last);
            }
        }

        [Test]
        public void FilteredClipCurvesAreIndependentSnapshots() {
            var first = new GameObject("First");
            var second = new GameObject("Second");
            var original = new Material(Shader.Find("Standard"));
            var replacement = new Material(Shader.Find("Standard"));
            try {
                var renderer = first.AddComponent<MeshRenderer>();
                var clip = VFClip.Create("Filtered curves");
                clip.SetCurve(first.transform, "m_LocalPosition.x", AnimationCurve.Constant(0, 0, 1));
                clip.SetCurve(second.transform, "m_LocalPosition.x", AnimationCurve.Constant(0, 0, 2));
                clip.SetAap("Unrelated", 3);
                clip.SetCurve(renderer, "m_Materials.Array.data[0]", new[] {
                    new UnityEditor.ObjectReferenceKeyframe { time = 0, value = original }
                });
                var snapshot = clip.GetAllCurves(binding => binding.Targets(first));
                Assert.That(snapshot, Has.Length.EqualTo(2));
                Assert.That(clip.GetAllCurves(), Has.Length.EqualTo(4));
                Assert.That(clip.GetAllCurves(_ => false), Is.Empty);

                var floatPair = snapshot.Single(pair => pair.Item2.IsFloat);
                var objectPair = snapshot.Single(pair => !pair.Item2.IsFloat);
                floatPair.Item2.FloatCurve.AddKey(1, 7);
                objectPair.Item2.ObjectCurve[0].value = replacement;
                Assert.That(clip.GetFloatCurve(floatPair.Item1).length, Is.EqualTo(1));
                Assert.That(clip.GetObjectCurve(objectPair.Item1)[0].value, Is.SameAs(original));
                clip.SetCurve(floatPair.Item1, 8);
                Assert.That(floatPair.Item2.FloatCurve.Evaluate(0), Is.EqualTo(1));
            } finally {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(replacement);
            }
        }
    }
}
