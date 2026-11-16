#ifndef SPS_INC_PATH
#define SPS_INC_PATH

#include "sps_config.cginc"

#include "sps_types.cginc"
#include "sps_utils.cginc"
#include "sps_control_points.cginc"
#include "sps_contacts.cginc"
#include "sps_lights.cginc"
#include "sps_bake.cginc"
#include "Packages/com.vrchat.base/ShaderLibrary/VRCContacts.cginc"

// Per-invocation scratch arrays avoid copying the path between helpers.
static float3 sps_path_worlds[SPS_CHAIN_SOCKETS_MAX + 1];
static float3 sps_path_forwards[SPS_CHAIN_SOCKETS_MAX + 1];
static uint sps_path_flags[SPS_CHAIN_SOCKETS_MAX + 1];
static float sps_path_cull_advances[SPS_CHAIN_SOCKETS_MAX + 1];

struct SpsDeformNode {
    float3 world;
    float3 forward;
    float3 up;
    uint flags;
    float deformationWeight;
    float entryAdvance;
};

struct SpsDeformPathInfo {
    float3 plugUp;
    float3 firstSocketUp;
    float worldLength;
    float bakeScale;
    float deformationWeight;
    float terminalAdvance; // How far the moved entrance extends beyond the final node.
};

inline void sps_store_path_node(
    uint index,
    SpsDeformNode node,
    SpsDeformNode previous,
    float worldLength,
    inout float curveWeight
) {
    curveWeight = min(curveWeight, sps_saturated_map(node.deformationWeight, SPS_MESH_FADE_WEIGHT_MAX, 1));
    float3 start = sps_path_worlds[index - 1];
    float3 forward = sps_path_forwards[index - 1];
    float3 offset = node.world - previous.world;
    // Straighten from the previous adjusted endpoint before blending the mesh
    // back to its original shape. Keep each segment's original direction change
    // so an already-faded bend is not applied again by a later node.
    sps_path_worlds[index] = start + lerp(forward * length(offset), offset, curveWeight);
    sps_path_forwards[index] = sps_normalize(forward + (node.forward - previous.forward) * curveWeight);
    sps_path_flags[index] = node.flags;
    float capLength = worldLength * SPS_HOLE_CAP_LENGTH;
    sps_path_cull_advances[index] = (1 - curveWeight) * (worldLength + capLength);
}

// Consume entrance advance as each next node is selected. Return true if the
// whole segment was skipped; otherwise entry becomes the join within it.
inline bool sps_advance_entry(inout SpsDeformNode entry, SpsDeformNode next, inout float advance) {
    float3 delta = next.world - entry.world;
    float span = length(delta);
    float weight = min(entry.deformationWeight, next.deformationWeight);
    if (advance >= span) {
        advance -= span;
        entry = next;
        entry.deformationWeight = weight;
        return true;
    }
    float t = advance / span;
    entry.world += delta * t;
    float3 forward = lerp(entry.forward, next.forward, t);
    entry.forward = sps_normalize(sps_is_zero(forward) ? delta : forward);
    entry.up = sps_nearest_normal(entry.forward, lerp(entry.up, next.up, t));
    entry.deformationWeight = lerp(entry.deformationWeight, weight, t);
    advance = 0;
    return false;
}

// Positive inside, zero on the capture boundary. An ellipse in angular space
// gives either axis a finite boundary through 90 degrees and into the rear half.
inline float sps_capture_margin(float2 sideways, float depth, float radius, float2 angles) {
    float cylinder = min(radius - length(sideways), depth + radius * SPS_BASE_DEPTH_MULTIPLIER);
    angles = clamp(angles, 0, 180);
    float sideSq = dot(sideways, sideways);
    float2 safeAngles = max(angles, 1e-6);
    float angle = angles.x == angles.y ? angles.x : sideSq > 0
        ? rsqrt(dot(sideways * sideways / sideSq, 1 / (safeAngles * safeAngles)))
        : max(angles.x, angles.y);
    float cosine = angle == 90 ? 0 : cos(radians(angle));
    float sine = angle >= 180 ? 0 : sin(radians(angle));
    // abs(cosine) keeps the base radius expanding the zone on either side of 90.
    float cone = depth * sine - sqrt(sideSq) * cosine + radius * abs(cosine);
    // Only angles beyond 90 may extend past the rear cylinder's cap. Keep the
    // signed margin on both sides of the base plane so its fade stays continuous.
    if (angle <= 90) cone = min(cone, depth + radius * SPS_BASE_DEPTH_MULTIPLIER);
    return max(cylinder, cone);
}

