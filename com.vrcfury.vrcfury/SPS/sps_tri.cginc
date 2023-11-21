#ifndef SPS_TRI
#define SPS_TRI

#include "sps_globals.cginc"

#define SPS_TRI_DEFINE(prefix) \
    float _SPS_Tri_##prefix##_Enabled;\
    float _SPS_Tri_##prefix##_Root_Center;\
    float _SPS_Tri_##prefix##_Root_Forward;\
    float _SPS_Tri_##prefix##_Root_Up;\
    float _SPS_Tri_##prefix##_Root_Right;\
    float _SPS_Tri_##prefix##_Front_Center;\
    float _SPS_Tri_##prefix##_Front_Forward;\
    float _SPS_Tri_##prefix##_Front_Up;\
    float _SPS_Tri_##prefix##_Front_Right;\
    float _SPS_Tri_##prefix##_IsRing;\
    float _SPS_Tri_##prefix##_IsHole;

SPS_TRI_DEFINE(Self)
SPS_TRI_DEFINE(Other)

struct SpsTriCoords {
    float center;
    float forward;
    float up;
    float right;
};
struct SpsTriData {
    float enabled;
    SpsTriCoords root;
    SpsTriCoords front;
    float isRing;
    float isHole;
};

#define sps_tri_GetCoords(prefix,name) \
    SpsTriCoords name; \
    { \
        name.center = _SPS_Tri_##prefix##_Center; \
        name.forward = _SPS_Tri_##prefix##_Forward; \
        name.up = _SPS_Tri_##prefix##_Up; \
        name.right = _SPS_Tri_##prefix##_Right; \
    }
#define sps_tri_GetData(prefix,name) \
    SpsTriData name; \
    { \
        name.enabled = _SPS_Tri_##prefix##_Enabled; \
        sps_tri_GetCoords(prefix##_Root, tmproot) \
        name.root = tmproot; \
        sps_tri_GetCoords(prefix##_Front, tmpfront) \
        name.front = tmpfront; \
        name.isRing = _SPS_Tri_##prefix##_IsRing; \
        name.isHole = _SPS_Tri_##prefix##_IsHole; \
    }

bool sps_tri_isTouching(SpsTriCoords coords) {
    return coords.center == 1 || coords.forward == 1 || coords.up == 1 || coords.right == 1;
}
bool sps_tri_isTouching(SpsTriData data) {
    return sps_tri_isTouching(data.front) || sps_tri_isTouching(data.root);
}

float sps_tri_triangulate(float centerRange,float offsetRange,float distBetweenStations) {
    if (centerRange <= 0 || centerRange >= 1 || offsetRange <= 0 || offsetRange >= 1) return 999;
    centerRange = (1-centerRange) * 3;
    offsetRange = (1-offsetRange) * 3;
    float inner = (distBetweenStations*distBetweenStations + centerRange*centerRange - offsetRange*offsetRange) / (2*distBetweenStations*centerRange);
    inner = clamp(inner, -1, 1);
    const float ang = acos(inner);
    const float offset = centerRange * sin(ang - SPS_PI/2);
    return -offset;
}
float3 sps_tri_triangulate(SpsTriCoords coords) {
    return float3(
        sps_tri_triangulate(coords.center, coords.right, 0.1),
        sps_tri_triangulate(coords.center, coords.up, 0.1),
        sps_tri_triangulate(coords.center, coords.forward, 0.1)
    );
}
void sps_tri_search(
    SpsTriData data, 
    inout bool ioFound,
    inout float3 ioRootLocal,
    inout bool ioIsRing,
    inout float3 ioRootNormal,
    inout float4 ioColor
) {
    if (data.enabled < 0.5) return;

    const float3 root = sps_tri_triangulate(data.root);
    const float3 front = sps_tri_triangulate(data.front);
    const bool isRing = data.root.center == data.isRing;
    const bool isHole = data.root.center == data.isHole;
    if (!isRing && !isHole) return;

    if (distance(root, front) > 0.1) return;
    if (ioFound && length(root) >= length(ioRootLocal)) return;

    ioIsRing = isRing;
    ioRootLocal = root;
    ioRootNormal = sps_normalize(front - root);
    ioFound = true;
}

#endif
