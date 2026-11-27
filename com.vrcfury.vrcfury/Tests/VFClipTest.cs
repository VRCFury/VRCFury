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
        public void ReusesSavedClipWhileImportsArePaused() {
            var root = new GameObject("Avatar");
            var clip = new AnimationClip();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/VF Pending Clip.anim");
            try {
                using (VRCFuryAssetDatabase.WithAssetEditing()) {
                    AssetDatabase.CreateAsset(clip, path);
                    var loaded = VFClip.Load(clip, new VFLoadContext());
                    Assert.That(loaded.GetUseOriginalUserClip(root), Is.SameAs(clip));
                }
            } finally {
                if (clip != null && !clip.IsSaved()) Object.DestroyImmediate(clip);
                AssetDatabase.DeleteAsset(path);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SamplingPreservesObjectKeysAndTheAdditiveReferencePose() {
            var root = new GameObject("Avatar");
            var raw = new AnimationClip();
            var rawReference = new AnimationClip();
            var first = new Material(Shader.Find("Standard"));
            var second = new Material(Shader.Find("Standard"));
            try {
                root.AddComponent<MeshRenderer>();
                var rawFloat = EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x");
                var rawObject = EditorCurveBinding.PPtrCurve("", typeof(MeshRenderer), "m_Materials.Array.data[0]");
                AnimationUtility.SetEditorCurve(raw, rawFloat, AnimationCurve.Linear(0, 2, 2, 6));
                AnimationUtility.SetEditorCurve(rawReference, rawFloat, AnimationCurve.Linear(0, 1, 2, 9));
                AnimationUtility.SetObjectReferenceCurve(
                    raw,
                    rawObject,
                    new[] {
                        new ObjectReferenceKeyframe { time = 0, value = first },
                        new ObjectReferenceKeyframe { time = 1, value = second }
                    }
                );
                var settings = AnimationUtility.GetAnimationClipSettings(raw);
                settings.additiveReferencePoseClip = rawReference;
                AnimationUtility.SetAnimationClipSettings(raw, settings);

                var source = VFClip.Load(raw, new VFLoadContext {
                    OwnerObject = root,
                    AnimatorObject = root,
                    ObjectPaths = new VRCFObjectPathCache()
                });
                var sampled = (VFClip)source.EvaluateMotion(0.5f);
                var binding = source.GetFloatBindings()[0];
                Assert.That(sampled.GetLengthInSeconds(), Is.EqualTo(0));
                Assert.That(sampled.GetFloatCurve(binding).keys[0].value, Is.EqualTo(4));
                var objectKey = sampled.GetAllCurves(b => b.propertyName == rawObject.propertyName)[0].Item2.ObjectCurve[0];
                Assert.That(objectKey.value, Is.SameAs(second));
                Assert.That(source.GetFloatCurve(binding).length, Is.EqualTo(2));
                var reference = sampled.GetAdditiveReferencePoseClip();
                Assert.That(reference, Is.Not.SameAs(source.GetAdditiveReferencePoseClip()));
                Assert.That(reference.GetLengthInSeconds(), Is.EqualTo(2));
                Assert.That(reference.GetFloatCurve(binding).Evaluate(1), Is.EqualTo(5));
            } finally {
                Object.DestroyImmediate(raw);
                Object.DestroyImmediate(rawReference);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NewSaveReadsRenamedTargetAfterAnEarlierSaveReusedTheClip() {
            var root = new GameObject("Avatar");
            var target = new GameObject("Before");
            target.transform.SetParent(root.transform, false);
            var folder = AssetDatabase.GenerateUniqueAssetPath("Assets/VF Clip Path Cache Test");
            VRCFuryAssetDatabase.CreateFolder(folder);
            var source = new AnimationClip();
            var saveAfterRename = new VFSaveContext(root);
            try {
                var binding = EditorCurveBinding.FloatCurve("Before", typeof(Transform), "m_LocalPosition.x");
                AnimationUtility.SetEditorCurve(source, binding, AnimationCurve.Constant(0, 1, 2));
                AssetDatabase.CreateAsset(source, folder + "/Original.anim");
                var loaded = VFClip.Load(source, new VFLoadContext {
                    OwnerObject = root,
                    AnimatorObject = root,
                    ObjectPaths = new VRCFObjectPathCache()
                });
                Assert.That(loaded.Save(new VFSaveContext(root)), Is.SameAs(source));

                target.name = "After";
                var saved = (AnimationClip)loaded.Save(saveAfterRename);
                Assert.That(saved, Is.Not.SameAs(source));
                var bindings = AnimationUtility.GetCurveBindings(saved);
                Assert.That(bindings, Has.Length.EqualTo(1));
                Assert.That(bindings[0].path, Is.EqualTo("After"));
                Assert.That(AnimationUtility.GetEditorCurve(saved, bindings[0]).Evaluate(1), Is.EqualTo(2));
                Assert.That(AnimationUtility.GetEditorCurve(source, binding).Evaluate(1), Is.EqualTo(2));
            } finally {
                foreach (var asset in saveAfterRename.NewAssets) Object.DestroyImmediate(asset);
                if (!EditorUtility.IsPersistent(source)) Object.DestroyImmediate(source);
                AssetDatabase.DeleteAsset(folder);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SaveMovesObjectCurvesAndRetainsClipSettings() {
            var root = new GameObject("Avatar");
            var target = new GameObject("Before");
            target.transform.SetParent(root.transform, false);
            target.AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Standard"));
            var source = new AnimationClip {
                frameRate = 30,
                wrapMode = WrapMode.PingPong
            };
            var context = new VFSaveContext(root, false);
            try {
                var original = EditorCurveBinding.PPtrCurve(
                    "Before",
                    typeof(MeshRenderer),
                    "m_Materials.Array.data[0]"
                );
                AnimationUtility.SetObjectReferenceCurve(
                    source,
                    original,
                    new[] { new ObjectReferenceKeyframe { time = 0, value = material } }
                );
                var settings = AnimationUtility.GetAnimationClipSettings(source);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(source, settings);

                var loaded = VFClip.Load(source, new VFLoadContext {
                    OwnerObject = root,
                    AnimatorObject = root,
                    ObjectPaths = new VRCFObjectPathCache()
                });
                target.name = "After";

                var saved = (AnimationClip)loaded.Save(context);
                var bindings = AnimationUtility.GetObjectReferenceCurveBindings(saved);
                Assert.That(bindings, Has.Length.EqualTo(1));
                Assert.That(bindings[0].path, Is.EqualTo("After"));
                Assert.That(AnimationUtility.GetObjectReferenceCurve(saved, bindings[0])[0].value, Is.SameAs(material));
                Assert.That(saved.frameRate, Is.EqualTo(30));
                Assert.That(saved.wrapMode, Is.EqualTo(WrapMode.PingPong));
                Assert.That(AnimationUtility.GetAnimationClipSettings(saved).loopTime, Is.True);
                Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(source)[0].path, Is.EqualTo("Before"));
            } finally {
                foreach (var asset in context.NewAssets) Object.DestroyImmediate(asset);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SaveKeepsUnresolvedPathsAndDropsTargetsRemovedFromTheAnimator() {
            var root = new GameObject("Avatar");
            var moving = new GameObject("Moving");
            var deleted = new GameObject("Deleted");
            moving.transform.SetParent(root.transform, false);
            deleted.transform.SetParent(root.transform, false);
            var source = new AnimationClip();
            var saving = new VFSaveContext(root, reuseSourceAssets: false);
            try {
                foreach (var path in new[] { "Moving", "Deleted", "Unresolved" }) {
                    AnimationUtility.SetEditorCurve(
                        source,
                        EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.x"),
                        AnimationCurve.Constant(0, 1, 2)
                    );
                }
                var loaded = VFClip.Load(source, new VFLoadContext {
                    OwnerObject = root,
                    AnimatorObject = root,
                    ObjectPaths = new VRCFObjectPathCache()
                });

                moving.transform.SetParent(null);
                Object.DestroyImmediate(deleted);
                var saved = (AnimationClip)loaded.Save(saving);
                var bindings = AnimationUtility.GetCurveBindings(saved);
                Assert.That(bindings, Has.Length.EqualTo(1));
                Assert.That(bindings[0].path, Is.EqualTo("Unresolved"));
            } finally {
                foreach (var asset in saving.NewAssets) Object.DestroyImmediate(asset);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(moving);
            }
        }

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
