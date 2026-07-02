#ifndef SPS_INC_RESOLVER_TYPES
#define SPS_INC_RESOLVER_TYPES

#include "../common/sps_cell_layout.cginc"
#include "../common/sps_id.cginc"

UNITY_INSTANCING_BUFFER_START(SpsResolverProps)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Enabled)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_Legacy)
UNITY_INSTANCING_BUFFER_END(SpsResolverProps)

#define SPS_RESOLVER_PROP(name) UNITY_ACCESS_INSTANCED_PROP(SpsResolverProps, name)
#define _SPS_Enabled SPS_RESOLVER_PROP(_SPS_Enabled)
#define _SPS_Legacy SPS_RESOLVER_PROP(_SPS_Legacy)

#ifndef SPS_RESOLVER_DEBUG
    #define SPS_RESOLVER_DEBUG 1
#endif

#define SPS_LEGACY_SLOT_COUNT 4

#define SPS_DEBUG_FLAG_HEADER_ENTRY      (1u << 0)
#define SPS_DEBUG_FLAG_FETCH_REJECT      (1u << 1)
#define SPS_DEBUG_FLAG_DISTANCE_CULL     (1u << 2)
#define SPS_DEBUG_FLAG_ELIGIBILITY       (1u << 3)
#define SPS_DEBUG_FLAG_EXIT_REJECT       (1u << 4)
#define SPS_DEBUG_FLAG_ENTRANCE_REJECT   (1u << 5)
#define SPS_DEBUG_FLAG_BEHIND_REJECT     (1u << 6)
#define SPS_DEBUG_FLAG_TOO_FAR_REJECT    (1u << 7)
#define SPS_DEBUG_FLAG_DUPLICATE_REJECT  (1u << 8)
#define SPS_TAG_MATCH_SELF               1u
#define SPS_TAG_MATCH_OTHERS             2u

#define SPS_EXIT_DOT_LIMIT -0.309016994
#define SPS_ENTRANCE_DOT_LIMIT 0.809016994
#define SPS_CHAIN_REF_INVALID -100

#if SPS_RESOLVER_DEBUG
    #define SPS_DEBUG_SET(flags, flag) sps_set_flag(flags, flag)
#else
    #define SPS_DEBUG_SET(flags, flag)
#endif

#ifndef SPS_CANDIDATE_COUNT
    #define SPS_CANDIDATE_COUNT SPS_RESOLVER_CANDIDATE_COUNT
#endif

struct Candidate {
    int cellIndex;
    float distanceSq;
    uint id;
    uint playerId;
};

struct CellData {
    int cellIndex;
    float distanceSq;
    float3 world;
    float3 normal;
    float3 up;
    uint id;
    uint playerId;
};

struct SocketData {
    uint flags;
    uint nextId;
    uint tags[SPS_SOCKET_PAYLOAD_TAG_COUNT];
    float3 tangentIn;
    float3 tangentOut;
};

struct ChainEntry {
    int cellIndex;
    bool flipped;
    bool isGuideTarget;
    float3 world;
    float3 traversalNormal;
    uint flags;
    uint id;
    uint nextId;
    uint playerId;
};

#endif
