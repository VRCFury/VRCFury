using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using VF.Utils;

namespace VF.Tests {
    internal class Texture2DExtensionsTest {
        [Test]
        public void PixelReadbackRestoresActiveRenderTarget() {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a graphics device.");
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(8, 8);
            var destination = new Texture2D(4, 4);
            try {
                RenderTexture.active = target;
                destination.ReadPixelsFrom(Texture2D.whiteTexture, RenderTextureReadWrite.Linear);
                Assert.That(RenderTexture.active, Is.EqualTo(target));
                Assert.That(destination.GetPixel(0, 0), Is.EqualTo(Color.white));
            } finally {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(destination);
            }
        }
    }
}
