using System;
using System.Reflection;
using UnityEditor;

namespace VF {
    [InitializeOnLoad]
    public class VCCRepoAdder {
        static VCCRepoAdder() {
            var reposClass = ReflectionUtils.GetTypeFromAnyAssembly("VRC.PackageManagement.Core.Repos");
            if (reposClass == null) return;
            var addMethod = reposClass.GetMethod("AddRepo", BindingFlags.Static | BindingFlags.Public);
            if (addMethod == null) return;
            ReflectionUtils.CallWithOptionalParams(addMethod, null, new Uri("https://vcc.vrcfury.com"));
        }
    }
}
