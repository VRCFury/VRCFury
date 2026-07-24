#ifndef SPS_INC_DEFORM_GLOBALS
#define SPS_INC_DEFORM_GLOBALS

#include "../common/sps_cell_layout.cginc"
#include "../common/sps_id.cginc"

Texture2D _SPS_Bake;
float4 _SPS_Bake_TexelSize;

UNITY_INSTANCING_BUFFER_START(SpsDeformProps)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Overrun)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_DisableShadows)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_DisableDepth)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_BlendshapeVertCount)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Blendshape0)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Blendshape1)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Blendshape2)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Blendshape3)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Blendshape4)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Blendshape5)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Blendshape6)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Blendshape7)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Blendshape8)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Blendshape9)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Blendshape10)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Blendshape11)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Blendshape12)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Blendshape13)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Blendshape14)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Blendshape15)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_BlendshapeCount)
UNITY_INSTANCING_BUFFER_END(SpsDeformProps)

#define SPS_DEFORM_PROP(name) UNITY_ACCESS_INSTANCED_PROP(SpsDeformProps, name)
#define _SPS_Overrun SPS_DEFORM_PROP(_SPS_Overrun)
#define _SPS_DisableShadows SPS_DEFORM_PROP(_SPS_DisableShadows)
#define _SPS_DisableDepth SPS_DEFORM_PROP(_SPS_DisableDepth)
#define _SPS_BlendshapeVertCount SPS_DEFORM_PROP(_SPS_BlendshapeVertCount)
#define _SPS_Blendshape0 SPS_DEFORM_PROP(_SPS_Blendshape0)
#define _SPS_Blendshape1 SPS_DEFORM_PROP(_SPS_Blendshape1)
#define _SPS_Blendshape2 SPS_DEFORM_PROP(_SPS_Blendshape2)
#define _SPS_Blendshape3 SPS_DEFORM_PROP(_SPS_Blendshape3)
#define _SPS_Blendshape4 SPS_DEFORM_PROP(_SPS_Blendshape4)
#define _SPS_Blendshape5 SPS_DEFORM_PROP(_SPS_Blendshape5)
#define _SPS_Blendshape6 SPS_DEFORM_PROP(_SPS_Blendshape6)
#define _SPS_Blendshape7 SPS_DEFORM_PROP(_SPS_Blendshape7)
#define _SPS_Blendshape8 SPS_DEFORM_PROP(_SPS_Blendshape8)
#define _SPS_Blendshape9 SPS_DEFORM_PROP(_SPS_Blendshape9)
#define _SPS_Blendshape10 SPS_DEFORM_PROP(_SPS_Blendshape10)
#define _SPS_Blendshape11 SPS_DEFORM_PROP(_SPS_Blendshape11)
#define _SPS_Blendshape12 SPS_DEFORM_PROP(_SPS_Blendshape12)
#define _SPS_Blendshape13 SPS_DEFORM_PROP(_SPS_Blendshape13)
#define _SPS_Blendshape14 SPS_DEFORM_PROP(_SPS_Blendshape14)
#define _SPS_Blendshape15 SPS_DEFORM_PROP(_SPS_Blendshape15)
#define _SPS_BlendshapeCount SPS_DEFORM_PROP(_SPS_BlendshapeCount)

SPS_INIT_TEX(_VFGridFinal)

#endif
