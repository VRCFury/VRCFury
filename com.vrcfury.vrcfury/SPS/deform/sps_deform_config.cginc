#ifndef SPS_INC_DEFORM_CONFIG
#define SPS_INC_DEFORM_CONFIG

// Maximum number of socket entrances/stops followed in one deformation path.
#define SPS_CHAIN_MAX_SOCKETS 8

// Shared path reach budget and outer capture boundary, in plug lengths.
#define SPS_DEFORM_MAX_REACH 1.6
// Distance at which the far-edge capture fade begins, in plug lengths.
#define SPS_DEFORM_REACH_FADE_START 1.2

// Bend angle at which fading begins
#define SPS_DEFORM_BEND_FADE_START_DEGREES 90.0
// Bend angle at which capture ends
#define SPS_DEFORM_BEND_FADE_END_DEGREES 108.0
// Horizontal half-angle for the fade cone. Don't set to 90 degrees: tan(90) is undefined.
#define SPS_DEFORM_CONE_HORIZONTAL_HALF_ANGLE_DEGREES 45.0
// Vertical half-angle for the fade cone. Don't set to 90 degrees: tan(90) is undefined.
#define SPS_DEFORM_CONE_VERTICAL_HALF_ANGLE_DEGREES 89.0
// Maximum base sideways capture radius, in plug radii, unless the material supplies an override.
#define SPS_DEFORM_BASE_RADIUS_MULTIPLIER 6.0
// Maximum perpendicular distance from the plug axis, as a multiplier of the base capture radius.
#define SPS_DEFORM_MAX_LATERAL_OFFSET_MULTIPLIER 999.0
// Start the rear-cylinder/cone-side fade at this fraction of the base capture radius, before cone widening.
#define SPS_DEFORM_SIDE_FADE_START_FRACTION 0.8

// Advance the entry target this far down the path, in plug lengths, to straighten near entry.
#define SPS_DEFORM_ENTRY_ADVANCE 0.1
// Extra entry look-ahead per unit of sideways displacement from the socket axis.
#define SPS_DEFORM_LATERAL_ADVANCE_MULTIPLIER 0.5
// At/below this capture weight the curve is straight; the final mesh blend happens below it.
#define SPS_DEFORM_MESH_FADE_WEIGHT 0.2
// Minimum extension beyond a terminal hole, in plug lengths, including entry look-ahead.
#define SPS_DEFORM_HOLE_OVERRUN 0.1
// Length of the radius taper closing a hole, in plug lengths.
#define SPS_DEFORM_HOLE_CAP_LENGTH 0.05

// Each Bezier tangent handle is this fraction of the distance between segment endpoints.
#define SPS_DEFORM_HANDLE_LENGTH 0.5
// Samples used to approximate Bezier arc length and transport the cross-section orientation.
#define SPS_BEZIER_SAMPLES 50

// Maximum distance for pairing legacy socket/front lights, in meters.
#define SPS_DEFORM_LEGACY_FRONT_MAX_DISTANCE 0.1
// Ignore effectively coincident front lights below this distance, in meters.
#define SPS_DEFORM_LEGACY_FRONT_MIN_DISTANCE 0.00005

#endif
