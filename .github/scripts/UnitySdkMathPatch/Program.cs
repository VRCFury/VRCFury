using System;
using Mono.Cecil;

var path = args[0];
using var assembly = ModuleDefinition.ReadModule(path, new ReaderParameters { InMemory = true });
var count = 0;
foreach (var member in assembly.GetMemberReferences()) {
    if (member is not MethodReference { Name: "op_Implicit", Parameters.Count: 1 } conversion) continue;
    var engineTypeName = conversion.DeclaringType.FullName switch {
        "Unity.Mathematics.float2" => "UnityEngine.Vector2",
        "Unity.Mathematics.float3" => "UnityEngine.Vector3",
        "Unity.Mathematics.float4x4" => "UnityEngine.Matrix4x4",
        "Unity.Mathematics.quaternion" => "UnityEngine.Quaternion",
        _ => null
    };
    var parameter = conversion.Parameters[0].ParameterType;
    var engineType = parameter.FullName == engineTypeName ? parameter : conversion.ReturnType;
    if (engineType.FullName != engineTypeName) continue;

    // When Mathematics became built-in in Unity 6.5, the conversion operators moved to UnityEngine's types.
    conversion.DeclaringType = engineType;
    count++;
}

if (count == 0) throw new InvalidOperationException("No SDK math conversions were found to patch.");
assembly.Write(path);
Console.WriteLine($"Updated {count} SDK math conversion references for Unity 6.5 and newer.");
