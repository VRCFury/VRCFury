#ifndef SPS_INC_DEFORM_PATH
#define SPS_INC_DEFORM_PATH

#include "../common/sps_types.cginc"

#define SPS_CHAIN_MAX_SOCKETS 8

struct SpsDeformNode {
    float3 world;
    float3 forward;
    float3 up;
    float3 tangentIn;
    float3 tangentOut;
    uint flags;
    bool guided;
};

struct SpsDeformPath {
    // Node zero is the plug frame; the remaining nodes are socket entrances/stops.
    SpsDeformNode nodes[SPS_CHAIN_MAX_SOCKETS + 1];
    float worldLength;
    float bakeScale;
};

bool sps_try_get_path(out SpsDeformPath path) {
    path = (SpsDeformPath)0;
    // Not hooked up yet
    return false;
}

#endif
