using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Builder.Haptics;

[Category("VRCFury")]
public class SpsBakedTextureTest {
    [Test]
    public void PreservesFloatBitsAndRowLayout() {
        var baked = new SpsBakedTexture();
        const int width = 8192;
        var patterns = new[] { 0, int.MinValue, 1, 0x007fffff, 0x00800000, 0x3f800000,
            unchecked((int)0xbf800000), 0x7f7fffff, unchecked((int)0xff7fffff) };
        var expected = Enumerable.Range(0, width + 17)
            .Select(i => patterns[i % patterns.Length]).ToArray();
        foreach (var bits in expected) baked.WriteFloat(BitConverter.ToSingle(BitConverter.GetBytes(bits), 0));
        var texture = baked.Save();
        try {
            Assert.That(texture.format, Is.EqualTo(TextureFormat.RFloat));
            Assert.That(texture.width, Is.EqualTo(width));
            Assert.That(texture.height, Is.EqualTo(2));
            Assert.That(texture.mipmapCount, Is.EqualTo(1));
            var actual = texture.GetRawTextureData<int>().ToArray();
            CollectionAssert.AreEqual(expected, actual.Take(expected.Length).ToArray());
            Assert.That(actual.Skip(expected.Length), Is.All.Zero);
        } finally {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
