using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Inspector;

namespace VF.Builder.Haptics {
    public class SpsPatcher {
        public static void patch(Material mat, MutableManager mutableManager) {
            if (!mat.shader) return;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mat, out var guid, out long localId);
            var newShaderName = $"SPSPatched/{guid}";
            var shader = mat.shader;
            var oldShaderPath = AssetDatabase.GetAssetPath(shader);
            var contents = ReadFile(oldShaderPath);
            var pathToSps = AssetDatabase.GUIDToAssetPath("6cf9adf85849489b97305dfeecc74768");
            var newPath = VRCFuryAssetDatabase.GetUniquePath(mutableManager.GetTmpDir(), "SPS Patched " + shader.name, "shader");

            // TODO: Add support for DPS channel 1 and TPS channels
            // TODO: Add animatable toggle

            var state = State.Idle;
            var seenProps = false;
            var lines = new List<string>();
            foreach (var l in contents) {
                var line = l;

                if (line.Contains("Properties") && !seenProps && state == State.Idle) {
                    state = State.LookingForPropsStart;
                    seenProps = true;
                }
                if (line.StartsWith("Shader ")) {
                    lines.Add($"Shader \"{newShaderName}\"");
                    continue;
                }
                
                if (line.Contains(" vert(")) {
                    state = State.LookingForVertStart;
                    lines.AddRange(ReadAndFlatten($"{pathToSps}/sps_funcs.cginc"));
                }

                var includePath = ParseInclude(line, oldShaderPath);
                if (includePath != null) {
                    line = $"#include \"{includePath}\"";
                }

                lines.Add(line);

                if (line.Contains("{") && state == State.LookingForVertStart) {
                    state = State.Idle;
                    lines.Add("sps_apply(v.vertex.xyz, v.normal.xyz, v.color, v.vertexId);");
                }
                if (line.Contains("{") && state == State.LookingForPropsStart) {
                    state = State.Idle;
                    lines.AddRange(ReadAndFlatten($"{pathToSps}/sps_props.cginc"));
                }
            }
            
            VRCFuryAssetDatabase.WithAssetEditing(() => {
                WriteFile(newPath, string.Join("\n", lines));
            });
            VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                AssetDatabase.ImportAsset(newPath, ImportAssetOptions.ForceSynchronousImport);
            });

            var newShader = Shader.Find(newShaderName);
            if (!newShader) {
                throw new VRCFBuilderException("SPS Patched shader failed to compile. Find the shader file and report any errors on the vrcfury discord.\n\n" + newPath);
            }

            mat.shader = newShader;
            VRCFuryEditorUtils.MarkDirty(mat);
        }

        private static string ParseInclude(string line, string filePath) {
            line = line.Trim();
            if (!line.StartsWith("#include")) return null;

            var pattern = @"""(.*)""";
            var m = Regex.Match(line, pattern);
            if (!m.Success) return null;

            var target = ClipRewriter.Join(Path.GetDirectoryName(filePath).Replace('\\', '/'), m.Groups[1].ToString());
            if (!File.Exists(target)) return null;
            return target;
        }

        private static string[] ReadAndFlatten(string path, HashSet<string> included = null) {
            bool isOuter = false;
            if (included == null) {
                included = new HashSet<string>();
                isOuter = true;
            }
            if (included.Contains(path)) return new string[]{};
            included.Add(path);

            var output = new List<string>();
            if (isOuter) {
                output.Add("//////////////////");
                output.Add("// BEGIN SPS PATCH");
                output.Add("//////////////////");
            }
            foreach (var line in ReadFile(path)) {
                var include = ParseInclude(line, path);
                if (include != null) {
                    output.AddRange(ReadAndFlatten(include, included));
                    continue;
                }
                output.Add(line);
            }
            if (isOuter) {
                output.Add("//////////////////");
                output.Add("// END SPS PATCH");
                output.Add("//////////////////");
            }
            return output.ToArray();
        }
        private static string[] ReadFile(string path) {
            StreamReader sr = new StreamReader(path);
            try {
                return sr.ReadToEnd().Split('\n').Select(line => line.TrimEnd()).ToArray();
            } finally {
                sr.Close();
            }
        }
        
        private static void WriteFile(string path, string content) {
            StreamWriter sw = new StreamWriter(path);
            try {
                sw.Write(content);
            } finally {
                sw.Close();
            }
        }

        private enum State {
            Idle,
            LookingForVertStart,
            LookingForPropsStart,
        }
    }
}
