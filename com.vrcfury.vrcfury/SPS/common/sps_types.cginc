#ifndef SPS_INC_TYPES
#define SPS_INC_TYPES

#include "sps_flags.cginc"

// Keep transmitted flags in their sender-scale bit positions to avoid unpacking each bit.
#define SPS_SOCKET_FLAG_HOLE (1u << 14)
#define SPS_SOCKET_FLAG_DOUBLE_SIDED (1u << 13)
#define SPS_SOCKET_FLAG_PORTAL 4u
#define SPS_SOCKET_FLAG_RADIUS_OFFSET (1u << 12)
#define SPS_SOCKET_FLAG_UNLOCK_LOCAL_X (1u << 11)
#define SPS_SOCKET_FLAG_UNLOCK_ALL (1u << 10)
// Shader-only flags, not transmitted sender-scale bits.
#define SPS_SOCKET_FLAG_GUIDED (1u << 15)
#define SPS_SOCKET_VALID (1u << 16)

#endif
