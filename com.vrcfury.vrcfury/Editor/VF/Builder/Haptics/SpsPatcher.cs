using System;
using System.Collections;
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
            try {
                patchUnsafe(mat, mutableManager);
            } catch (Exception e) {
                throw new Exception(
                    "Failed to patch shader with SPS. Report this on the VRCFury discord. Maybe this shader isn't supported yet.\n\n" +
                    mat.shader.name + "\n\n" + e.Message, e);
            }
        }

        public static Regex GetRegex(string pattern) {
            return new Regex(pattern, RegexOptions.Compiled);
        }

        public static void patchUnsafe(Material mat, MutableManager mutableManager) {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mat, out var guid, out long localId);
            var newShaderName = $"SPSPatched/{guid}";
            var shader = mat.shader;
            var pathToSps = AssetDatabase.GUIDToAssetPath("6cf9adf85849489b97305dfeecc74768");
            var newPath = VRCFuryAssetDatabase.GetUniquePath(mutableManager.GetTmpDir(), "SPS Patched " + shader.name, "shader");
            var oldShaderPath = AssetDatabase.GetAssetPath(shader);

            if (oldShaderPath.StartsWith("Resources")) {
                if (shader.name == "Standard") {
                    oldShaderPath = $"{pathToSps}/Standard.shader.orig";
                } else {
                    throw new VRCFBuilderException(
                        "SPS does not yet support this built-in unity shader: " + shader.name);
                }
            }
            var contents = ReadFile(oldShaderPath);

            // TODO: Add support for DPS channel 1 and TPS channels
            // TODO: Add animatable toggle

            void Replace(string pattern, string replacement, int count) {
                var startLen = contents.Length + "" + contents.GetHashCode();
                contents = GetRegex(pattern).Replace(contents, replacement, count);
                if (startLen == contents.Length + "" + contents.GetHashCode()) {
                    throw new VRCFBuilderException("Failed to find " + pattern);
                }
            }

            Replace(
                @"((?:^|\n)\s*Shader\s*"")([^""]*)",
                $"$1{Regex.Escape(newShaderName)}",
                1
            );

            var propertiesContent = ReadAndFlatten($"{pathToSps}/sps_props.cginc");
            Replace(
                @"((?:^|\n)\s*Properties\s*{)",
                $"$1\n{propertiesContent}\n",
                1
            );

            var passNum = 0;
            contents = WithEachPass(contents, pass => {
                passNum++;
                void ex(String msg) {
                    throw new Exception("Pass " + passNum + ": " + msg);
                }

                string oldVertFunction = null;
                var newVertFunction = "spsVert";
                pass = GetRegex(@"(#pragma[ \t]+vertex[ \t]+)([^\s]+)([^\n]*)").Replace(pass, match => {
                    oldVertFunction = match.Groups[2].ToString();
                    return "// " + match.Groups[0].ToString() + "\n"
                        + $"{match.Groups[1]}spsVert{match.Groups[3]}\n";
                }, 1);
                if (oldVertFunction == null) {
                    ex("Failed to find #pragma vertex");
                }

                var flattenedPass = ReadAndFlatten(pass, oldShaderPath, includeLibraryFiles: true);
                
                string returnType;
                string paramType;
                var foundOldVert = GetRegex(Regex.Escape(oldVertFunction) + @"\s*\(\s*([^\s]+)[^\);]*\)\s*\{")
                    .Matches(flattenedPass)
                    .Cast<Match>()
                    .Select(m => {
                        var p = m.Groups[1].ToString();
                        var i = m.Index-1;
                        if (!Char.IsWhiteSpace(flattenedPass[i])) return null;
                        while (Char.IsWhiteSpace(flattenedPass[i])) i--;
                        var endOfReturnType = i + 1;
                        while (!Char.IsWhiteSpace(flattenedPass[i])) i--;
                        var startOfReturnType = i + 1;
                        var r = flattenedPass.Substring(startOfReturnType, endOfReturnType-startOfReturnType);
                        return Tuple.Create(p, r);
                    })
                    .Where(t => t != null)
                    .ToArray();
                if (foundOldVert.Length > 0) {
                    foundOldVert = foundOldVert.Where(m => !m.Item2.ToString().Contains("Simple")).ToArray();
                }
                if (foundOldVert.Length == 0) {
                    ex("Failed to find vertex method: " + oldVertFunction);
                }
                if (foundOldVert.Length > 1) {
                    ex("Found vertex method multiple times: " + oldVertFunction);
                }
                paramType = foundOldVert[0].Item1;
                returnType = foundOldVert[0].Item2;

                // Poi 8
                if (returnType == "VertexOut" && paramType == "#ifndef") {
                    paramType = "appdata";
                }

                string paramBody;
                var foundOldParam = GetRegex(@"struct\s+" + Regex.Escape(paramType) + @"\s[^}]*}")
                    .Match(flattenedPass);
                if (foundOldParam.Success) {
                    paramBody = foundOldParam.Groups[0].ToString();
                } else {
                    ex("Failed to find vertex parameter: " + paramType);
                }

                string FindParam(string keyword) {
                    var match = GetRegex(@"([^ \t]+)[ \t]+([^ \t:]+)[ \t:]+" + Regex.Escape(keyword)).Match(paramBody);
                    if (match.Success) {
                        return match.Groups[2].ToString();
                    }

                    return null;
                }

                var newBody = new List<string>();
                newBody.Add(ReadAndFlatten($"{pathToSps}/sps_funcs.cginc"));
                newBody.Add($"struct SpsInputs : {paramType} {{");
                var vertexParam = FindParam("POSITION");
                if (vertexParam == null) {
                    newBody.Add("  float3 spsPosition : POSITION;");
                    vertexParam = "spsPosition";
                };
                var normalParam = FindParam("NORMAL");
                if (normalParam == null) {
                    newBody.Add("  float3 spsNormal : NORMAL;");
                    normalParam = "spsNormal";
                };
                var vertexIdParam = FindParam("SV_VertexID");
                if (vertexIdParam == null) {
                    newBody.Add("  uint spsVertexId : SV_VertexID;");
                    vertexIdParam = "spsVertexId";
                };
                newBody.Add("};");
                var ret = returnType == "void" ? "" : "return ";
                if (returnType == "void" && oldVertFunction == "vertShadowCaster") {
                    newBody.Add($"{returnType} spsVert(SpsInputs input");
                    newBody.Add(@"
                        , out float4 opos : SV_POSITION
                        #ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
                        , out VertexOutputShadowCaster o
                        #endif
                        #ifdef UNITY_STANDARD_USE_STEREO_SHADOW_OUTPUT_STRUCT
                        , out VertexOutputStereoShadowCaster os
                        #endif
                    ");
                    newBody.Add(") {");
                    newBody.Add($"  sps_apply(input.{vertexParam}.xyz, input.{normalParam}, input.{vertexIdParam});");
                    newBody.Add($"  {ret}{oldVertFunction}(({paramType})input");
                    newBody.Add(@"
                        , opos
                        #ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
                        , o
                        #endif
                        #ifdef UNITY_STANDARD_USE_STEREO_SHADOW_OUTPUT_STRUCT
                        , os
                        #endif
                    ");
                    newBody.Add(");");
                    newBody.Add("}");
                } else {
                    newBody.Add($"{returnType} spsVert(SpsInputs input) {{");
                    newBody.Add($"  sps_apply(input.{vertexParam}.xyz, input.{normalParam}, input.{vertexIdParam});");
                    newBody.Add($"  {ret}{oldVertFunction}(({paramType})input);");
                    newBody.Add("}");
                }

                // We add the body to the end of the pass, since otherwise it may be too early and
                // get inserted before includes that are needed for the base data types
                var endCg = pass.LastIndexOf("ENDCG");
                pass = pass.Substring(0, endCg)
                       + string.Join("\n", newBody)
                       + "\n" + pass.Substring(endCg);

                return pass;
            });

            contents = WithEachInclude(contents, oldShaderPath, includePath => {
                return $"#include \"{includePath}\"";
            });

            VRCFuryAssetDatabase.WithAssetEditing(() => {
                WriteFile(newPath, contents);
            });
            VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                AssetDatabase.ImportAsset(newPath, ImportAssetOptions.ForceSynchronousImport);
            });

            var newShader = Shader.Find(newShaderName);
            if (!newShader) {
                throw new VRCFBuilderException("Patch succeeded, but shader failed to compile. Check the unity log for compile error.\n\n" + newPath);
            }

            mat.shader = newShader;
            VRCFuryEditorUtils.MarkDirty(mat);
        }

        private static string WithEachPass(string content, Func<string, string> with) {
            var seenFirst = false;
            var sinceLast = new List<string>();
            var output = new List<string>();

            void AfterPass() {
                var chunk = string.Join("\n", sinceLast);
                if (seenFirst) chunk = with(chunk);
                output.Add(chunk);
                sinceLast.Clear();
                seenFirst = true;
            }
            var passStartRegex = GetRegex(@"^\s*Pass[\s{]*$");
            foreach (var line in content.Split('\n').Select(line => line.TrimEnd())) {
                if (passStartRegex.Match(line).Success) {
                    AfterPass();
                }
                sinceLast.Add(line);
            }
            AfterPass();

            return string.Join("\n", output);
        }

        private static string WithEachInclude(string contents, string filePath, Func<string, string> with, bool includeLibraryFiles = false) {
            return GetRegex(@"(\s*#include\s"")([^""]+)("")").Replace(contents, match => {
                var before = match.Groups[1].ToString();
                var path = match.Groups[2].ToString();
                var after = match.Groups[3].ToString();
                var fullPath = ClipRewriter.Join(Path.GetDirectoryName(filePath).Replace('\\', '/'), path);
                if (includeLibraryFiles && !path.Contains("..") && !File.Exists(fullPath)) {
                    fullPath = ClipRewriter.Join(EditorApplication.applicationPath.Replace('\\', '/'), "../Data/CGIncludes/" + path);
                }
                if (!File.Exists(fullPath)) return match.Groups[0].ToString();
                return "\n" + with(fullPath) + "\n";
            });
        }

        private static string ReadAndFlatten(string path, HashSet<string> included = null, bool includeLibraryFiles = false) {
            var content = ReadFile(path);
            return ReadAndFlatten(content, path, included, includeLibraryFiles);
        }
        private static string ReadAndFlatten(string content, string path, HashSet<string> included = null, bool includeLibraryFiles = false) {
            bool isOuter = false;
            if (included == null) {
                included = new HashSet<string>();
                isOuter = true;
            }
            if (included.Contains(path)) return "";
            included.Add(path);

            var output = new List<string>();
            content = WithEachInclude(content, path, includePath => {
                return ReadAndFlatten(includePath, included, includeLibraryFiles);
            }, includeLibraryFiles);
            output.Add(content);
            return string.Join("\n", output);
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
    }
}
