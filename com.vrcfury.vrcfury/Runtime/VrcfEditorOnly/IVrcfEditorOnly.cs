using VRC.SDKBase;

namespace VF.VrcfEditorOnly {
    // This is here so we can be compatible with /either/ the whitelist patch, OR the new vrcsdk IEditorOnly
    internal interface IVrcfEditorOnly
#if VRC_NEW_HOOK_API
    : IEditorOnly
#endif
    {
        
    }
}
