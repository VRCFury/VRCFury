#include "../common/sps_utils.cginc"
#include "sps_deform_config.cginc"

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
inline void sps_bezierSolve(
	float3 p0, float3 p1, float3 p2, float3 p3,
	float lookingForLength,
	float3 initialUp,
	out float remainingLength,
	out float3 position,
	out float3 forward,
	out float3 up
) {
	float sampleT = 0;
	float3 approximateUp = initialUp;
	float totalLength = 0;
	bool exhausted = false;
	// Preserve the original <= 0 early-out, but share the final evaluation.
	if (lookingForLength > 0) {
		sampleT = 1;
		exhausted = true;
		float3 lastPoint = p0;
		float lastT = 0;
		float lastLength = 0;
		float3 lastUp = initialUp;
		[fastopt]
		for (int i = 1; i <= SPS_BEZIER_SAMPLES; i++) {
			float t = float(i) / SPS_BEZIER_SAMPLES;
			float3 currentPoint = sps_bezier(p0, p1, p2, p3, t);
			float3 currentForward = sps_normalize(sps_bezierDerivative(p0, p1, p2, p3, t));
			float3 currentUp = sps_nearest_normal(currentForward, lastUp);
			totalLength += length(currentPoint - lastPoint);
			if (lookingForLength <= totalLength) {
				float fraction = sps_map(lookingForLength, lastLength, totalLength, 0, 1);
				sampleT = lerp(lastT, t, fraction);
				approximateUp = lerp(lastUp, currentUp, fraction);
				exhausted = false;
				break;
			}
			lastT = t;
			lastLength = totalLength;
			lastUp = currentUp;
			approximateUp = currentUp;
			lastPoint = currentPoint;
		}
	}
	remainingLength = exhausted ? max(lookingForLength - totalLength, 0) : 0;
	position = sampleT == 0 ? p0 : sampleT == 1 ? p3 : sps_bezier(p0, p1, p2, p3, sampleT);
	forward = sps_normalize(sps_bezierDerivative(p0, p1, p2, p3, sampleT));
	up = sps_nearest_normal(forward, approximateUp);
}
