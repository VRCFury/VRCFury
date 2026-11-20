#ifndef SPS_INC_CURVE
#define SPS_INC_CURVE

#include "sps_config.cginc"

#include "sps_path.cginc"
#include "sps_types.cginc"
#include "sps_utils.cginc"
#include "sps_bezier.cginc"
#include "sps_globals.cginc"

// Closing and reopening can each use their whole half of the hole-to-ring span.
inline float sps_transition_taper_length(float taperLength, float span) {
    return min(taperLength, span * 0.5);
}

// Chords give a lower bound on the curved distance to the ring. Once the taper fits,
// later nodes cannot shorten it. This avoids another Bezier length walk.
inline float sps_guided_taper_scale(uint hole, uint lastRing, float taperLength) {
    if (hole >= lastRing || taperLength <= 0) return 1;
    float span = 0;
    [fastopt]
    for (uint i = hole + 1; i <= lastRing; i++) {
        span += length(sps_path_worlds[i] - sps_path_worlds[i - 1]);
        if (span >= 2 * taperLength) return 1;
        if (sps_path_hole_weights[i] < 0) break;
    }
    return sps_transition_taper_length(taperLength, span) / taperLength;
}

struct SpsHoleTaper {
    float collapseLength;
    float distanceToEnd;
};

inline SpsHoleTaper sps_configured_taper(float plugLength, bool instant) {
    float full = instant ? SPS_TAPER_INSTANT_FULL_RADIUS_LENGTH : SPS_TAPER_SMOOTH_FULL_RADIUS_LENGTH;
    float collapse = instant ? SPS_TAPER_INSTANT_COLLAPSE_LENGTH : SPS_TAPER_SMOOTH_COLLAPSE_LENGTH;
    SpsHoleTaper taper;
    taper.collapseLength = plugLength * max(collapse, 0);
    taper.distanceToEnd = plugLength * max(full, 0) + taper.collapseLength;
    return taper;
}

inline float sps_taper_radius(SpsHoleTaper taper, bool fullAtCut) {
    if (taper.collapseLength > 0) return saturate(taper.distanceToEnd / taper.collapseLength);
    return fullAtCut ? taper.distanceToEnd >= 0 : taper.distanceToEnd > 0;
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
    bool instantClosing = false;
    bool instantExit = path.terminalGuidedHole ? SPS_TAPER_USE_INSTANT_FOR_PATH_TERMINAL_HOLES : SPS_TAPER_USE_INSTANT_FOR_STANDALONE_HOLES;
    SpsHoleTaper taper = (SpsHoleTaper)0;
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
    SpsCurveFrame holeFrame = frame;
    SpsCurveFrame pathSample = frame;
    float3 taperEnd = frame.position;

    [fastopt]
    for (uint sampleIndex = 1; sampleIndex <= path.nodeCount; sampleIndex++) {
        bool measureToRing = inHole && sampleIndex <= lastRing;
        bool needSample = remainingDistance >= 0;
        bool collapsed = inHole && taper.distanceToEnd < 0;
        SpsCurveFrame sampleFrame;
        bool reachedEnd = sps_walk_segment(
            frame,
            sps_path_worlds[sampleIndex],
            sps_path_forwards[sampleIndex],
            remainingDistance,
            collapsed,
            remainingDistance + taper.distanceToEnd,
            taperEnd,
            measureToRing ? distanceFromJoin : -1,
            sampleFrame
        );
        if (needSample) {
            pathSample = sampleFrame;
            vertex = sampleFrame;
            if (inHole && instantClosing) vertex = holeFrame;
            if (collapsed && !instantClosing && remainingDistance <= 0) {
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
            float distanceToNode = -remainingDistance;
            if (distanceToNode <= distanceFromJoin) {
                if (sps_path_hole_weights[sampleIndex] >= 0) continue;
                // Measure the reopening profile backward from the ring.
                taper = sps_configured_taper(path.worldLength, SPS_TAPER_USE_INSTANT_FOR_PATH_UNCOLLAPSE);
                float length = sps_transition_taper_length(taper.distanceToEnd, distanceFromJoin + distanceToNode);
                if (taper.distanceToEnd > 0) taper.collapseLength *= length / taper.distanceToEnd;
                taper.distanceToEnd = length;
                taper.distanceToEnd -= distanceToNode;
                vertex = pathSample;
                if (SPS_TAPER_USE_INSTANT_FOR_PATH_UNCOLLAPSE) vertex = frame;
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
            holeFrame = frame;
            instantClosing = sampleIndex == path.nodeCount ? instantExit : SPS_TAPER_USE_INSTANT_FOR_PATH_HOLES;
            SpsHoleTaper configured = sps_configured_taper(path.worldLength, instantClosing);
            float lengthToEnd = configured.distanceToEnd
                + (1 - sps_path_hole_weights[sampleIndex]) * (path.worldLength + configured.collapseLength);
            lengthToEnd -= sps_path_hole_advance[sampleIndex];
            float scale = sps_guided_taper_scale(sampleIndex, lastRing, lengthToEnd);
            taper.collapseLength = configured.collapseLength * scale;
            taper.distanceToEnd = lengthToEnd * scale - distanceFromJoin;
        }
        if (remainingDistance <= 0 && !(inHole && sampleIndex < lastRing)) break;
    }

    if (inHole) {
        outRadiusMult = sps_taper_radius(taper, remainingDistance > 0);
        // A terminal guide can shorten a taper already in progress, but cannot reopen it.
        if (path.terminalGuidedHole && remainingDistance > 0) {
            vertex = frame;
            SpsHoleTaper exit = sps_configured_taper(path.worldLength, instantExit);
            exit.distanceToEnd -= remainingDistance;
            outRadiusMult = min(outRadiusMult, sps_taper_radius(exit, true));
            taper.distanceToEnd = min(taper.distanceToEnd, exit.distanceToEnd);
        }
        remainingDistance += min(taper.distanceToEnd, 0);
    }
    outPosition = vertex.position;
    outForward = vertex.forward;
    outUp = vertex.up;
    if (!(inHole && instantExit) && remainingDistance > 0) outPosition += outForward * remainingDistance;
}

#endif
