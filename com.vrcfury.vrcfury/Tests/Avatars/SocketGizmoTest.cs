using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Component;
using VF.Inspector;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal class SocketGizmoTest {
        [Test]
        public void CachedDataObservesEditsAndKeepsLiveStopReferences() {
            var obj = new GameObject("Socket");
            var stop = new GameObject("Stop");
            try {
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
                socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = stop.transform });
                var data = VRCFuryHapticSocketGizmo.GetGizmoData(socket);
                for (var i = 0; i < 100; i++) {
                    Assert.That(VRCFuryHapticSocketGizmo.GetGizmoData(socket), Is.SameAs(data));
                }
                stop.transform.position = Vector3.forward;
                Assert.That(data.guidedPathStops[0].transform.position, Is.EqualTo(Vector3.forward));

                socket.position = Vector3.right;
                socket.addLight = VRCFuryHapticSocket.AddLight.Ring;
                EditorChanges.Changed();
                var changed = VRCFuryHapticSocketGizmo.GetGizmoData(socket);
                Assert.That(changed, Is.Not.SameAs(data));
                Assert.That(changed.pos, Is.EqualTo(Vector3.right));
                Assert.That(changed.type, Is.EqualTo(VRCFuryHapticSocket.AddLight.Ring));
            } finally {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(stop);
            }
        }

        [Test]
        public void LegacyPrefabIsUpgradedWithoutChangingItAndRefreshesWhenScaled() {
            var path = "Assets/SocketGizmoTest-" + Guid.NewGuid().ToString("N") + ".prefab";
            var obj = new GameObject("Legacy socket");
            GameObject instance = null;
            try {
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                socket.Version = 0;
                socket.name = "D P S old socket";
                socket.overrideLegacyOffset = true;
#pragma warning disable 0612
                socket.legacyOffset = Vector3.forward * 0.2f;
#pragma warning restore 0612
                var serialized = new SerializedObject(socket);
                serialized.FindProperty("offsetsInLocalUnits").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                obj.transform.localScale = Vector3.one * 2;
                var prefab = PrefabUtility.SaveAsPrefabAsset(obj, path);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var original = instance.GetComponent<VRCFuryHapticSocket>();
                var before = EditorJsonUtility.ToJson(original);
                var data = VRCFuryHapticSocketGizmo.GetGizmoData(original);
                Assert.That(data.name, Is.EqualTo(HapticUtils.GetFallbackId(original.owner())));
                Assert.That(data.legacyOffsetLocal.z, Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(VRCFuryHapticSocketGizmo.GetGizmoData(original), Is.SameAs(data));
                Assert.That(EditorJsonUtility.ToJson(original), Is.EqualTo(before));
                Assert.That(PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false), Is.False);

                instance.transform.localScale = Vector3.one * 4;
                var scaled = VRCFuryHapticSocketGizmo.GetGizmoData(original);
                Assert.That(scaled.legacyOffsetLocal.z, Is.EqualTo(0.05f).Within(0.00001f));
                Assert.That(original.Version, Is.EqualTo(0));
                Assert.That(original.name, Is.EqualTo("D P S old socket"));
            } finally {
                if (instance != null) Object.DestroyImmediate(instance);
                Object.DestroyImmediate(obj);
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
