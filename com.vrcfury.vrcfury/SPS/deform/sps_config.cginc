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

// ENTRY JOIN

// Bend room as a fraction of plug length. Lower permits tighter bends.
// The join is solved directly from heading, lateral clearance, and fold bounds.
// Two-way sockets choose the shorter permissible connection; guided stops are exempt.
#define SPS_BEND_ROOM 0.2
// Maximum depth that a plug is allowed to "late-join" a socket path. If it's impossible to join by this depth
// (due to SPS_BEND_ROOM), then entry will be rejected.
#define SPS_ENTRY_ADVANCE_MAX 0.5

// HOLE OVERRUN

// Minimum extension beyond a terminal hole, in plug lengths, including entry look-ahead.
// Set to zero to disable the minimum; the path join can still extend beyond a hole.
#define SPS_HOLE_OVERRUN_MIN 0.1
// Length of the radius taper after the terminal hole's overrun, in plug lengths. Zero uses a hard cutoff.
#define SPS_HOLE_CAP_LENGTH 0.05

// BEZIER

// Each Bezier tangent handle is this fraction of the distance between segment endpoints (0 < value < 1).
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

// Blend radius offsets from overall radius at the tip to the profile at this insertion depth, in plug lengths.
#define SPS_RADIUS_PROFILE_FADE_DEPTH 0.1

#endif
