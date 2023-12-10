#ifndef SPS_PLUS
#define SPS_PLUS

float _SPS_Plus_Enabled;
float _SPS_Plus_Ring;
float _SPS_Plus_Hole;

void sps_plus_search(
    out float distance,
    out int type
) {
    distance = 999;
    type = SPS_TYPE_INVALID;
    
    float maxVal = 0;
    if (_SPS_Plus_Ring > maxVal) { maxVal = _SPS_Plus_Ring; type = SPS_TYPE_RING_TWOWAY; }
    if (_SPS_Plus_Hole == maxVal) { type = SPS_TYPE_RING_ONEWAY; }
    if (_SPS_Plus_Hole > maxVal) { maxVal = _SPS_Plus_Hole; type = SPS_TYPE_HOLE; }

    if (maxVal > 0) {
        distance = (1 - maxVal) * 3;
    }
}

#endif
