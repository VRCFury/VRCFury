using System.Reflection;
using UdonSharp;
using VF.Utils;
using VRC.Udon;
using VRC.Udon.Editor.ProgramSources;

namespace VF.Hooks.UdonCleaner {
    internal abstract class UdonCleanerReflection : ReflectionHelper {
        public static readonly FieldInfo programSource = typeof(UdonBehaviour).VFField("programSource");
        public static readonly FieldInfo serializedProgramAsset = typeof(UdonBehaviour).VFField("serializedProgramAsset");
        public static readonly FieldInfo serializedUdonProgramAsset = typeof(UdonProgramAsset).VFField("serializedUdonProgramAsset");
        public delegate void ClearProgramAssetCache_();
        public static readonly ClearProgramAssetCache_ ClearProgramAssetCache = typeof(UdonSharpProgramAsset)
            .GetMatchingDelegate<ClearProgramAssetCache_>("ClearProgramAssetCache");
    }
}
