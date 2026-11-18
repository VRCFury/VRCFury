#ifndef SPS_INC_CURVE
#define SPS_INC_CURVE

#include "sps_config.cginc"

#include "sps_path.cginc"
#include "sps_types.cginc"
#include "sps_utils.cginc"
#include "sps_bezier.cginc"
#include "sps_control_points.cginc"
#include "sps_globals.cginc"

inline float sps_sample_hole_taper(
    SpsDeformPathInfo path,
    uint firstHole,
    float distanceFromHole,
    inout float exitDistance
) {
    float angle = SPS_HOLE_OVERRUN_TAPER_ANGLE;
    float worldRadius = _SPS_BakedRadius * path.bakeScale;
    float coneLength = angle > 0 && angle < 90 ? worldRadius / max(tan(radians(angle)), 1e-6) : 0;
    float fadeAdvance = (1 - sps_path_hole_weights[firstHole]) * (path.worldLength + coneLength);
    float taperEnd = path.worldLength * SPS_HOLE_OVERRUN_MIN + coneLength + fadeAdvance;

    float distanceToEnd = taperEnd - distanceFromHole;
    bool onExit = exitDistance > 0;
    exitDistance = max(exitDistance + min(distanceToEnd, 0), 0);
    float divisor = coneLength > 0 ? coneLength : 1;
    if (coneLength > 0) return saturate(distanceToEnd / divisor);
    return (onExit ? distanceToEnd >= 0 : distanceToEnd > 0) ? 1 : 0;
}

inline void sps_deform_walk_chain(
    SpsDeformPathInfo path,
    float targetDistance,
    out float outDeformationWeight,
    out float outRadiusMult,
    out float3 outPosition,
    out float3 outForward,
    out float3 outUp
) {
    float remainingDistance = max(targetDistance, 0);
    uint firstHole = 0;
    float distanceFromHole = 0;
    outDeformationWeight = saturate(path.deformationWeight / SPS_MESH_FADE_WEIGHT_MAX);
    outRadiusMult = 1;
    outPosition = sps_path_worlds[0];
    outForward = sps_path_forwards[0];
    if (sps_is_zero(outForward)) outForward = float3(0, 0, 1);
    outForward = sps_normalize(outForward);
    outUp = sps_nearest_normal(outForward, path.plugUp);
    float3 startPoint = outPosition;
    float3 startForward = outForward;
    float3 currentUp = outUp;

    [fastopt]
    for (uint sampleIndex = 1; sampleIndex <= path.nodeCount; sampleIndex++) {
        // Path construction already moved the entrance and removed skipped nodes.
        float3 endPoint = sps_path_worlds[sampleIndex];
        float3 endForward = sps_path_forwards[sampleIndex];

        float3 p0;
        float3 p1;
        float3 p2;
        float3 p3;
        sps_deform_segment_control_points(
            startPoint,
            startForward,
            endPoint,
            endForward,
            p0,
            p1,
            p2,
            p3
        );

        bool zeroLength = sps_is_zero(p1 - p0) && sps_is_zero(p2 - p0) && sps_is_zero(p3 - p0);
        float nextRemainingDistance = remainingDistance;
        if (zeroLength) {
            // Coincident endpoints can leave no curve length or derivative to solve.
            outPosition = endPoint;
            outForward = endForward;
            outUp = sps_nearest_normal(endForward, currentUp);
        } else {
            sps_bezierSolve(
                p0,
                p1,
                p2,
                p3,
                remainingDistance,
                currentUp,
                nextRemainingDistance,
                outPosition,
                outForward,
                outUp
            );
        }
        remainingDistance = nextRemainingDistance;

        if (remainingDistance <= 0) break;

        // Use the known endpoint tangent after completing a segment. Subtracting
        // nearly coincident control points can lose its direction to float precision.
        outForward = endForward;
        outUp = sps_nearest_normal(outForward, outUp);
        currentUp = outUp;
        if (sps_path_hole_weights[sampleIndex] < 0) {
            firstHole = 0;
        } else if (firstHole == 0) {
            firstHole = sampleIndex;
            distanceFromHole = remainingDistance;
        }
        startPoint = endPoint;
        startForward = endForward;
    }

    if (firstHole != 0) {
        outRadiusMult = sps_sample_hole_taper(path, firstHole, distanceFromHole, remainingDistance);
    }
    if (remainingDistance > 0) outPosition += outForward * remainingDistance;
}

#endif
