using System;
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
        public static void patch(Material mat, MutableManager mutableManager, bool keepImports) {
            if (!mat.shader) return;
            try {
                patchUnsafe(mat, mutableManager, keepImports);
            } catch (Exception e) {
                throw new Exception(
                    "Failed to patch shader with SPS. Report this on the VRCFury discord. Maybe this shader isn't supported yet.\n\n" +
                    mat.shader.name + "\n\n" + e.Message, e);
            }
        }

        public static Regex GetRegex(string pattern) {
            return new Regex(pattern, RegexOptions.Compiled);
        }

        public static void patchUnsafe(Material mat, MutableManager mutableManager, bool keepImports) {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mat, out var guid, out long localId);
            var newShaderName = $"SPSPatched/{guid}";
            var shader = mat.shader;
            var pathToSps = GetPathToSps();
            var newPath = VRCFuryAssetDatabase.GetUniquePath(mutableManager.GetTmpDir(), "SPS Patched " + shader.name, "shader");
            var contents = ReadFile(shader);

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

            var propertiesContent = ReadAndFlattenPath($"{pathToSps}/sps_props.cginc");
            Replace(
                @"((?:^|\n)\s*Properties\s*{)",
                $"$1\n{propertiesContent}\n",
                1
            );
            
            contents = GetRegex(@"\n[ \t]*UsePass[ \t]+""([^""]+)/([^""/]+)""").Replace(contents, match => {
                var shaderName = match.Groups[1].ToString();
                var passName = match.Groups[2].ToString();
                var includedShader = Shader.Find(shaderName);
                if (!includedShader) {
                    throw new Exception("Failed to find included shader: " + shaderName);
                }
                var includedShaderBody = ReadFile(includedShader);
                string foundPass = null;
                WithEachPass(includedShaderBody, pass => {
                    if (GetRegex(@"\n[ \t]*Name[ \t]+""" + Regex.Escape(passName) + @"""").Match(pass).Success) {
                        foundPass = pass;
                    }

                    return "";
                });
                if (foundPass == null) {
                    throw new Exception($"Failed to find pass named {passName} in shader {shaderName}");
                }

                return "\n" + foundPass + "\n";
            });

            var passNum = 0;
            contents = WithEachPass(contents, (string pass) => {
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

                var flattenedPass = ReadAndFlattenContent(pass, includeLibraryFiles: true);
                
                string returnType;
                string paramType;
                var foundOldVert = GetRegex(Regex.Escape(oldVertFunction) + @"\s*\(\s*([^\s]+)[^\);]*\)\s*\{")
                    .Matches(flattenedPass)
                    .Cast<Match>()
                    .Select(m => {
                        // The reason we search backward for the return type instead of just including it in the regex
                        // is because it makes the regex REALLY SLOW to have wildcards at the start.
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

                var newHeader = new List<string>();
                newHeader.Add("#define LIL_APP_POSITION");
                newHeader.Add("#define LIL_APP_NORMAL");
                newHeader.Add("#define LIL_APP_VERTEXID");
                newHeader.Add("#define LIL_APP_COLOR");

                var newBody = new List<string>();
                if (keepImports) {
                    newBody.Add($"#include \"{pathToSps}/sps_funcs.cginc\"");
                } else {
                    newBody.Add(ReadAndFlattenPath($"{pathToSps}/sps_funcs.cginc"));
                }
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
                var colorParam = FindParam("COLOR");
                if (colorParam == null) {
                    newBody.Add("  float4 spsColor : COLOR;");
                    colorParam = "spsColor";
                };
                newBody.Add("};");
                var ret = returnType == "void" ? "" : "return ";
                if (returnType == "void" && oldVertFunction == "vertShadowCaster") {
                    newBody.Add($"{returnType} {newVertFunction}(SpsInputs input");
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
                    newBody.Add($"  sps_apply(input.{vertexParam}.xyz, input.{normalParam}, input.{vertexIdParam}, input.{colorParam});");
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
                    newBody.Add($"{returnType} {newVertFunction}(SpsInputs input) {{");
                    newBody.Add($"  sps_apply(input.{vertexParam}.xyz, input.{normalParam}, input.{vertexIdParam}, input.{colorParam});");
                    newBody.Add($"  {ret}{oldVertFunction}(({paramType})input);");
                    newBody.Add("}");
                }
                
                // We add the body to the end of the pass, since otherwise it may be too early and
                // get inserted before includes that are needed for the base data types
                var startCg = pass.IndexOf("CGPROGRAM");
                if (startCg < 0) startCg = pass.IndexOf("HLSLPROGRAM");
                if (startCg > 0) startCg = pass.IndexOf("\n", startCg);
                if (startCg < 0) throw new Exception("Failed to find CGPROGRAM");
                pass = pass.Substring(0, startCg) + "\n"
                       + string.Join("\n", newHeader)
                       + "\n" + pass.Substring(startCg);

                // We add the body to the end of the pass, since otherwise it may be too early and
                // get inserted before includes that are needed for the base data types
                var endCg = pass.LastIndexOf("ENDCG");
                if (endCg < 0) endCg = pass.LastIndexOf("ENDHLSL");
                if (endCg < 0) throw new Exception("Failed to find ENDCG");
                pass = pass.Substring(0, endCg) + "\n"
                       + string.Join("\n", newBody)
                       + "\n" + pass.Substring(endCg);

                return pass;
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
            var output = "";
            var foundPasses = 0;
            var i = 0;
            while (true) {
                var nextPassStart = GetRegex(@"\n\s*Pass[\s{]*\s*\n").Match(content, i);
                if (nextPassStart.Success) {
                    foundPasses++;
                    var start = nextPassStart.Index;
                    output += content.Substring(i, start - i);
                    var end = IndexOfEndOfNextContext(content, start);
                    var oldPass = content.Substring(start, end - start);
                    var newPass = with(oldPass);
                    output += newPass;
                    i = end;
                } else {
                    output += content.Substring(i);
                    break;
                }
            }

            if (foundPasses == 0) {
                throw new Exception("Didn't find any passes");
            }
            return output;
        }

        private static int IndexOfEndOfNextContext(string str, int start) {
            var bracketLevel = 0;
            var inString = false;
            var inStringEscape = false;
            for (var i = start; i < str.Length; i++) {
                var c = str[i];
                if (inString) {
                    if (inStringEscape) {
                        inStringEscape = false;
                        // skip it, this is a literal
                    } else if (c == '\\') {
                        inStringEscape = true;
                    } else if (c == '"') {
                        inString = false;
                    }
                    continue;
                }
                if (c == '{') {
                    bracketLevel++;
                } else if (c == '}') {
                    bracketLevel--;
                    if (bracketLevel == 0) return i+1;
                } else if (c == '"') {
                    inString = true;
                }
            }
            throw new Exception("Failed to find matching closing bracket");
        }

        private static string WithEachInclude(string contents, string filePath, Func<string, string> with, bool includeLibraryFiles = false) {
            return GetRegex(@"(\s*#include\s"")([^""]+)("")").Replace(contents, match => {
                var before = match.Groups[1].ToString();
                var path = match.Groups[2].ToString();
                var after = match.Groups[3].ToString();
                string fullPath;
                if (filePath == null) {
                    fullPath = path;
                } else {
                    fullPath = ClipRewriter.Join(Path.GetDirectoryName(filePath).Replace('\\', '/'), path);
                }
                if (includeLibraryFiles && !path.Contains("..") && !File.Exists(fullPath)) {
                    fullPath = ClipRewriter.Join(EditorApplication.applicationPath.Replace('\\', '/'), "../Data/CGIncludes/" + path);
                }
                if (!File.Exists(fullPath)) return match.Groups[0].ToString();
                return "\n" + with(fullPath) + "\n";
            });
        }

        private static string ReadAndFlattenPath(string path, HashSet<string> included = null, bool includeLibraryFiles = false) {
            if (included == null) {
                included = new HashSet<string>();
            }
            if (included.Contains(path)) return "";
            included.Add(path);
            var content = ReadFile(path);
            return ReadAndFlattenContent(content, included, includeLibraryFiles);
        }
        private static string ReadAndFlattenContent(string content, HashSet<string> included = null, bool includeLibraryFiles = false) {
            var output = new List<string>();
            content = WithEachInclude(content, null, includePath => {
                return ReadAndFlattenPath(includePath, included, includeLibraryFiles);
            }, includeLibraryFiles);
            output.Add(content);
            return string.Join("\n", output);
        }

        private static string GetPathToSps() {
            return AssetDatabase.GUIDToAssetPath("6cf9adf85849489b97305dfeecc74768");
        }
        private static string ReadFile(Shader shader) {
            var path = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrWhiteSpace(path)) {
                throw new Exception("Failed to find file for shader: " + shader.name);
            }

            if (path.StartsWith("Resources")) {
                if (shader.name == "Standard") {
                    path = $"{GetPathToSps()}/Standard.shader.orig";
                } else {
                    throw new VRCFBuilderException(
                        "SPS does not yet support this built-in unity shader: " + shader.name);
                }
            }

            return ReadFile(path);
        }
        private static string ReadFile(string path) {
            StreamReader sr = new StreamReader(path);
            string content;
            try {
                content = sr.ReadToEnd();
            } finally {
                sr.Close();
            }
            content = WithEachInclude(content, path, includePath => {
                return $"#include \"{includePath}\"";
            });
            content = content.Replace("\r", "");
            return content;
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
