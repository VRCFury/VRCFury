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
            var newShaderName = $"Hidden/SPSPatched/{guid}";
            var shader = mat.shader;
            var pathToSps = GetPathToSps();
            var newPath = VRCFuryAssetDatabase.GetUniquePath(mutableManager.GetTmpDir(), "SPS Patched " + shader.name, "shader");
            var contents = ReadFile(shader);

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

            contents = FlattenUsePass(contents);

            var passNum = 0;
            contents = WithEachPass(contents, (pass) => {
                passNum++;
                try {
                    return PatchPass(pass, keepImports, false);
                } catch (Exception e) {
                    throw new Exception($"Failed to patch pass #{passNum}: " + e.Message, e);
                }
            });
            if (passNum == 0) {
                try {
                    contents = PatchPass(contents, keepImports, true);
                } catch (Exception e) {
                    throw new Exception($"Failed to patch surface shader (or no passes found): " + e.Message, e);
                }
            }

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

        private static string FlattenUsePass(string shader) {
            return GetRegex(@"\n[ \t]*UsePass[ \t]+""([^""]+)/([^""/]+)""").Replace(shader, match => {
                var shaderName = match.Groups[1].ToString();
                var passName = match.Groups[2].ToString();
                var includedShader = Shader.Find(shaderName);
                if (!includedShader) {
                    throw new Exception("Failed to find included shader: " + shaderName);
                }
                var includedShaderBody = ReadFile(includedShader);
                string foundPass = null;
                WithEachPass(includedShaderBody, pass => {
                    if (new Regex(@"\n[ \t]*Name[ \t]+""(?i:" + Regex.Escape(passName) + @")""").Match(pass).Success) {
                        foundPass = pass;
                    }

                    return "";
                });
                if (foundPass == null) {
                    throw new Exception($"Failed to find pass named {passName} in shader {shaderName}");
                }

                return "\n" + foundPass + "\n";
            });
        }

        private static string PatchPass(string pass, bool keepImports, bool isSurfaceShader) {
            var newVertFunction = "spsVert";
            var pragmaKeyword = isSurfaceShader ? "surface" : "vertex";
            string oldVertFunction = null;
            var foundPragma = false;
            pass = GetRegex(@"(#pragma[ \t]+" + pragmaKeyword + @"[ \t]+)([^\s]+)([^\n]*)").Replace(pass, match => {
                string newPragma;
                if (isSurfaceShader) {
                    var extraParams = match.Groups[3].ToString();
                    extraParams = GetRegex(@"vertex:(\S+)").Replace(extraParams, vertMatch => {
                        oldVertFunction = vertMatch.Groups[1].ToString();
                        return "vertex:" + newVertFunction;
                    });
                    if (oldVertFunction == null) {
                        newPragma = $"{match.Groups[0]} vertex:{newVertFunction}";
                    } else {
                        newPragma = $"{match.Groups[1]}{match.Groups[2]}{extraParams}";
                    }
                } else {
                    oldVertFunction = match.Groups[2].ToString();
                    newPragma = $"{match.Groups[1]}spsVert{match.Groups[3]}";
                }

                foundPragma = true;
                return $"// {match.Groups[0]}\n{newPragma}\n";
            }, 1);
            if (!foundPragma) {
                throw new Exception($"Failed to find #pragma {pragmaKeyword}");
            }

            var flattenedPass = ReadAndFlattenContent(pass, includeLibraryFiles: true);

            string oldStructType;
            string returnType;
            string newInputParams;
            string newPassParams;
            string mainParamName;
            string searchForOldStruct;
            bool useStructExtends;
            if (oldVertFunction != null) {
                var foundOldVert = GetRegex(Regex.Escape(oldVertFunction) + @"\s*\(([^\);]*)\)\s*\{")
                    .Matches(flattenedPass)
                    .Cast<Match>()
                    .Select(m => {
                        // The reason we search backward for the return type instead of just including it in the regex
                        // is because it makes the regex REALLY SLOW to have wildcards at the start.
                        var ps = m.Groups[1].ToString();
                        var i = m.Index - 1;
                        if (!Char.IsWhiteSpace(flattenedPass[i])) return null;
                        while (Char.IsWhiteSpace(flattenedPass[i])) i--;
                        var endOfReturnType = i + 1;
                        while (!Char.IsWhiteSpace(flattenedPass[i])) i--;
                        var startOfReturnType = i + 1;
                        var r = flattenedPass.Substring(startOfReturnType, endOfReturnType - startOfReturnType);
                        return Tuple.Create(ps, r);
                    })
                    .Where(t => t != null)
                    .ToArray();
                if (foundOldVert.Length > 0) {
                    foundOldVert = foundOldVert.Where(m => !m.Item2.ToString().Contains("Simple")).ToArray();
                }

                if (foundOldVert.Length == 0) {
                    throw new Exception("Failed to find vertex method: " + oldVertFunction);
                }

                if (foundOldVert.Length > 1) {
                    throw new Exception("Found vertex method multiple times: " + oldVertFunction);
                }

                var paramList = foundOldVert[0].Item1;
                returnType = foundOldVert[0].Item2;

                var rewrittenInputParams = RewriteParamList(paramList, rewriteFirstParamTypeTo: "SpsInputs");
                newInputParams = rewrittenInputParams.rewritten;
                oldStructType = rewrittenInputParams.firstParamType;
                mainParamName = rewrittenInputParams.firstParamName;
                var firstParamType = rewrittenInputParams.firstParamType;
                var rewrittenPassParams = RewriteParamList(paramList, stripTypes: true,
                    rewriteFirstParamNameTo: $"({firstParamType}){mainParamName}");
                newPassParams = rewrittenPassParams.rewritten;
                searchForOldStruct = flattenedPass;
                useStructExtends = true;
            } else {
                oldStructType = "appdata_full";
                searchForOldStruct = ReadAndFlattenContent("#include \"UnityCG.cginc\"", includeLibraryFiles: true);
                returnType = "void";
                newInputParams = "inout SpsInputs input";
                mainParamName = "input";
                newPassParams = null;
                useStructExtends = false;
            }

            var oldStructBody = "";
            if (oldStructType != null) {
                var foundOldParam = GetRegex(@"struct\s+" + Regex.Escape(oldStructType) + @"\s*{([^}]*)}")
                    .Match(searchForOldStruct);
                if (foundOldParam.Success) {
                    oldStructBody = foundOldParam.Groups[1].ToString();
                } else {
                    throw new Exception("Failed to find old struct: " + oldStructType);
                }
            }

            var newStructBody = "";
            if (!useStructExtends) newStructBody += oldStructBody;
            string FindParam(string keyword, string defaultName, string defaultType) {
                if (oldStructBody != null) {
                    var match = GetRegex(@"([^ \t]+)[ \t]+([^ \t:]+)[ \t:]+" + Regex.Escape(keyword))
                        .Match(oldStructBody);
                    if (match.Success) {
                        return match.Groups[2].ToString();
                    }
                }
                newStructBody += $"  {defaultType} {defaultName} : {keyword};\n";
                return defaultName;
            }

            var vertexParam = FindParam("POSITION", "spsPosition", "float3");
            var normalParam = FindParam("NORMAL", "spsNormal", "float3");
            var vertexIdParam = FindParam("SV_VertexID", "spsVertexId", "uint");
            var colorParam = FindParam("COLOR", "spsColor", "float4");
            
            var newHeader = new List<string>();
            // liltoon
            newHeader.Add("#define LIL_APP_POSITION");
            newHeader.Add("#define LIL_APP_NORMAL");
            newHeader.Add("#define LIL_APP_VERTEXID");
            newHeader.Add("#define LIL_APP_COLOR");
            
            var newBody = new List<string>();
            if (keepImports) {
                newBody.Add($"#include \"{GetPathToSps()}/sps_funcs.cginc\"");
            } else {
                newBody.Add(ReadAndFlattenPath($"{GetPathToSps()}/sps_funcs.cginc"));
            }
            var extends = useStructExtends ? $" : {oldStructType}" : "";
            newBody.Add($"struct SpsInputs{extends} {{");
            newBody.Add(newStructBody);
            newBody.Add("};");
            newBody.Add($"{returnType} {newVertFunction}({newInputParams}) {{");
            newBody.Add($"  sps_apply({mainParamName}.{vertexParam}.xyz, {mainParamName}.{normalParam}, {mainParamName}.{vertexIdParam}, {mainParamName}.{colorParam});");
            if (newPassParams != null) {
                var ret = returnType == "void" ? "" : "return ";
                newBody.Add($"  {ret}{oldVertFunction}({newPassParams});");
            }
            newBody.Add("}");

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
        }

        public class RewriteParamListOutput {
            public string firstParamName;
            public string firstParamType;
            public string rewritten;
        }
        private static RewriteParamListOutput RewriteParamList(string paramList, string rewriteFirstParamTypeTo = null, string rewriteFirstParamNameTo = null, bool stripTypes = false) {
            string firstParamName = null;
            string firstParamType = null;
            var rewritten = string.Join("\n", paramList.Split('\n').Select(line => {
                if (line.Trim().StartsWith("#")) return line;
                return string.Join(",", line.Split(',').Select(p => {
                    var trimmed = p.Trim();
                    if (trimmed.Length == 0) return p;
                    if (firstParamName == null) {
                        var m = Regex.Match(trimmed, @"(\S+)\s+(\S+)$");
                        firstParamType = m.Groups[1].ToString();
                        firstParamName = m.Groups[2].ToString();
                        if (rewriteFirstParamTypeTo != null) {
                            p = Regex.Replace(p, @"(\S+)(\s+\S+\s*)$", rewriteFirstParamTypeTo+"$2");
                        }
                        if (rewriteFirstParamNameTo != null) {
                            p = Regex.Replace(p, @"(\S+)(\s*)$", rewriteFirstParamNameTo+"$2");
                        }
                    }
                    if (stripTypes) {
                        p = Regex.Replace(p, @":.*", "");
                        p = Regex.Replace(p, @"\S.*\s(\S+)\s*$", "$1");
                    }
                    return p;
                }));
            }));
            return new RewriteParamListOutput() {
                firstParamType = firstParamType,
                firstParamName = firstParamName,
                rewritten = rewritten,
            };
        }

        private static string WithEachPass(string content, Func<string, string> with) {
            var output = "";
            var i = 0;
            while (true) {
                var nextPassStart = GetRegex(@"\n\s*Pass[\s{]*\s*\n").Match(content, i);
                if (nextPassStart.Success) {
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
            // if (includeLibraryFiles && content.Contains("CGPROGRAM")) {
            //     content = "#include \"HLSLSupport.cginc\"\n"
            //         + "#include \"UnityShaderVariables.cginc\"\n"
            //         + content;
            // }
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
