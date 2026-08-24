using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using VF.Builder.Haptics;

[Category("VRCFury")]
public class SpsPatcherTest {
    private const string Common = @"
struct AppData {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
};

struct VertexOutput {
    float4 position : SV_POSITION;
};

float4 frag() : SV_Target {
    return 0;
}
";

    private const string SpsStub = "void sps_apply(inout SpsInputs input) {}";

    private static IEnumerable<TestCaseData> VertexPrograms() {
        yield return Case("StructOnly", @"
#pragma vertex vert
VertexOutput vert(AppData data) {
    VertexOutput output;
    output.position = data.vertex;
    return output;
}");

        yield return Case("StructAlreadyContainsEverySpsSemantic", @"
struct CompleteData {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    uint vertexId : SV_VertexID;
    float4 color : COLOR;
};
#pragma vertex vert
float4 vert(CompleteData data) : SV_POSITION {
    return data.vertex + (data.normal.x + data.tangent.x + data.vertexId + data.color.x) * 0;
}");

        yield return Case("ConditionalStructFields", @"
#define USE_NORMAL
struct ConditionalData {
    float4 vertex : POSITION;
#if defined(USE_NORMAL)
    float3 normal : NORMAL;
#endif
};
#pragma vertex vert
float4 vert(ConditionalData data) : SV_POSITION {
    return data.vertex;
}");

        yield return Case("StructAfterPrimitive", @"
#pragma vertex vert
VertexOutput vert(uint vertexId : SV_VertexID, AppData data) {
    VertexOutput output;
    output.position = data.vertex + vertexId * 0;
    return output;
}");

        yield return Case("StructWithAllPrimitiveInputs", @"
#pragma vertex vert
VertexOutput vert(
    AppData data,
    uint vertexId : SV_VertexID,
    uint instanceId : SV_InstanceID,
    float4 color : COLOR0,
    float2 uv : TEXCOORD7
) {
    VertexOutput output;
    output.position = data.vertex + (vertexId + instanceId + color.x + uv.x) * 0;
    return output;
}");

        yield return Case("PrimitivePosition", @"
#pragma vertex vert
float4 vert(float4 vertex : POSITION) : SV_POSITION {
    return vertex;
}");

        yield return Case("PrimitivePosition0", @"
#pragma vertex vert
float4 vert(float4 vertex : POSITION0) : SV_POSITION {
    return vertex;
}");

        yield return Case("PrimitiveInputsOnly", @"
#pragma vertex vert
float4 vert(
    float4 vertex : POSITION,
    float3 normal : NORMAL,
    float4 tangent : TANGENT,
    uint vertexId : SV_VertexID,
    float4 color : COLOR
) : SV_POSITION {
    return vertex + (normal.x + tangent.x + vertexId + color.x) * 0;
}");

        yield return Case("InModifier", @"
#pragma vertex vert
VertexOutput vert(in AppData data) {
    VertexOutput output;
    output.position = data.vertex;
    return output;
}");

        yield return Case("InoutWithOutParam", @"
#pragma vertex vert
void vert(float4 vertex : POSITION, inout float2 uv : TEXCOORD0, out VertexOutput output) {
    uv.x += 1;
    output.position = vertex;
}");

        yield return Case("PrimitiveInputAndOutputSemantics", @"
#pragma vertex vert
void vert(float4 vertex : POSITION, out float4 position : SV_POSITION) {
    position = vertex;
}");

        yield return Case("MultiplePrimitiveOutputs", @"
#pragma vertex vert
void vert(
    float4 vertex : POSITION,
    out float4 position : SV_POSITION,
    out float2 uv : TEXCOORD0,
    out float fog : TEXCOORD1
) {
    position = vertex;
    uv = 0;
    fog = 0;
}");

        yield return Case("MultiplePrimitiveInputsAndOutputs", @"
#pragma vertex vert
void vert(
    float4 vertex : POSITION,
    float3 normal : NORMAL,
    uint vertexId : SV_VertexID,
    out float4 position : SV_POSITION,
    out float2 uv : TEXCOORD0,
    out float fog : TEXCOORD1
) {
    position = vertex + (normal.x + vertexId) * 0;
    uv = 0;
    fog = 0;
}");

        yield return Case("OutParamBeforeStruct", @"
struct AuxiliaryData { float2 uv : TEXCOORD0; };
#pragma vertex vert
void vert(out VertexOutput output, float4 vertex : POSITION, inout AuxiliaryData data) {
    data.uv.x += 1;
    output.position = vertex;
}");

        yield return Case("InoutWithReturnValue", @"
#pragma vertex vert
float4 vert(float4 vertex : POSITION, inout float2 uv : TEXCOORD0) : SV_POSITION {
    uv.x += 1;
    return vertex;
}");

        yield return Case("StructAndPrimitiveWithPreprocessor", @"
#define USE_VERTEX_ID
#pragma vertex vert
VertexOutput vert(
    AppData data
#if defined(USE_VERTEX_ID)
    , uint vertexId : SV_VertexID
#endif
) {
    VertexOutput output;
    output.position = data.vertex;
#if defined(USE_VERTEX_ID)
    output.position += vertexId * 0;
#endif
    return output;
}");

        yield return Case("PrimitivesWithPreprocessor", @"
#define USE_NORMAL
#pragma vertex vert
float4 vert(
    float4 vertex : POSITION
#if defined(USE_NORMAL)
    , float3 normal : NORMAL
#endif
) : SV_POSITION {
#if defined(USE_NORMAL)
    vertex += normal.x * 0;
#endif
    return vertex;
}");

        yield return Case("DisabledPreprocessorPrimitive", @"
#pragma vertex vert
float4 vert(
    float4 vertex : POSITION
#if 0
    , uint vertexId : SV_VertexID
#endif
) : SV_POSITION {
    return vertex;
}");

        yield return Case("CommentsInParameterList", @"
#pragma vertex vert
VertexOutput vert(
    AppData data, // The main mesh data
    /* This comment spans
       multiple lines. */
    uint vertexId : SV_VertexID /* trailing comment */
) {
    VertexOutput output;
    output.position = data.vertex + vertexId * 0;
    return output;
}");

        yield return Case("ParameterDeclarationSplitAcrossLines", @"
#pragma vertex vert
float4 vert(
    float4
    vertex
    :
    POSITION,
    uint
    vertexId
    : SV_VertexID
) : SV_POSITION {
    return vertex + vertexId * 0;
}");

        yield return Case("PreprocessorElseAndElif", @"
#define INPUT_MODE 2
#pragma vertex vert
float4 vert(
    float4 vertex : POSITION
#if INPUT_MODE == 1
    , float3 normal : NORMAL
#elif INPUT_MODE == 2
    , uint vertexId : SV_VertexID
#else
    , uint instanceId : SV_InstanceID
#endif
) : SV_POSITION {
#if INPUT_MODE == 1
    return vertex + normal.x * 0;
#elif INPUT_MODE == 2
    return vertex + vertexId * 0;
#else
    return vertex + instanceId * 0;
#endif
}");

        yield return Case("ManyConditionalParameters", @"
#define USE_NORMAL
#define USE_UV_OUTPUT
#define INPUT_MODE 2
#pragma vertex vert
float4 vert(
    float4 vertex : POSITION
#ifdef USE_NORMAL
    , float3 normal : NORMAL
#endif
#ifndef SKIP_TANGENT
    , float4 tangent : TANGENT
#endif
#if INPUT_MODE == 1
    , uint instanceId : SV_InstanceID
#elif INPUT_MODE == 2
    , uint vertexId : SV_VertexID
#else
    , float2 extra : TEXCOORD7
#endif
#ifdef USE_UV_OUTPUT
    , out float2 uv : TEXCOORD0
#endif
    , float4 color : COLOR
) : SV_POSITION {
#ifdef USE_UV_OUTPUT
    uv = 0;
#endif
    return vertex + (normal.x + tangent.x + vertexId + color.x) * 0;
}");

        yield return Case("ConditionalStructParameter", @"
#define USE_STRUCT
#pragma vertex vert
float4 vert(
#if defined(USE_STRUCT)
    AppData data
#else
    float4 vertex : POSITION
#endif
) : SV_POSITION {
#if defined(USE_STRUCT)
    return data.vertex;
#else
    return vertex;
#endif
}");

        yield return Case("ConditionalDeclaredStructTypes", @"
struct FirstData { float4 vertex : POSITION; };
struct SecondData { float4 vertex : POSITION; };
#define USE_SECOND
#pragma vertex vert
float4 vert(
#if defined(USE_SECOND)
    SecondData data
#else
    FirstData data
#endif
) : SV_POSITION {
    return data.vertex;
}");

        yield return Case("DisabledOptionalFirstParameter", @"
#pragma vertex vert
float4 vert(
#if 0
    float extra : TEXCOORD5,
#endif
    float4 vertex : POSITION
) : SV_POSITION {
    return vertex;
}");

        yield return Case("ParameterModifierCombinations", @"
#pragma vertex vert
void vert(
    const AppData data,
    inout float weight : TEXCOORD5,
    out nointerpolation uint category : TEXCOORD6,
    out VertexOutput output
) {
    weight += 0;
    category = 0;
    output.position = data.vertex;
}");

        yield return Case("ArrayPrimitiveParameter", @"
#pragma vertex vert
float4 vert(
    float4 vertex : POSITION,
    float2 uvs [ 2 ] : TEXCOORD5
) : SV_POSITION {
    return vertex + (uvs[0].x + uvs[1].x) * 0;
}");

        yield return Case("TemplatePrimitiveType", @"
#pragma vertex vert
float4 vert(vector < float, 4 > vertex : POSITION) : SV_POSITION {
    return vertex;
}");

        yield return Case("DefaultParameterValue", @"
#pragma vertex vert
float4 vert(
    float4 vertex : POSITION,
    float weight : TEXCOORD5 = 0
) : SV_POSITION {
    return vertex + weight * 0;
}");

        yield return Case("DefaultParameterWithoutSemantic", @"
#pragma vertex vert
float4 vert(float4 vertex : POSITION, uniform float weight = 0) : SV_POSITION {
    return vertex + weight * 0;
}");

        yield return Case("DefaultConstructorParameterValue", @"
#pragma vertex vert
float4 vert(
    float4 vertex : POSITION,
    float4 extra : TEXCOORD5 = float4(0, 0, 0, 0)
) : SV_POSITION {
    return vertex + extra * 0;
}");

        yield return Case("MultipleInputStructs", @"
struct PositionData { float4 vertex : POSITION; };
struct NormalData { float3 normal : NORMAL; };
#pragma vertex vert
VertexOutput vert(PositionData positionData, NormalData normalData) {
    VertexOutput output;
    output.position = positionData.vertex + normalData.normal.x * 0;
    return output;
}");

        yield return Case("StandardSimpleOverload", @"
struct VertexOutputSimple { float4 position : SV_POSITION; };
#pragma vertex vert
VertexOutputSimple vert(float4 vertex : POSITION) {
    VertexOutputSimple output;
    output.position = vertex;
    return output;
}
VertexOutput vert(AppData data) {
    VertexOutput output;
    output.position = data.vertex;
    return output;
}");

        yield return Case("FastFurSkinLayerOverload", @"
#define FUR_SKIN_LAYER
struct fragInput { float4 position : SV_POSITION; };
struct hullGeomInput { float4 position : SV_POSITION; };
#pragma vertex vert
fragInput vert(AppData data) {
    fragInput output;
    output.position = data.vertex;
    return output;
}
hullGeomInput vert(float4 vertex : POSITION) {
    hullGeomInput output;
    output.position = vertex;
    return output;
}");

        yield return Case("FastFurNonSkinLayerOverload", @"
struct fragInput { float4 position : SV_POSITION; };
struct hullGeomInput { float4 position : SV_POSITION; };
#pragma vertex vert
#if defined(FUR_SKIN_LAYER)
fragInput vert(AppData data) {
    fragInput output;
    output.position = data.vertex;
    return output;
}
#endif
hullGeomInput vert(float4 vertex : POSITION) {
    hullGeomInput output;
    output.position = vertex;
    return output;
}");

        yield return Case("PragmaTrailingComment", @"
#pragma vertex vert // trailing comment
float4 vert(float4 vertex : POSITION) : SV_POSITION {
    return vertex;
}");

        yield return Case("MultilineSignature", @"
#pragma vertex vert
float4
vert
(
    float4 vertex : POSITION
)
    : SV_POSITION
{
    return vertex;
}");
    }

    private static TestCaseData Case(string name, string vertexProgram) {
        return new TestCaseData(Common + vertexProgram)
            .SetName("Compiles_" + name);
    }

    private const string ShaderFamilyCommon = @"
struct appdata {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
};
struct VertexInput {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
};
struct Attributes {
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
};
struct VertexOut { float4 position : SV_POSITION; };
struct v2f { float4 position : SV_POSITION; };
struct VertexControl { float4 position : SV_POSITION; };
struct VertexOutput { float4 position : SV_POSITION; };
struct VertexOutputForwardBase { float4 position : SV_POSITION; };
struct PackedVaryings { float4 position : SV_POSITION; };
float4 frag() : SV_Target { return 0; }
";

    private static IEnumerable<TestCaseData> ShaderFamilyPrograms() {
        yield return ShaderCase("PoiyomiToon9", true, @"
#pragma vertex vert
VertexOut vert(
#ifndef POI_TESSELLATED
    appdata v
#else
    tessAppData v
#endif
) {
    VertexOut output;
    output.position = v.vertex;
    return output;
}");

        yield return ShaderCase("PoiyomiToon8AndOptimized", true, @"
#pragma vertex vert
VertexOut vert(appdata v) {
    VertexOut output;
    output.position = v.vertex;
    return output;
}");

        yield return ShaderCase("LilToon", false, @"
#pragma vertex vert
struct v2f vert(struct appdata input) {
    struct v2f output;
    output.position = input.vertex;
    return output;
}");

        yield return ShaderCase("RealSweatMain", true, @"
#pragma vertex vertNoTess
v2f vertNoTess(appdata v, uint vertexID : SV_VertexID) {
    v2f output;
    output.position = v.vertex + vertexID * 0;
    return output;
}");

        yield return ShaderCase("RealSweatFallback", true, @"
#pragma vertex vertFallback
float4 vertFallback(float4 vertex : POSITION) : SV_POSITION {
    return vertex;
}");

        yield return ShaderCase("MochieWater", true, @"
#pragma vertex vert
v2f vert(
#ifdef TESSELLATION_VARIANT
    TessellationControlPoint v
#else
    appdata v
#endif
) {
    v2f output;
    output.position = v.vertex;
    return output;
}");

        yield return ShaderCase("MochieWaterTessellated", true, @"
struct TessellationControlPoint {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
};
#pragma vertex vertStandin
TessellationControlPoint vertStandin(appdata v) {
    TessellationControlPoint output;
    output.vertex = v.vertex;
    output.normal = v.normal;
    output.tangent = v.tangent;
    return output;
}");

        yield return ShaderCase("AudioLink", true, @"
#pragma vertex vert
VertexControl vert(appdata v) {
    VertexControl output;
    output.position = v.vertex;
    return output;
}");

        yield return ShaderCase("VrcSdkToonStandard", true, @"
#pragma vertex vert
VertexOutput vert(VertexInput v) {
    VertexOutput output;
    output.position = v.vertex;
    return output;
}");

        yield return ShaderCase("UnityStandard", true, @"
#pragma vertex vertBase
VertexOutputForwardBase vertBase(VertexInput v) {
    VertexOutputForwardBase output;
    output.position = v.vertex;
    return output;
}");

        yield return ShaderCase("XiexeAndRaliv", true, @"
#pragma vertex vert
VertexOutput vert(VertexInput v) {
    VertexOutput output;
    output.position = v.vertex;
    return output;
}");

        yield return ShaderCase("Z3yShaderGraph", false, @"
#pragma vertex vert
PackedVaryings vert(Attributes input) {
    PackedVaryings output;
    output.position = input.positionOS;
    return output;
}");
    }

    private static TestCaseData ShaderCase(string name, bool isCgProgram, string vertexProgram) {
        return new TestCaseData(ShaderFamilyCommon + vertexProgram, isCgProgram)
            .SetName("CompilesShaderFamily_" + name);
    }

    [TestCaseSource(nameof(ShaderFamilyPrograms))]
    public void CompilesKnownShaderFamily(string originalProgram, bool isCgProgram) {
        var patchedProgram = SpsPatcher.PatchProgram(
            originalProgram,
            isCgProgram,
            SpsStub,
            "",
            false
        );
        AssertShaderCompiles(
            patchedProgram,
            isCgProgram ? "CGPROGRAM" : "HLSLPROGRAM",
            isCgProgram ? "ENDCG" : "ENDHLSL"
        );
    }

    [TestCaseSource(nameof(VertexPrograms))]
    public void CompilesPatchedVertexProgram(string originalProgram) {
        var patchedProgram = SpsPatcher.PatchProgram(
            originalProgram,
            isCgProgram: false,
            spsMain: SpsStub,
            cgIncludes: "",
            isSurfaceShader: false
        );
        AssertShaderCompiles(patchedProgram, "HLSLPROGRAM", "ENDHLSL");

        patchedProgram = SpsPatcher.PatchProgram(
            originalProgram,
            isCgProgram: true,
            spsMain: SpsStub,
            cgIncludes: "",
            isSurfaceShader: false
        );
        AssertShaderCompiles(patchedProgram, "CGPROGRAM", "ENDCG");
    }

    [Test]
    public void CompilesLegacyCgPrecisionTypes() {
        var originalProgram = @"
struct LegacyAppData {
    float4 vertex : POSITION;
    fixed3 normal : NORMAL;
    half4 tangent : TANGENT;
    fixed4 color : COLOR;
    fixed2 uv : TEXCOORD0;
};

fixed4 frag() : SV_Target {
    return 0;
}

#pragma vertex vert
float4 vert(LegacyAppData data, fixed weight : TEXCOORD1) : SV_POSITION {
    return data.vertex + (data.normal.x + data.tangent.x + data.color.x + data.uv.x + weight) * 0;
}";
        var patchedProgram = SpsPatcher.PatchProgram(
            originalProgram,
            isCgProgram: true,
            spsMain: SpsStub,
            cgIncludes: "",
            isSurfaceShader: false
        );
        AssertShaderCompiles(patchedProgram, "CGPROGRAM", "ENDCG");
    }

    [Test]
    public void CompilesVertexFunctionFromCgInclude() {
        var cgInclude = Common + @"
VertexOutput vert(AppData data, uint vertexId : SV_VertexID) {
    VertexOutput output;
    output.position = data.vertex + vertexId * 0;
    return output;
}
";
        var originalProgram = @"
#pragma vertex vert
";
        var patchedProgram = SpsPatcher.PatchProgram(
            originalProgram,
            isCgProgram: true,
            spsMain: SpsStub,
            cgIncludes: cgInclude,
            isSurfaceShader: false
        );
        AssertShaderCompiles(cgInclude + patchedProgram, "CGPROGRAM", "ENDCG");
    }

    [Test]
    public void CompilesSurfaceShaderWithoutCustomVertexFunction() {
        var originalProgram = @"
#pragma surface surf Lambert
struct Input { float2 uv_MainTex; };
void surf(Input input, inout SurfaceOutput output) {
    output.Albedo = 1;
}
";
        var patchedProgram = SpsPatcher.PatchProgram(
            originalProgram,
            isCgProgram: true,
            spsMain: SpsStub,
            cgIncludes: "",
            isSurfaceShader: true
        );
        AssertShaderCompiles(patchedProgram, "CGPROGRAM", "ENDCG", isSurfaceShader: true);
    }

    [Test]
    public void CompilesSurfaceShaderWithInoutAndOutParams() {
        var originalProgram = @"
#pragma surface surf Lambert vertex:vert
#include ""UnityCG.cginc""
struct Input { float2 uv_MainTex; };
void vert(inout appdata_full data, out Input output) {
    UNITY_INITIALIZE_OUTPUT(Input, output);
}
void surf(Input input, inout SurfaceOutput output) {
    output.Albedo = 1;
}
";
        var patchedProgram = SpsPatcher.PatchProgram(
            originalProgram,
            isCgProgram: true,
            spsMain: SpsStub,
            cgIncludes: "",
            isSurfaceShader: true
        );
        AssertShaderCompiles(patchedProgram, "CGPROGRAM", "ENDCG", isSurfaceShader: true);
    }

    private static IEnumerable<TestCaseData> SurfaceShaderFamilyPrograms() {
        yield return SurfaceShaderCase("VrcSdkMobile", "vert", "appdata_full");
        yield return SurfaceShaderCase("WickerWetness", "vertexDataFunc", "appdata_full");
        yield return SurfaceShaderCase("RalivAndXiexe", "vertexDataFunc", "appdata_full_custom", @"
struct appdata_full_custom {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float4 texcoord : TEXCOORD0;
};
");
    }

    private static TestCaseData SurfaceShaderCase(
        string name,
        string vertexFunction,
        string inputType,
        string declarations = ""
    ) {
        var program = $@"
#pragma surface surf Lambert vertex:{vertexFunction}
#include ""UnityCG.cginc""
{declarations}
struct Input {{ float2 uv_MainTex; }};
void {vertexFunction}(inout {inputType} data, out Input output) {{
    UNITY_INITIALIZE_OUTPUT(Input, output);
}}
void surf(Input input, inout SurfaceOutput output) {{
    output.Albedo = 1;
}}
";
        return new TestCaseData(program).SetName("CompilesSurfaceShaderFamily_" + name);
    }

    [TestCaseSource(nameof(SurfaceShaderFamilyPrograms))]
    public void CompilesKnownSurfaceShaderFamily(string originalProgram) {
        var patchedProgram = SpsPatcher.PatchProgram(
            originalProgram,
            true,
            SpsStub,
            "",
            true
        );
        AssertShaderCompiles(patchedProgram, "CGPROGRAM", "ENDCG", isSurfaceShader: true);
    }

    [Test]
    public void LeavesEmptyVertexInputProgramAlone() {
        var originalProgram = Common + @"
#pragma vertex vert
float4 vert() : SV_POSITION { return 0; }
";
        var patchedProgram = SpsPatcher.PatchProgram(
            originalProgram,
            isCgProgram: false,
            spsMain: SpsStub,
            cgIncludes: "",
            isSurfaceShader: false
        );
        Assert.That(patchedProgram, Is.EqualTo(originalProgram));
        AssertShaderCompiles(patchedProgram, "HLSLPROGRAM", "ENDHLSL");
    }

    [Test]
    public void PreservesNonSurfaceInoutCopyOut() {
        var originalProgram = Common + @"
#pragma vertex vert
void vert(inout AppData data) {
    data.vertex.x += 1;
}
";
        var patchedProgram = SpsPatcher.PatchProgram(
            originalProgram,
            isCgProgram: false,
            spsMain: SpsStub,
            cgIncludes: "",
            isSurfaceShader: false
        );
        Assert.That(patchedProgram, Does.Contain("out AppData spsOutput_data"));
        Assert.That(patchedProgram, Does.Contain("AppData vanillaInput_data = (AppData)input"));
        Assert.That(patchedProgram, Does.Contain("spsOutput_data = vanillaInput_data"));
        AssertShaderCompiles(patchedProgram, "HLSLPROGRAM", "ENDHLSL");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WrapsSilentCrosstoneStages(bool isCgProgram) {
        var originalProgram = Common + @"
#define SCSS_FORWARD_VERTEX_INCLUDED
#pragma vertex vert
VertexOutput vert(AppData data) {
    VertexOutput output;
    output.position = data.vertex;
    return output;
}
";
        var patchedProgram = SpsPatcher.PatchProgram(
            originalProgram,
            isCgProgram,
            SpsStub,
            "",
            false
        );
        Assert.That(
            patchedProgram,
            Does.Contain("#if (defined(SHADER_STAGE_VERTEX) || defined(SHADER_STAGE_GEOMETRY))")
        );
        AssertShaderCompiles(
            patchedProgram,
            isCgProgram ? "CGPROGRAM" : "HLSLPROGRAM",
            isCgProgram ? "ENDCG" : "ENDHLSL"
        );
    }

    [Test]
    public void RejectsUnparsedParameterInsteadOfDroppingIt() {
        var originalProgram = Common + @"
#define EXTRA_PARAM uint vertexId : SV_VertexID
#pragma vertex vert
VertexOutput vert(AppData data, EXTRA_PARAM) {
    VertexOutput output;
    output.position = data.vertex + vertexId * 0;
    return output;
}
";
        var exception = Assert.Throws<Exception>(() => SpsPatcher.PatchProgram(
            originalProgram,
            isCgProgram: false,
            spsMain: SpsStub,
            cgIncludes: "",
            isSurfaceShader: false
        ));
        Assert.That(exception.Message, Does.Contain("Failed to parse vertex parameter: EXTRA_PARAM"));
    }

    [Test]
    public void DefinesVanillaTypeOnlyForStructInputs() {
        var primitiveProgram = Common + @"
#pragma vertex vert
float4 vert(float4 vertex : POSITION) : SV_POSITION { return vertex; }
";
        var structProgram = Common + @"
#pragma vertex vert
VertexOutput vert(AppData data) { VertexOutput output; output.position = data.vertex; return output; }
";
        var primitiveOutput = SpsPatcher.PatchProgram(primitiveProgram, false, SpsStub, "", false);
        var structOutput = SpsPatcher.PatchProgram(structProgram, false, SpsStub, "", false);
        Assert.That(primitiveOutput, Does.Not.Contain("#define SPS_VANILLA_STRUCT_EXISTS"));
        Assert.That(primitiveOutput, Does.Not.Contain("#define SPS_VANILLA_VERT_PARAM_TYPE"));
        Assert.That(structOutput, Does.Contain("#define SPS_VANILLA_STRUCT_EXISTS"));
        Assert.That(structOutput, Does.Contain("#define SPS_VANILLA_VERT_PARAM_TYPE AppData"));
    }

    [Test]
    public void RejectsAmbiguousVertexFunctionOverloads() {
        var originalProgram = Common + @"
struct OtherData { float4 vertex : POSITION; };
#pragma vertex vert
VertexOutput vert(AppData data) {
    VertexOutput output;
    output.position = data.vertex;
    return output;
}
float4 vert(OtherData data) : SV_POSITION {
    return data.vertex;
}
";
        Assert.Throws<Exception>(() => SpsPatcher.PatchProgram(
            originalProgram,
            isCgProgram: false,
            spsMain: SpsStub,
            cgIncludes: "",
            isSurfaceShader: false
        ));
    }

    [Test]
    public void TraversesMultipleCgAndHlslPrograms() {
        var input = @"
before
CGPROGRAM
cg body
ENDCG
between
HLSLPROGRAM
hlsl body
ENDHLSL
after
";
        var visited = new List<bool>();
        var output = SpsPatcher.WithEachProgram(input, (program, isCg) => {
            visited.Add(isCg);
            return program.ToUpperInvariant();
        });
        Assert.That(visited, Is.EqualTo(new[] { true, false }));
        Assert.That(output, Does.Contain("CG BODY"));
        Assert.That(output, Does.Contain("HLSL BODY"));
        Assert.That(output, Does.Contain("before"));
        Assert.That(output, Does.Contain("after"));
    }

    private static void AssertShaderCompiles(
        string program,
        string startMarker,
        string endMarker,
        bool isSurfaceShader = false
    ) {
        var fragmentPragma = isSurfaceShader ? "" : "#pragma fragment frag";
        var shaderProgram = $@"
        {startMarker}
        #pragma target 4.0
        {fragmentPragma}
        {program}
        {endMarker}
";
        if (!isSurfaceShader) {
            shaderProgram = $@"
        Pass {{
            {shaderProgram}
        }}
";
        }
        var source = $@"
Shader ""Hidden/VRCFury/Tests/SpsPatcher/{Guid.NewGuid()}"" {{
    SubShader {{
        {shaderProgram}
    }}
}}
";
        var shader = ShaderUtil.CreateShaderAsset(source);
        Assert.That(shader, Is.Not.Null, source);
        var material = new Material(shader);
        try {
            for (var pass = 0; pass < material.passCount; pass++) {
                ShaderUtil.CompilePass(material, pass, true);
            }
            var errors = ShaderUtil.GetShaderMessages(shader)
                .Where(message => message.severity == ShaderCompilerMessageSeverity.Error)
                .Select(message => $"{message.file}:{message.line} {message.message}")
                .ToArray();
            Assert.That(errors, Is.Empty, string.Join("\n", errors) + "\n\n" + source);
        } finally {
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(shader);
        }
    }
}
