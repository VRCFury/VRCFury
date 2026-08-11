#ifndef SPS_INC_ID
#define SPS_INC_ID

#include "UnityShaderVariables.cginc"
#include "UnityShaderUtilities.cginc"
#include "UnityInstancing.cginc"
#include "sps_cell_hash.cginc"
#include "sps_utils.cginc"

UNITY_INSTANCING_BUFFER_START(SpsInstanceBuf_Id)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Configured)
    #define _SPS_Configured UNITY_ACCESS_INSTANCED_PROP(SpsInstanceBuf_Id, _SPS_Configured)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_IdLow)
    #define _SPS_IdLow UNITY_ACCESS_INSTANCED_PROP(SpsInstanceBuf_Id, _SPS_IdLow)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_IdHigh)
    #define _SPS_IdHigh UNITY_ACCESS_INSTANCED_PROP(SpsInstanceBuf_Id, _SPS_IdHigh)
    #define _SPS_Id SPS_MERGE_SPLIT(_SPS_Id)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_PlayerIdLow)
    #define _SPS_PlayerIdLow UNITY_ACCESS_INSTANCED_PROP(SpsInstanceBuf_Id, _SPS_PlayerIdLow)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_PlayerIdHigh)
    #define _SPS_PlayerIdHigh UNITY_ACCESS_INSTANCED_PROP(SpsInstanceBuf_Id, _SPS_PlayerIdHigh)
    #define _SPS_PlayerId SPS_MERGE_SPLIT(_SPS_PlayerId)
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
    return _SPS_PlayerId;
}

inline uint sps_id() {
    return _SPS_Id;
}

inline uint sps_hash_id(uint id, uint playerId) {
    if (playerId == 0u) return id;
    return sps_hash_mix(id ^ sps_hash_mix(playerId));
}

inline uint sps_id_hash() {
    return sps_hash_id(sps_id(), sps_player_id());
}

#endif
