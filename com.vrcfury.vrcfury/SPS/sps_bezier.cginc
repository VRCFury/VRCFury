#include "sps_utils.cginc"

// https://en.wikipedia.org/wiki/B%C3%A9zier_curve
float3 sps_bezier(float3 p0, float3 p1, float3 p2, float3 p3, float t)
{
	float minT = 1-t;
	return
		minT * minT * minT * p0
		+ 3 * minT * minT * t * p1
		+ 3 * minT * t * t * p2
		+ t * t * t * p3;
}
float3 sps_bezierDerivative(float3 p0, float3 p1, float3 p2, float3 p3, float t)
{
	float minT = 1-t;
	return
		3 * minT * minT * (p1 - p0)
		+ 6 * minT * t * (p2 - p1)
		+ 3 * t * t * (p3 - p2);
}

// https://gamedev.stackexchange.com/questions/105230/points-evenly-spaced-along-a-bezier-curve
// https://gamedev.stackexchange.com/questions/5373/moving-ships-between-two-planets-along-a-bezier-missing-some-equations-for-acce/5427#5427
// https://www.geometrictools.com/Documentation/MovingAlongCurveSpecifiedSpeed.pdf
float sps_bezierFindT(float3 p0, float3 p1, float3 p2, float3 p3, float lookingForLength, out float curveLength)
{
	#define SPS_BEZIER_SAMPLES 50
	float sampledT[SPS_BEZIER_SAMPLES];
	float sampledLength[SPS_BEZIER_SAMPLES];
	float totalLength = 0;
	float3 lastPoint = p0;
	sampledT[0] = 0;
	sampledLength[0] = 0;
	{
		for(int i = 1; i < SPS_BEZIER_SAMPLES-1; i++)
		{
			sampledT[i] = float(i) / (SPS_BEZIER_SAMPLES-1);
			const float3 currentPoint = sps_bezier(p0, p1, p2, p3, sampledT[i]);
			sampledLength[i] = totalLength + length(currentPoint - lastPoint);
			totalLength = sampledLength[i];
			lastPoint = currentPoint;
		}
	}
	sampledT[SPS_BEZIER_SAMPLES-1] = 1;
	sampledLength[SPS_BEZIER_SAMPLES-1] = totalLength + length(p3 - lastPoint);
	totalLength = sampledLength[SPS_BEZIER_SAMPLES-1];

	float adjustedT = 1;
	for(int i = 1; i < SPS_BEZIER_SAMPLES; i++)
	{
		if (lookingForLength <= sampledLength[i])
		{
			adjustedT = sps_map(lookingForLength, sampledLength[i-1], sampledLength[i], sampledT[i-1], sampledT[i]);
			break;
		}
	}

	curveLength = totalLength;
	return saturate(adjustedT);
}
