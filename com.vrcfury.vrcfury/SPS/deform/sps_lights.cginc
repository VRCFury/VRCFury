#ifndef SPS_INC_LIGHTS
#define SPS_INC_LIGHTS

#include "sps_config.cginc"

#include "sps_contacts.cginc"

float3 sps_light_world(uint index) {
    return float3(unity_4LightPosX0[index], unity_4LightPosY0[index], unity_4LightPosZ0[index]);
}

uint sps_light_type(uint index) {
    float range = 5 * rsqrt(unity_4LightAtten0[index]);
    if (!(range < 0.5)) return 0;
    half4 color = unity_LightColor[index];
    if (!sps_is_zero(color.rgb) && color.a > 0) return 0;
    uint kind = (uint)round(fmod(range, 0.1) * 100);
    // System      Emitted offset     Accepted range      Meaning
    // DPS         +.0000             (-.005, +.005)      Base
    // TPS older   +.0000             (-.001, +.001)      Base
    // TPS newer   +.0000             (-.001, +.001)      Base
    // SPS1 older  +.0002             (-.005, +.005)      Base
    // SPS1 older  +.0002             (.0001, .0003)      Hole/ring requires contacts
    // SPS1 later  +.0002             (-.005, +.005)      Base
    // SPS1 later  +.0002             [.00015, .00025]    Hole/ring requires contacts
    // SPS2        +.0006             (-.005, +.005)      Base
    // SPS2        +.0006             [.0005, .0007]      Ignored SPS2 compatibility light
    // SPS3        +.0004             (-.005, +.005)      Base
    // SPS3        +.0004             (.0001, .0007]      Ignored SPS1/SPS2/SPS3 compatibility light
    float marker = fmod(range, 0.001) * 10000;
    if (marker > 1 && marker <= 7) return 0;
    return kind == 1 || kind == 2 || kind == 5 ? kind : 0;
}

SpsSocketCandidate sps_read_light(uint index, float3 plugWorld, float3 plugUp) {
    SpsSocketCandidate socket = (SpsSocketCandidate)0;
    if (!sps_to_bool(_SPS_UseLegacyLights)) return socket;
    uint kind = sps_light_type(index);
    if (kind != 1 && kind != 2) return socket;
    socket.world = sps_light_world(index);
    socket.normal = sps_normalize(plugWorld - socket.world);
    socket.up = plugUp;
    socket.control = kind == 1 ? SPS_SOCKET_FLAG_HOLE : SPS_SOCKET_FLAG_DOUBLE_SIDED;
    socket.control |= SPS_SOCKET_VALID;
    float nearest = SPS_LEGACY_FRONT_DISTANCE_MAX * SPS_LEGACY_FRONT_DISTANCE_MAX;
    [fastopt]
    for (uint front = 0; front < 4; front++) {
        float3 delta = sps_light_world(front) - socket.world;
        float distanceSq = dot(delta, delta);
        if (sps_light_type(front) == 5
            && distanceSq > SPS_LEGACY_FRONT_DISTANCE_MIN * SPS_LEGACY_FRONT_DISTANCE_MIN
            && distanceSq < nearest) {
            socket.normal = normalize(delta);
            nearest = distanceSq;
        }
    }
    return socket;
}

#endif
