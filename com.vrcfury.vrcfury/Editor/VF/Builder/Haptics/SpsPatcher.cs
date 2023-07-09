using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
            var pathToOldShaderDir = Path.GetDirectoryName(oldShaderPath).Replace("\\", "/");
            
            // TODO: Flatten includes to make poiyomi lockdown happy
            // TODO: Add support for DPS channel 1 and TPS channels
            // TODO: Add animatable toggle
            // TODO: Make scale fix work

            var state = State.Idle;
            var seenProps = false;
            var lines = new List<string>();
            foreach (var l in contents.Split('\n')) {
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
                    lines.Add($"#include \"{pathToSps}/sps_funcs.cginc\"");
                }

                if (line.Contains("#include")) {
                    var pattern = @"#include ""(.*)""";
                    var m = Regex.Match(line, pattern);
                    if (m.Success) {
                        var includePath = $"{pathToOldShaderDir}/{m.Groups[1]}";
                        if (File.Exists(includePath)) {
                            line = $"#include \"{includePath}\"";
                        }
                    }
                }

                lines.Add(line.TrimEnd());

                if (line.Contains("{") && state == State.LookingForVertStart) {
                    state = State.Idle;
                    lines.Add($"#include \"{pathToSps}/sps_vert.cginc\"");
                }
                if (line.Contains("{") && state == State.LookingForPropsStart) {
                    state = State.Idle;
                    lines.AddRange(ReadFile($"{pathToSps}/sps_props.cginc").Split('\n'));
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

        private static string ReadFile(string path) {
            StreamReader sr = new StreamReader(path);
            try {
                return sr.ReadToEnd();
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
