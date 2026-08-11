using NUnit.Framework;
using UnityEngine;
using VF.Utils;

[Category("VRCFury")]
public class MaterialExtensionsTest {
    [Test]
    public void NativeSettersCanBeReadByFastGetters() {
        var shader = Shader.Find("Hidden/VRCFury/Tests/MaterialExtensions");
        Assert.That(shader, Is.Not.Null);

        var mat = new Material(shader);
        var tex = new Texture2D(1, 1);
        try {
            mat.SetFloat("_Float", 2.5f);
            mat.SetFloat("_Range", 0.75f);
            mat.SetColor("_Color", new Color(0.2f, 0.4f, 0.6f, 0.8f));
            mat.SetVector("_Vector", new Vector4(5, 6, 7, 8));
            mat.SetTexture("_Tex", tex);
            mat.SetTextureScale("_Tex", new Vector2(2, 3));
            mat.SetTextureOffset("_Tex", new Vector2(0.25f, 0.5f));

            Assert.That(mat.TryGetFloatFast("_Float", out var floatValue), Is.True);
            Assert.That(floatValue, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(mat.TryGetFloatFast("_Range", out var rangeValue), Is.True);
            Assert.That(rangeValue, Is.EqualTo(0.75f).Within(0.0001f));

            Assert.That(mat.TryGetColorFast("_Color", out var colorValue), Is.True);
            AssertColor(colorValue, new Color(0.2f, 0.4f, 0.6f, 0.8f));

            Assert.That(mat.TryGetVectorFast("_Vector", out var vectorValue), Is.True);
            AssertVector(vectorValue, new Vector4(5, 6, 7, 8));

            Assert.That(mat.TryGetTextureFast("_Tex", out var textureValue), Is.True);
            Assert.That(textureValue, Is.SameAs(tex));
            AssertVector(mat.GetTextureScaleFast("_Tex"), new Vector2(2, 3));
            AssertVector(mat.GetTextureOffsetFast("_Tex"), new Vector2(0.25f, 0.5f));
        } finally {
            UnityEngine.Object.DestroyImmediate(mat);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    [Test]
    public void FastSettersCanBeReadByNativeGetters() {
        var shader = Shader.Find("Hidden/VRCFury/Tests/MaterialExtensions");
        Assert.That(shader, Is.Not.Null);

        var mat = new Material(shader);
        var tex = new Texture2D(1, 1);
        try {
            mat.SetFloatFast("_Float", 2.5f);
            mat.SetFloatFast("_Range", 0.75f);
            mat.SetColorFast("_Color", new Color(0.2f, 0.4f, 0.6f, 0.8f));
            mat.SetVectorFast("_Vector", new Vector4(5, 6, 7, 8));
            mat.SetTextureFast("_Tex", tex);
            mat.SetTextureScaleFast("_Tex", new Vector2(2, 3));
            mat.SetTextureOffsetFast("_Tex", new Vector2(0.25f, 0.5f));

            Assert.That(mat.GetFloat("_Float"), Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(mat.GetFloat("_Range"), Is.EqualTo(0.75f).Within(0.0001f));
            AssertColor(mat.GetColor("_Color"), new Color(0.2f, 0.4f, 0.6f, 0.8f));
            AssertVector(mat.GetVector("_Vector"), new Vector4(5, 6, 7, 8));
            Assert.That(mat.GetTexture("_Tex"), Is.SameAs(tex));
            AssertVector(mat.GetTextureScale("_Tex"), new Vector2(2, 3));
            AssertVector(mat.GetTextureOffset("_Tex"), new Vector2(0.25f, 0.5f));
        } finally {
            UnityEngine.Object.DestroyImmediate(mat);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    [Test]
    public void FastMaterialGettersUseShaderDefaults() {
        var shader = Shader.Find("Hidden/VRCFury/Tests/MaterialExtensions");
        Assert.That(shader, Is.Not.Null);

        var mat = new Material(shader);
        try {
            Assert.That(mat.TryGetFloatFast("_Float", out var floatValue), Is.True);
            Assert.That(floatValue, Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(mat.TryGetFloatFast("_Range", out var rangeValue), Is.True);
            Assert.That(rangeValue, Is.EqualTo(0.5f).Within(0.0001f));

            Assert.That(mat.TryGetColorFast("_Color", out var colorValue), Is.True);
            AssertColor(colorValue, new Color(0.1f, 0.2f, 0.3f, 0.4f));

            Assert.That(mat.TryGetVectorFast("_Vector", out var vectorValue), Is.True);
            AssertVector(vectorValue, new Vector4(1, 2, 3, 4));

            AssertVector(mat.GetTextureScaleFast("_Tex"), Vector2.one);
            AssertVector(mat.GetTextureOffsetFast("_Tex"), Vector2.zero);
        } finally {
            UnityEngine.Object.DestroyImmediate(mat);
        }
    }

    private static void AssertColor(Color actual, Color expected) {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f));
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f));
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f));
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f));
    }

    private static void AssertVector(Vector4 actual, Vector4 expected) {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
        Assert.That(actual.w, Is.EqualTo(expected.w).Within(0.0001f));
    }

    private static void AssertVector(Vector2 actual, Vector2 expected) {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
    }
}
