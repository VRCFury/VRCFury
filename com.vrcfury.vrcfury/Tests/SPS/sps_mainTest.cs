namespace VF.Tests {
    using System;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.Rendering;
    using UnityEngine;
    using UnityEngine.Rendering;
    using VF.Builder.Haptics;
    using Object = UnityEngine.Object;

    internal class sps_mainTest : SpsShaderTestBase {
        [Test]
        public void ProductionPathAndCurveCompile() {
            var compiled = ShaderUtil.GetShaderData(shader).GetSerializedSubshader(0).GetPass(0).CompileVariant(
                ShaderType.Fragment,
                Array.Empty<string>(),
                ShaderCompilerPlatform.D3D,
                BuildTarget.StandaloneWindows64
            );
            Assert.That(compiled.Success, Is.True, string.Join("\n", compiled.Messages.Select(m => m.message)));
            Assert.That(compiled.Messages, Is.Empty, string.Join("\n", compiled.Messages.Select(m => m.message)));
            Assert.That(compiled.ShaderData, Is.Not.Null.And.Not.Empty);
        }
    }
}
