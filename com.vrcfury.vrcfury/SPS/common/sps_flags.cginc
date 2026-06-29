#ifndef SPS_INC_FLAGS
#define SPS_INC_FLAGS

bool sps_has_flag(uint flags, uint flag) {
    return (flags & flag) != 0;
}

void sps_set_flag(inout uint flags, uint flag) {
    flags |= flag;
}

#endif
