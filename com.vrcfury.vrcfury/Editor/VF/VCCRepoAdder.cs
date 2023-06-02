using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VF {
    [InitializeOnLoad]
    public class VCCRepoAdder {
        static VCCRepoAdder() {
            Debug.LogWarning("Adding VCC Repo");
            var reposClass = ReflectionUtils.GetTypeFromAnyAssembly("VRC.PackageManagement.Core.Repos");
            if (reposClass == null) return;
            var addMethod = reposClass.GetMethod("AddRepo", BindingFlags.Static | BindingFlags.Public);
            if (addMethod == null) return;
            addMethod.Invoke(null, new object[] { new Uri("https://vcc.vrcfury.com") });
        }
    }
}
