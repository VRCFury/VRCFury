#ifndef SPS_INC_BEZIER
#define SPS_INC_BEZIER

#include "sps_utils.cginc"
#include "sps_config.cginc"

// https://en.wikipedia.org/wiki/B%C3%A9zier_curve
float3 sps_bezier(float3 p0, float3 p1, float3 p2, float3 p3, float t)
{
	const float minT = 1-t;
	return
		minT * minT * minT * p0
		+ 3 * minT * minT * t * p1
		+ 3 * minT * t * t * p2
		+ t * t * t * p3;
}
float3 sps_bezierDerivative(float3 p0, float3 p1, float3 p2, float3 p3, float t)
{
	const float minT = 1-t;
	return
		3 * minT * minT * (p1 - p0)
		+ 6 * minT * t * (p2 - p1)
		+ 3 * t * t * (p3 - p2);
}

// https://gamedev.stackexchange.com/questions/105230/points-evenly-spaced-along-a-bezier-curve
// https://gamedev.stackexchange.com/questions/5373/moving-ships-between-two-planets-along-a-bezier-missing-some-equations-for-acce/5427#5427
// https://www.geometrictools.com/Documentation/MovingAlongCurveSpecifiedSpeed.pdf
// https://gamedev.stackexchange.com/questions/137022/consistent-normals-at-any-angle-in-bezier-curve/
struct SpsCurveFrame {
	float3 position;
	float3 forward;
	float3 up;
};

// Sample the requested vertex, then leave the walker at the next node.
// Enclosed spans also need the whole segment length and exit orientation.
inline bool sps_walk_segment(
	inout SpsCurveFrame frame,
	float3 endPoint,
	float3 endForward,
	inout float remainingDistance,
	bool collapsed,
	float taperEndLength,
	inout float3 taperEnd,
	float lookAhead,
	out SpsCurveFrame sampleFrame
) {
	float3 p0 = frame.position;
	float3 p3 = endPoint;
	float handleLength = length(p3 - p0) * SPS_HANDLE_LENGTH;
	float3 p1 = p0 + frame.forward * handleLength;
	float3 p2 = p3 - endForward * handleLength;
	frame.position = endPoint;
	frame.forward = endForward;
	if (sps_is_zero(p3 - p0)) {
		frame.up = sps_nearest_normal(endForward, frame.up);
		sampleFrame = frame;
		return true;
	}

	bool measureToRing = lookAhead >= 0;
	float sampleT = 0;
	float taperEndT = -1;
	float3 approximateUp = frame.up;
	float totalLength = 0;
	float3 endUp = frame.up;
	if (remainingDistance > 0 || measureToRing) {
		sampleT = remainingDistance > 0 ? 1 : 0;
		float3 lastPoint = p0;
		float lastT = 0;
		[fastopt]
		for (int i = 1; i <= SPS_BEZIER_SAMPLES; i++) {
			float t = float(i) / SPS_BEZIER_SAMPLES;
			float3 currentPoint = sps_bezier(p0, p1, p2, p3, t);
			float3 currentForward = sps_normalize(sps_bezierDerivative(p0, p1, p2, p3, t));
			float3 currentUp = sps_nearest_normal(currentForward, endUp);
			float lastLength = totalLength;
			totalLength += length(currentPoint - lastPoint);
			if (collapsed && lastLength < taperEndLength && taperEndLength <= totalLength) {
				taperEndT = sps_map(taperEndLength, lastLength, totalLength, lastT, t);
			}
			if (sampleT == 1 && remainingDistance <= totalLength) {
				float fraction = sps_map(remainingDistance, lastLength, totalLength, 0, 1);
				sampleT = lerp(lastT, t, fraction);
				approximateUp = lerp(endUp, currentUp, fraction);
				if (!measureToRing) {
					endUp = approximateUp;
					break;
				}
			}
			endUp = currentUp;
			lastT = t;
			lastPoint = currentPoint;
			// Once more than half the span is known to lie beyond the vertex,
			// its first-half sample is final, even if this segment continues.
			if (measureToRing && totalLength - remainingDistance > lookAhead) break;
		}
		if (sampleT == 1) approximateUp = endUp;
	}
	// Negative inside a segment. A reopening ring uses the distance still to travel.
	remainingDistance -= totalLength;
	frame.up = endUp;
	sampleFrame.position = sampleT == 0 ? p0 : sampleT == 1 ? p3 : sps_bezier(p0, p1, p2, p3, sampleT);
	// Carry the actual taper endpoint through every later segment of this hole.
	if (taperEndT >= 0) taperEnd = sps_bezier(p0, p1, p2, p3, taperEndT);
	sampleFrame.forward = sampleT == 1 ? endForward : sps_normalize(sps_bezierDerivative(p0, p1, p2, p3, sampleT));
	sampleFrame.up = sps_nearest_normal(sampleFrame.forward, approximateUp);
	return sampleT == 1;
}

#endif
