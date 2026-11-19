// Standard evaluates fog per pixel on desktop. All three fog modes use the
// same clip-space depth from the vertex shader, which already runs SPS.
#if defined(SHADER_API_MOBILE)
    // Mobile Standard calculates the mode-specific fog factor per vertex.
    #pragma multi_compile_fog
#else
    #pragma multi_compile_fragment __ FOG_LINEAR FOG_EXP FOG_EXP2
    #if defined(SHADER_STAGE_VERTEX) && !defined(FOG_LINEAR) && !defined(FOG_EXP) && !defined(FOG_EXP2)
        #define FOG_LINEAR 1
    #endif
#endif
