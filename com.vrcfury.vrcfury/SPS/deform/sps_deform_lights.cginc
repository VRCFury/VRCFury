#ifndef SPS_INC_DEFORM_LIGHTS
#define SPS_INC_DEFORM_LIGHTS

#include "sps_deform_contacts.cginc"

float3 sps_light_world(uint index) {
    return float3(unity_4LightPosX0[index], unity_4LightPosY0[index], unity_4LightPosZ0[index]);
}

uint sps_light_type(uint index) {
    float range = 5 * rsqrt(unity_4LightAtten0[index]);
    if (!(range < 0.5)) return 0;
    half4 color = unity_LightColor[index];
    if (!sps_is_zero(color.rgb) && color.a > 0) return 0;
    uint kind = (uint)round(fmod(range, 0.1) * 100);
    // All SPS-marked lights are represented by contacts, including SPS1's .0002 suffix.
    // Never let a filtered-out contact reappear through its compatibility light.
    float marker = fmod(range, 0.001) * 10000;
    if ((marker > 1 && marker < 4) || (marker > 5 && marker < 7)) return 0;
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
    socket.flags = kind == 1 ? SPS_SOCKET_FLAG_HOLE : SPS_SOCKET_FLAG_DOUBLE_SIDED;
    socket.valid = true;
    float nearest = 0.01; // Front lights are 1 cm away; don't pair across distant sockets.
    [unroll]
    for (uint front = 0; front < 4; front++) {
        float3 delta = sps_light_world(front) - socket.world;
        float distanceSq = dot(delta, delta);
        if (sps_light_type(front) == 5 && distanceSq > 0.0000000025 && distanceSq < nearest) {
            socket.normal = normalize(delta);
            nearest = distanceSq;
        }
    }
    return socket;
}

#endif
