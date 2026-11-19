#ifndef SPS_INC_CURVE
#define SPS_INC_CURVE

#include "sps_config.cginc"

#include "sps_path.cginc"
#include "sps_types.cginc"
#include "sps_utils.cginc"
#include "sps_bezier.cginc"
#include "sps_globals.cginc"

inline float sps_hole_cone_length(SpsDeformPathInfo path) {
    float angle = SPS_HOLE_TAPER_ANGLE;
    float worldRadius = _SPS_BakedRadius * path.bakeScale;
    return angle > 0 && angle < 90 ? worldRadius / max(tan(radians(angle)), 1e-6) : 0;
}

inline float sps_hole_taper_end(SpsDeformPathInfo path, uint firstHole, float coneLength) {
    float taperLength = path.worldLength * SPS_HOLE_TAPER_FULL_RADIUS_LENGTH + coneLength;
    float fadeAdvance = (1 - sps_path_hole_weights[firstHole]) * (path.worldLength + coneLength);
    return taperLength + fadeAdvance;
}

// Chords give a lower bound on the curved distance to the ring. Once the taper fits,
// later nodes cannot shorten it. This avoids another Bezier length walk.
inline float sps_guided_taper_scale(uint hole, uint lastRing, float taperLength) {
    float required = 4 * taperLength;
    if (hole >= lastRing || required <= 0) return 1;
    float span = 0;
    [fastopt]
    for (uint i = hole + 1; i <= lastRing; i++) {
        span += length(sps_path_worlds[i] - sps_path_worlds[i - 1]);
        if (span >= required) return 1;
        if (sps_path_hole_weights[i] < 0) break;
    }
    return span / required;
}

// A zero-length cone switches directly between full radius and the centerpoint.
inline float sps_sample_hole_taper(float coneLength, float distanceToEnd, bool fullAtCut) {
    float divisor = coneLength > 0 ? coneLength : 1;
    if (coneLength > 0) return saturate(distanceToEnd / divisor);
    return (fullAtCut ? distanceToEnd >= 0 : distanceToEnd > 0) ? 1 : 0;
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
    bool inHole = false;
    float distanceToEnd = 0;
    float distanceFromJoin = 0;
    uint lastRing = path.nodeCount;
    [fastopt]
    while (lastRing > 0 && sps_path_hole_weights[lastRing] >= 0) lastRing--;

    outDeformationWeight = saturate(path.deformationWeight / SPS_MESH_FADE_WEIGHT_MAX);
    outRadiusMult = 1;
    SpsCurveFrame frame;
    frame.position = sps_path_worlds[0];
    frame.forward = sps_normalize(sps_path_forwards[0]);
    frame.up = sps_nearest_normal(frame.forward, path.plugUp);
    SpsCurveFrame vertex = frame;
    float coneLength = sps_hole_cone_length(path);
    float taperConeLength = coneLength;
    float3 taperEnd = frame.position;

    [fastopt]
    for (uint sampleIndex = 1; sampleIndex <= path.nodeCount; sampleIndex++) {
        bool measureToRing = inHole && sampleIndex <= lastRing;
        bool needSample = remainingDistance >= 0;
        bool collapsed = inHole && distanceToEnd < 0;
        SpsCurveFrame sampleFrame;
        bool reachedEnd = sps_walk_segment(
            frame,
            sps_path_worlds[sampleIndex],
            sps_path_forwards[sampleIndex],
            remainingDistance,
            collapsed,
            remainingDistance + distanceToEnd,
            taperEnd,
            measureToRing ? distanceFromJoin : -1,
            sampleFrame
        );
        if (needSample) {
            vertex = sampleFrame;
            if (collapsed && remainingDistance <= 0) {
                // A terminal hole span has two anchors, even if it crosses intermediate nodes.
                float3 exitPoint = sps_path_worlds[path.nodeCount];
                vertex.position = measureToRing || sps_length_sq(vertex.position - taperEnd) < sps_length_sq(vertex.position - exitPoint)
                    ? taperEnd : exitPoint;
            }
        }
        if (!reachedEnd && !measureToRing) {
            remainingDistance = 0;
            break;
        }
        // Once the vertex is sampled, finish measuring its enclosed span to locate the exit taper.
        if (measureToRing && remainingDistance < 0) {
            float measuredPastVertex = -remainingDistance;
            if (measuredPastVertex <= distanceFromJoin) {
                if (sps_path_hole_weights[sampleIndex] >= 0) continue;
                float distanceToRing = measuredPastVertex;
                vertex = frame;
                // Pin the second half at the ring: hold closed, open gradually, then hold full.
                float thirdOfPinnedSpan = (distanceFromJoin + distanceToRing) / 6;
                outRadiusMult = sps_sample_hole_taper(
                    thirdOfPinnedSpan,
                    2 * thirdOfPinnedSpan - distanceToRing,
                    true
                );
                inHole = false;
            }
            remainingDistance = 0;
            break;
        }
        if (sps_path_hole_weights[sampleIndex] < 0) {
            if (measureToRing) vertex = frame;
            inHole = false;
        } else if (!inHole) {
            inHole = true;
            taperEnd = frame.position;
            distanceFromJoin = remainingDistance;
            float taperLength = sps_hole_taper_end(path, sampleIndex, coneLength);
            if (sampleIndex == 1) taperLength -= path.entryHoleAdvance;
            float scale = sps_guided_taper_scale(sampleIndex, lastRing, taperLength);
            taperConeLength = coneLength * scale;
            distanceToEnd = taperLength * scale - distanceFromJoin;
        }
        if (remainingDistance <= 0 && !(inHole && sampleIndex < lastRing)) break;
    }

    if (inHole) {
        outRadiusMult = sps_sample_hole_taper(taperConeLength, distanceToEnd, remainingDistance > 0);
        remainingDistance = max(remainingDistance + min(distanceToEnd, 0), 0);
    }
    outPosition = vertex.position;
    outForward = vertex.forward;
    outUp = vertex.up;
    if (remainingDistance > 0 && !path.stationaryExit) outPosition += outForward * remainingDistance;
}

#endif