inline float sps_deformation_weight(
    float3 delta,
    float3 currentForward,
    float3 currentUp,
    float worldLength,
    float worldRadius,
    float worldBaseActivationRadiusOverride,
    uint flags
) {
    float forwardDistance = dot(delta, currentForward);
    float distance = length(delta);
    float baseActivationRadius = worldBaseActivationRadiusOverride > 0
        ? worldBaseActivationRadiusOverride : worldRadius * SPS_BASE_RADIUS_MULTIPLIER;
    float2 sideways = float2(dot(delta, cross(currentUp, currentForward)), dot(delta, currentUp));
    float2 angles = float2(
        clamp(
            worldBaseActivationRadiusOverride > 0
                ? SPS_CONE_HORIZONTAL_HALF_ANGLE_DEGREES : SPS_CONE_VERTICAL_HALF_ANGLE_DEGREES,
            0,
            180
        ),
        clamp(SPS_CONE_VERTICAL_HALF_ANGLE_DEGREES, 0, 180)
    );
    float outerMargin = sps_capture_margin(sideways, forwardDistance, baseActivationRadius, angles);
    float innerMargin = sps_capture_margin(
        sideways,
        forwardDistance,
        baseActivationRadius * SPS_SIDE_FADE_FRACTION_MIN,
        angles * SPS_SIDE_FADE_FRACTION_MIN
    );
    float boundaryWeight = outerMargin <= 0 ? 0 : innerMargin >= 0 ? 1
        : outerMargin / (outerMargin - innerMargin);
    float farEdgeWeight = 1 - sps_saturated_map(
        distance,
        worldLength * SPS_REACH_FADE_MIN,
        worldLength * SPS_REACH_FADE_MAX
    );
    // A hole snaps at its side/back limits; only its far edge fades.
    if (sps_has_flag(flags, SPS_SOCKET_FLAG_HOLE)) boundaryWeight = boundaryWeight > 0 ? 1 : 0;
    return farEdgeWeight * boundaryWeight;
}

bool sps_prepare_socket(
    SpsSocketCandidate socket,
    SpsDeformNode previous,
    float worldLength,
    float traveledDistance,
    float worldRadius,
    float worldBaseActivationRadiusOverride,
    bool guided,
    bool entrance,
    float advance,
    out SpsDeformNode node,
    out float distanceSq
) {
    node = (SpsDeformNode)0;
    node.world = socket.world;
    // Offset in the socket's own up direction before distance/approach checks.
    // Orientation unlocking below must not change which side of the socket we target.
    if (sps_has_flag(socket.control, SPS_SOCKET_FLAG_RADIUS_OFFSET)) {
        // Estimate once from the processed previous node. Pending entrance
        // advance skips part of this segment before the plug joins it.
        float profileDistance = traveledDistance + max(length(socket.world - previous.world) - advance, 0);
        float profileDepth = profileDistance / worldLength;
        // Use overall radius at/beyond the tip, fading to the profile as insertion begins.
        float profileWeight = saturate((1 - profileDepth) / SPS_RADIUS_PROFILE_FADE_DEPTH);
        float multiplier = lerp(1, sps_bake_radius_multiplier(profileDepth), profileWeight);
        float offset = min(worldRadius * multiplier, socket.radiusOffsetLimit);
        node.world += socket.up * offset;
    }
    float3 delta = node.world - previous.world;
    distanceSq = dot(delta, delta);
    float3 direction = sps_normalize(delta);
    float3 normal = socket.normal;
    float3 up = sps_nearest_normal(normal, socket.up);
    if (sps_has_flag(socket.control, SPS_SOCKET_FLAG_UNLOCK_ALL)) {
        normal = -direction;
        up = sps_nearest_normal(normal, up);
    } else if (sps_has_flag(socket.control, SPS_SOCKET_FLAG_UNLOCK_LOCAL_X)) {
        float3 right = sps_normalize(cross(up, -normal));
        normal = sps_nearest_normal(right, -direction);
        up = sps_normalize(cross(right, normal));
    }
    bool twoWay = sps_has_flag(socket.control, SPS_SOCKET_FLAG_DOUBLE_SIDED);
    bool unlocked = sps_has_flag(socket.control, SPS_SOCKET_FLAG_UNLOCK_ALL)
        || sps_has_flag(socket.control, SPS_SOCKET_FLAG_UNLOCK_LOCAL_X);
    // Guided stops are exempt from rejection. Unlocked two-way sockets already
    // have their orientation determined above, so they do not need a face choice.
    bool checkFixedFace = !guided && (!twoWay || unlocked);
    bool chooseTwoWayFace = twoWay && !unlocked;
    if (checkFixedFace || chooseTwoWayFace || entrance) {
        float advance;
        bool valid = sps_solve_entry_join(
            delta,
            previous.forward,
            -normal,
            worldLength,
            entrance,
            advance
        );
        if (chooseTwoWayFace) {
            float oppositeAdvance;
            bool oppositeValid = sps_solve_entry_join(
                delta,
                previous.forward,
                normal,
                worldLength,
                entrance,
                oppositeAdvance
            );
            if (!valid && !oppositeValid && !guided) return false;
            // Prefer a shorter connection, but penalize skipping farther past the
            // entrance: advancing can bring a join closer to the plug root.
            // This is a selection heuristic, not actual curve length or smoothness.
            float cost = length(delta - normal * advance) + advance;
            float oppositeCost = length(delta + normal * oppositeAdvance) + oppositeAdvance;
            bool flip = oppositeValid && (!valid || oppositeCost < cost);
            if (valid == oppositeValid && oppositeCost == cost) {
                // Break symmetric ties in the incoming frame, independently of
                // the authored normal's sign. Prefer forward, then up, then right.
                float facing = dot(normal, previous.forward);
                if (facing == 0) facing = dot(normal, previous.up);
                if (facing == 0) facing = dot(normal, cross(previous.up, previous.forward));
                flip = facing > 0;
            }
            if (flip) {
                normal = -normal;
                advance = oppositeAdvance;
                valid = oppositeValid;
            }
        } else if (checkFixedFace && !valid) {
            return false;
        }
        node.entryAdvance = valid ? advance : 0;
    }
    node.forward = -normal;
    node.up = up;
    node.flags = socket.control & 0xfc00u; // Flags in bits 10-15, without path key or validity.
    node.deformationWeight = 1;
    if (!guided) {
        // Capture and fade use the effective entrance, including late joining.
        if (entrance) delta += node.forward * node.entryAdvance;
        else if (advance > 0) delta *= max(1 - advance / max(sqrt(distanceSq), 1e-20), 0);
        distanceSq = dot(delta, delta);
        float captureLength = max(worldLength * SPS_REACH_FADE_MAX - traveledDistance, 0) / SPS_REACH_FADE_MAX;
        node.deformationWeight = sps_deformation_weight(
            delta,
            previous.forward,
            previous.up,
            captureLength,
            worldRadius,
            worldBaseActivationRadiusOverride,
            node.flags
        );
    }
    return node.deformationWeight > 0;
}

