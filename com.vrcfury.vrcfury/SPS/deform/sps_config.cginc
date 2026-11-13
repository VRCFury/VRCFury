#ifndef SPS_INC_CONFIG
#define SPS_INC_CONFIG

// FADE OUT (TOO FAR)

// Distance at which the far-edge capture fade begins, in plug lengths.
#define SPS_REACH_FADE_MIN 1.2
// Distance at which capture ends, also the shared path reach budget, in plug lengths.
#define SPS_REACH_FADE_MAX 1.6
// Within the fadeout ranges, bezier adjusts to "straight" above this value, and mesh lerps to no deformation below this value
// A higher number gives more range to lerp and less to straightening
#define SPS_MESH_FADE_WEIGHT_MAX 0.2

// FADE OUT (SOCKET OUTSIDE OF CAPTURE ZONE)

// Horizontal half-angle in degrees, used with a base radius override.
// Without an override, both axes use the vertical half-angle.
#define SPS_CONE_HORIZONTAL_HALF_ANGLE_DEGREES 50.0
// Vertical half-angle in degrees
#define SPS_CONE_VERTICAL_HALF_ANGLE_DEGREES 95.0
// Maximum base sideways capture radius, in plug radii, unless the material supplies an override.
#define SPS_BASE_RADIUS_MULTIPLIER 6.0
// Rear cylinder depth as a multiple of the base capture radius.
#define SPS_BASE_DEPTH_MULTIPLIER 1.5
// Inner capture radius and cone angles as a fraction of their outer values.
#define SPS_SIDE_FADE_FRACTION_MIN 0.8

// BEND SHARPNESS

// Maximum geometric sharpness for one-way entrances. Higher permits tighter bends.
// 10 allows a right-angle entry at the base with the default 10% entry advance.
// Two-way sockets choose the gentler face; guided stops are never rejected here.
#define SPS_ONE_WAY_BEND_SHARPNESS_MAX 5.0

// HILT / OFFSET "EASING"

// Advance the entry target this far down the path, in plug lengths, to straighten near entry.
#define SPS_ENTRY_ADVANCE 0.1
// Extra entry look-ahead per unit of sideways displacement from the socket axis.
#define SPS_LATERAL_ADVANCE_MULTIPLIER 0.5

// HOLE OVERRUN

// Minimum extension beyond a terminal hole, in plug lengths, including entry look-ahead.
// Set to zero to disable the minimum; SPS_ENTRY_ADVANCE still applies.
#define SPS_HOLE_OVERRUN_MIN 0.1
// Length of the radius taper closing a hole, in plug lengths. Zero uses a hard cutoff.
#define SPS_HOLE_CAP_LENGTH 0.05

// BEZIER

// Each Bezier tangent handle is this fraction of the distance between segment endpoints.
#define SPS_HANDLE_LENGTH 0.5
// Samples used to approximate Bezier arc length and transport the cross-section orientation.
#define SPS_BEZIER_SAMPLES 50

// LEGACY LIGHTS

// Ignore front lights that are basically overlapping their root light
#define SPS_LEGACY_FRONT_DISTANCE_MIN 0.00005
// Maximum distance for pairing legacy socket/front lights, in meters.
#define SPS_LEGACY_FRONT_DISTANCE_MAX 0.1

// PATHING

// Maximum number of socket entrances/stops followed in one deformation path.
#define SPS_CHAIN_SOCKETS_MAX 8

#endif
