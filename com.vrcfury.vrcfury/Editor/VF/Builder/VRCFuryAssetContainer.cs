using UnityEditor;
using UnityEngine;

namespace VF.Builder
{
    /// <summary>
    /// Unity 2019 has a bug in which creating assets with preview thumbnails early in Play Mode initialization causes
    /// an editor crash (it also causes a lot of overhead otherwise). To workaround this bug, when saving assets in
    /// Play Mode, we put them into this container asset, which does not have a thumbnail.
    /// </summary>
    [PreferBinarySerialization]
    internal class VRCFuryAssetContainer : ScriptableObject
    {
        
    }
}