// Node zero is the plug frame; the remaining nodes are socket entrances/stops.
// fastopt keeps FXC from spending excessive compile time optimizing the nested loops.
bool sps_try_get_path(out SpsDeformPathInfo path) {
    // Unity's shader upgrader mistakes built-in array casts for legacy array declarations.
    [unroll]
    for (uint i = 0; i <= SPS_CHAIN_SOCKETS_MAX; i++) {
        sps_path_worlds[i] = 0;
        sps_path_forwards[i] = 0;
        sps_path_flags[i] = 0;
        sps_path_cull_advances[i] = 0;
    }
    path = (SpsDeformPathInfo)0;
    float4x4 frame = sps_contact_frame();
    float frameScale = length(frame._m02_m12_m22);
    if (!(frameScale > 0)) return false;
    sps_path_worlds[0] = frame._m03_m13_m23;
    sps_path_forwards[0] = sps_normalize(frame._m02_m12_m22);
    path.plugUp = sps_nearest_normal(sps_path_forwards[0], frame._m01_m11_m21);
    path.bakeScale = frameScale;
    path.worldLength = _SPS_BakedLength * path.bakeScale;
    float worldRadius = _SPS_BakedRadius * path.bakeScale;
    float worldBaseActivationRadiusOverride = _SPS_BaseActivationRadiusOverride;
    if (!(path.worldLength > 0)) return false;

    SpsSocketCandidate sockets[SPS_CONTACT_COUNT + 4];
    [fastopt]
    for (uint i = 0; i < SPS_CONTACT_COUNT; i++) {
        sockets[i] = sps_read_contact(i);
    }
    [fastopt]
    for (uint lightIndex = 0; lightIndex < 4; lightIndex++) {
        sockets[SPS_CONTACT_COUNT + lightIndex] = sps_read_light(lightIndex, sps_path_worlds[0], path.plugUp);
    }

    uint used = 0;
    SpsSocketCandidate previous = (SpsSocketCandidate)0;
    SpsDeformNode previousNode = (SpsDeformNode)0;
    previousNode.world = sps_path_worlds[0];
    previousNode.forward = sps_path_forwards[0];
    previousNode.up = path.plugUp;
    SpsDeformNode rootNode = previousNode;
    SpsDeformNode entry = (SpsDeformNode)0;
    uint count = 0;
    float traveledDistance = 0; // Processed chord length, shared by profile sampling and reach.
    float advance = 0;
    float curveWeight = 1;
    [fastopt]
    for (uint step = 1; step <= SPS_CHAIN_SOCKETS_MAX; step++) {
        float remainingLength = max(path.worldLength * SPS_REACH_FADE_MAX - traveledDistance, 0);
        int chosen = -1;
        SpsDeformNode chosenNode = (SpsDeformNode)0;
        float nearest = 1e30;
        // Look for path stops first
        if (sps_socket_key(previous) != 0) {
            [fastopt]
            for (int candidate = 2 * VRC_CONTACT_MAX_TARGETS; candidate < SPS_CONTACT_COUNT; candidate++) {
                SpsSocketCandidate socket = sockets[candidate];
                if (!sps_socket_valid(socket) || (used & (1u << candidate)) != 0) continue;
                // Adding 128 advances the path index without changing the low 7-bit group.
                if (socket.owner != previous.owner || sps_socket_key(socket) != sps_socket_key(previous) + 128u) continue;
                SpsDeformNode node;
                float distanceSq;
                if (!sps_prepare_socket(
                    socket,
                    previousNode,
                    path.worldLength,
                    traveledDistance,
                    worldRadius,
                    worldBaseActivationRadiusOverride,
                    true,
                    false,
                    advance,
                    node,
                    distanceSq
                ) || distanceSq >= nearest) continue;
                chosen = candidate;
                chosenNode = node;
                nearest = distanceSq;
            }
        }
        // If no path stop, look for a new socket entrance (unless it just entered a hole)
        if (chosen < 0 && (step == 1 || !sps_has_flag(previousNode.flags, SPS_SOCKET_FLAG_HOLE))) {
            nearest = remainingLength * remainingLength;
            [fastopt]
            for (int slot = 0; slot < 2 * VRC_CONTACT_MAX_TARGETS + 4; slot++) {
                int candidate = slot < 2 * VRC_CONTACT_MAX_TARGETS ? slot : slot + VRC_CONTACT_MAX_TARGETS;
                SpsSocketCandidate socket = sockets[candidate];
                if (!sps_socket_valid(socket) || sps_socket_key(socket) >= 128u || (used & (1u << candidate)) != 0) continue;
                SpsDeformNode node;
                float distanceSq;
                if (!sps_prepare_socket(
                    socket,
                    previousNode,
                    path.worldLength,
                    traveledDistance,
                    worldRadius,
                    worldBaseActivationRadiusOverride,
                    false,
                    step == 1,
                    advance,
                    node,
                    distanceSq
                ) || distanceSq >= nearest) continue;
                chosen = candidate;
                chosenNode = node;
                nearest = distanceSq;
            }
        }
        if (chosen < 0) break;
        SpsDeformNode segmentStart = previousNode;
        bool skipped = false;
        bool movingEntry = step == 1 || advance > 0;
        if (step == 1) {
            entry = chosenNode;
            entry.deformationWeight = 1;
            path.deformationWeight = chosenNode.deformationWeight;
            advance = chosenNode.entryAdvance;
            count = 1;
            skipped = true;
        } else if (advance > 0) {
            skipped = sps_advance_entry(entry, chosenNode, advance);
        }
        if (movingEntry) {
            // Until advance is consumed, predict the join on the current exit.
            // Once a following node is available, the same walk moves it along
            // that segment instead. No skipped nodes enter the finished path.
            SpsDeformNode join = entry;
            join.world += join.forward * advance;
            if (step > 1) path.deformationWeight = sps_deformation_weight(
                join.world - sps_path_worlds[0],
                sps_path_forwards[0],
                path.plugUp,
                path.worldLength,
                worldRadius,
                worldBaseActivationRadiusOverride,
                join.flags
            );
            join.deformationWeight = min(join.deformationWeight, path.deformationWeight);
            curveWeight = 1;
            sps_store_path_node(
                1,
                join,
                rootNode,
                path.worldLength,
                curveWeight
            );
            path.firstSocketUp = join.up;
            path.terminalAdvance = advance;
            traveledDistance = length(join.world - sps_path_worlds[0]);
            segmentStart = join;
        }
        if (!skipped) {
            traveledDistance += length(chosenNode.world - segmentStart.world);
            sps_store_path_node(
                ++count,
                chosenNode,
                segmentStart,
                path.worldLength,
                curveWeight
            );
        }
        previous = sockets[chosen];
        previousNode = chosenNode;
        used |= 1u << chosen;
    }
    return sps_socket_valid(previous);
}

#endif
