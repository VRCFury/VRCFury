using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Builder.Haptics;

public class SpsBakedTextureTest {
    private const int Width = 8192;

    [TestCase(0, Width + 17)]
    [TestCase(Width * 4, Width + 17)]
    [TestCase(0, Width)]
    [TestCase(0, 0)]
    public void PreservesFloatBitsAndRowLayout(int reservedFloats, int sampleCount) {
        using var baked = new SpsBakedTexture(reservedFloats);
        var patterns = new[] {
            0,
            int.MinValue,
            1,
            0x007fffff,
            0x00800000,
            0x3f800000,
            unchecked((int)0xbf800000),
            0x7f7fffff,
            unchecked((int)0xff7fffff)
        };
        var expected = Enumerable.Range(0, sampleCount)
            .Select(i => patterns[i % patterns.Length]).ToArray();
        var index = 0;
        float ReadFloat(int i) => BitConverter.ToSingle(BitConverter.GetBytes(expected[i]), 0);
        for (; index + 2 < expected.Length; index += 3) {
            baked.WriteVector3(new Vector3(
                ReadFloat(index),
                ReadFloat(index + 1),
                ReadFloat(index + 2)
            ));
        }
        for (; index < expected.Length; index++) baked.WriteFloat(ReadFloat(index));
        var texture = baked.Save();
        try {
            Assert.That(texture.format, Is.EqualTo(TextureFormat.RFloat));
            Assert.That(texture.width, Is.EqualTo(Width));
            Assert.That(texture.height, Is.EqualTo(sampleCount / Width + 1));
            Assert.That(texture.mipmapCount, Is.EqualTo(1));
            var actual = texture.GetRawTextureData<int>().ToArray();
            CollectionAssert.AreEqual(expected, actual.Take(expected.Length).ToArray());
            Assert.That(actual.Skip(expected.Length), Is.All.Zero);
        } finally {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
