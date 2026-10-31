using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Component;

[Category("VRCFury")]
public class PlugRadiusCurveTest {
    private static AnimationCurve Fit(params (float depth, float radius)[] rings) {
        var vertices = rings.SelectMany(r => new[] {
            new Vector3(r.radius, 0, r.depth), new Vector3(0, r.radius, r.depth),
            new Vector3(-r.radius, 0, r.depth), new Vector3(0, -r.radius, r.depth)
        }).ToArray();
        var triangles = new List<int>();
        for (var i = 0; i < rings.Length - 1; i++) {
            for (var side = 0; side < 4; side++) {
                var a = i * 4 + side;
                var b = i * 4 + (side + 1) % 4;
                triangles.AddRange(new[] { a, b, a + 4, b, b + 4, a + 4 });
            }
        }
        return PlugRadiusCurve.Fit(vertices, triangles.ToArray(), 1);
    }

    [TestCase(0.1f)]
    [TestCase(0)]
    public void SparseCylinderOrConeNeedsOnlyTwoKeys(float endRadius) {
        var curve = Fit((0, 0.1f), (1, endRadius));
        Assert.That(curve.length, Is.EqualTo(2));
        for (var i = 0; i <= 100; i++) {
            var t = i / 100f;
            Assert.That(curve.Evaluate(t), Is.EqualTo(Mathf.Lerp(0.1f, endRadius, t)).Within(0.00001f));
        }
        Assert.That(curve.Evaluate(-1), Is.EqualTo(0.1f).Within(0.00001f));
        Assert.That(curve.Evaluate(2), Is.EqualTo(endRadius).Within(0.00001f));
    }

    [Test]
    public void IncludesNarrowBulgesBetweenRegularSlices() {
        var curve = Fit((0, 0.05f), (0.501f, 0.05f), (0.502f, 0.2f), (0.503f, 0.05f), (1, 0));
        Assert.That(curve.Evaluate(0.502f), Is.EqualTo(0.2f).Within(0.001f));
        Assert.That(curve.length, Is.LessThanOrEqualTo(6));
        for (var i = 0; i <= 1000; i++) Assert.That(curve.Evaluate(i / 1000f), Is.InRange(-0.000001f, 0.201f));
    }

    [Test]
    public void AveragesDirectionalExtentsInsteadOfDistanceFromAxis() {
        // Right shrinks from 1 to 0 as up grows from 0 to 1; left/down stay zero.
        var curve = PlugRadiusCurve.Fit(new[] {
            new Vector3(1, 0, 0), new Vector3(0, 1, 1), Vector3.zero
        }, new[] { 0, 1, 2 }, 1);
        for (var i = 0; i <= 1000; i++) {
            var z = i / 1000f;
            Assert.That(curve.Evaluate(z), Is.EqualTo(0.25f).Within(0.00001f));
        }
    }

    [Test]
    public void AveragesFourAsymmetricExtentsWithoutCountingDiagonalCorners() {
        var vertices = new[] { 0f, 1f }.SelectMany(z => new[] {
            new Vector3(0.04f, 0.03f, z), new Vector3(-0.02f, 0.03f, z),
            new Vector3(-0.02f, -0.01f, z), new Vector3(0.04f, -0.01f, z)
        }).ToArray();
        var triangles = new List<int>();
        for (var side = 0; side < 4; side++) {
            var next = (side + 1) % 4;
            triangles.AddRange(new[] { side, next, side + 4, next, next + 4, side + 4 });
        }
        var curve = PlugRadiusCurve.Fit(vertices, triangles.ToArray(), 1);
        Assert.That(curve.length, Is.EqualTo(2));
        foreach (var depth in new[] { 0f, 0.25f, 0.75f, 1f })
            Assert.That(curve.Evaluate(depth), Is.EqualTo(0.025f).Within(0.000001f));
    }

    [Test]
    public void ClipsTrianglesAtRootAndConfiguredLength() {
        var curve = Fit((-1, 0.3f), (2, 0));
        Assert.That(curve.Evaluate(0), Is.EqualTo(0.2f).Within(0.00001f));
        Assert.That(curve.Evaluate(1), Is.EqualTo(0.1f).Within(0.00001f));
    }

    [Test]
    public void UsesOutermostSurfaceAndIgnoresUnreferencedVertices() {
        var vertices = new[] {
            new Vector3(0.1f, 0, 0), new Vector3(0.1f, 0, 1), Vector3.zero,
            new Vector3(0.2f, 0, 0), new Vector3(0.2f, 0, 1), Vector3.zero,
            new Vector3(100, 0, 0.5f)
        };
        var curve = PlugRadiusCurve.Fit(vertices, new[] { 0, 1, 2, 3, 4, 5 }, 1);
        Assert.That(curve.length, Is.EqualTo(2));
        Assert.That(curve.Evaluate(0.5f), Is.EqualTo(0.05f).Within(0.00001f));
    }

    [Test]
    public void EmptyGeometryHasZeroRadius() {
        var curve = PlugRadiusCurve.Fit(Array.Empty<Vector3>(), Array.Empty<int>(), 1);
        Assert.That(curve.length, Is.EqualTo(2));
        Assert.That(curve.Evaluate(0.5f), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MeasuresPosedMeshInPlugFrameAndWorldUnits(bool skinned) {
        var root = new GameObject("Plug curve test");
        var mesh = new Mesh();
        try {
            root.transform.SetPositionAndRotation(new Vector3(7, -2, 3), Quaternion.Euler(35, 67, 19));
            root.transform.localScale = Vector3.one * 2;
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = false;
            plug.useBoneMask = false;
            var child = new GameObject("Renderer");
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(0, 0, -0.1f);
            mesh.vertices = new[] {
                new Vector3(0.1f, 0, 0.1f), new Vector3(0, 0.1f, 0.1f),
                new Vector3(-0.1f, 0, 0.1f), new Vector3(0, -0.1f, 0.1f), new Vector3(0, 0, 0.6f)
            };
            mesh.triangles = new[] { 0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4 };
            Renderer renderer;
            if (skinned) {
                // Verify the fit uses baked positions, not just the source mesh's rest pose.
                mesh.AddBlendShapeFrame("Wider", 100, new[] {
                    new Vector3(0.1f, 0, 0), Vector3.zero,
                    new Vector3(-0.1f, 0, 0), Vector3.zero, Vector3.zero
                }, null, null);
                var skin = child.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.SetBlendShapeWeight(0, 100);
                renderer = skin;
            } else {
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = child.AddComponent<MeshRenderer>();
            }
            plug.configureTpsMesh.Add(renderer);
            var size = PlugSizeDetector.GetWorldSize(plug);
            var curve = PlugRadiusCurve.GetRadiusCurve(plug);
            Assert.That(size.worldLength, Is.EqualTo(1).Within(0.00001f));
            var radius = skinned ? 0.3f : 0.2f;
            for (var i = 0; i <= 100; i++) {
                var z = i / 100f;
                Assert.That(curve.Evaluate(z), Is.EqualTo(radius * (1 - z)).Within(0.0021f));
            }
        } finally {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(mesh);
        }
    }
}
