#ifndef SPS_INC_ID
#define SPS_INC_ID

#include "sps_cell_hash.cginc"
#include "sps_utils.cginc"

UNITY_INSTANCING_BUFFER_START(SpsInstanceBuf_Id)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Configured)
    #define _SPS_Configured UNITY_ACCESS_INSTANCED_PROP(SpsInstanceBuf_Id, _SPS_Configured)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Id)
    #define _SPS_Id UNITY_ACCESS_INSTANCED_PROP(SpsInstanceBuf_Id, _SPS_Id)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_PlayerId)
    #define _SPS_PlayerId UNITY_ACCESS_INSTANCED_PROP(SpsInstanceBuf_Id, _SPS_PlayerId)
UNITY_INSTANCING_BUFFER_END(SpsInstanceBuf_Id)

inline bool sps_should_abort() {
    if (!sps_to_bool(_SPS_Configured)) return true;
    #if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
        return unity_StereoEyeIndex != 0;
    #else
        return false;
    #endif
}

inline uint sps_player_id() {
    return sps_to_uint(_SPS_PlayerId);
}

inline uint sps_id() {
    return sps_to_uint(_SPS_Id);
}

inline uint sps_id_hash() {
    uint playerId = sps_player_id();
    uint id = sps_id();
    if (playerId == 0u) return id;
    return sps_hash_mix(id ^ sps_hash_mix(playerId));
}

#endif
