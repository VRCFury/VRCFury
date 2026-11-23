#ifndef SPS_INC_TYPES
#define SPS_INC_TYPES

#include "sps_flags.cginc"

// X-scale flags retain their transmitted bit positions.
#define SPS_SOCKET_FLAG_HOLE (1u << 14)
#define SPS_SOCKET_FLAG_DOUBLE_SIDED (1u << 13)
#define SPS_SOCKET_FLAG_PORTAL 4u
#define SPS_SOCKET_FLAG_RADIUS_OFFSET (1u << 12)
#define SPS_SOCKET_FLAG_UNLOCK_LOCAL_X (1u << 11)
#define SPS_SOCKET_FLAG_UNLOCK_ALL (1u << 10)
// Shader-only flags, not transmitted sender-scale bits.
#define SPS_SOCKET_FLAG_GUIDED (1u << 15)
#define SPS_SOCKET_VALID (1u << 16)
// Z-scale bits 1–2 are carried in bits 17–18 of the internal flags.
#define SPS_SOCKET_FLAG_OVERRIDE_INSTANT_COLLAPSE (1u << 17)
#define SPS_SOCKET_FLAG_INSTANT_COLLAPSE (1u << 18)

#endif
