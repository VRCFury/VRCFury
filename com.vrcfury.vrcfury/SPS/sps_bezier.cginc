#include "sps_utils.cginc"

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
void sps_bezierSolve(float3 p0, float3 p1, float3 p2, float3 p3, float lookingForLength, out float curveLength, out float3 position, out float3 forward, out float3 up)
{
	#define SPS_BEZIER_SAMPLES 50
	float sampledT[SPS_BEZIER_SAMPLES];
	float sampledLength[SPS_BEZIER_SAMPLES];
	float3 sampledUp[SPS_BEZIER_SAMPLES];
	float totalLength = 0;
	float3 lastPoint = p0;
	sampledT[0] = 0;
	sampledLength[0] = 0;
	sampledUp[0] = float3(0,1,0);
	{
		for(int i = 1; i < SPS_BEZIER_SAMPLES; i++)
		{
			const float t = float(i) / (SPS_BEZIER_SAMPLES-1);
			const float3 currentPoint = sps_bezier(p0, p1, p2, p3, t);
			const float3 currentForward = sps_normalize(sps_bezierDerivative(p0, p1, p2, p3, t));
			const float3 lastUp = sampledUp[i-1];
			const float3 currentUp = sps_nearest_normal(currentForward, lastUp);

			sampledT[i] = t;
			totalLength += length(currentPoint - lastPoint);
			sampledLength[i] = totalLength;
			sampledUp[i] = currentUp;
			lastPoint = currentPoint;
		}
	}

	float adjustedT = 1;
	float3 approximateUp = sampledUp[SPS_BEZIER_SAMPLES - 1];
	for(int i = 1; i < SPS_BEZIER_SAMPLES; i++)
	{
		if (lookingForLength <= sampledLength[i])
		{
			const float fraction = sps_map(lookingForLength, sampledLength[i-1], sampledLength[i], 0, 1);
			adjustedT = lerp(sampledT[i-1], sampledT[i], fraction);
			approximateUp = lerp(sampledUp[i-1], sampledUp[i], fraction);
			break;
		}
	}

	curveLength = totalLength;
	const float t = saturate(adjustedT);
	position = sps_bezier(p0, p1, p2, p3, t);
	forward = sps_normalize(sps_bezierDerivative(p0, p1, p2, p3, t));
	up = sps_nearest_normal(forward, approximateUp);
}
