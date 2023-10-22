using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Inspector;

namespace VF.Builder.Haptics {
    public class SpsPatcher {
        public static void Patch(Material mat, bool keepImports) {
            if (!mat.shader) return;
            try {
                var renderQueue = mat.renderQueue;
                PatchUnsafe(mat, keepImports);
                mat.renderQueue = renderQueue;
            } catch (Exception e) {
                throw new Exception(
                    "Failed to patch shader with SPS. Report this on the VRCFury discord. Maybe this shader isn't supported yet.\n\n" +
                    mat.shader.name + "\n\n" + e.Message, e);
            }
        }

        private static Regex GetRegex(string pattern) {
            return new Regex(pattern, RegexOptions.Compiled);
        }

        private static void PatchUnsafe(Material mat, bool keepImports) {
            var shader = mat.shader;
            var newShader = PatchUnsafe(shader, keepImports);
            mat.shader = newShader.shader;
            VRCFuryEditorUtils.MarkDirty(mat);
        }

        public class PatchResult {
            public Shader shader;
            public int patchedPasses;
        }
        private static PatchResult PatchUnsafe(Shader shader, bool keepImports, string parentHash = null) {
            var pathToSps = GetPathToSps();
            var contents = ReadFile(shader);

            void Replace(string pattern, string replacement, int count) {
                var startLen = contents.Length + "" + contents.GetHashCode();
                contents = GetRegex(pattern).Replace(contents, replacement, count);
                if (startLen == contents.Length + "" + contents.GetHashCode()) {
                    throw new VRCFBuilderException("Failed to find " + pattern);
                }
            }

            if (parentHash == null) {
                var propertiesContent = ReadAndFlattenPath($"{pathToSps}/sps_props.cginc");
                Replace(
                    @"((?:^|\n)\s*Properties\s*{)",
                    $"$1\n{propertiesContent}\n",
                    1
                );
            }

            string spsMain;
            if (keepImports) {
                spsMain = $"#include \"{pathToSps}/sps_main.cginc\"";
            } else {
                spsMain = ReadAndFlattenPath($"{pathToSps}/sps_main.cginc");
            }
            
            var md5 = MD5.Create();
            var hashContent = contents + spsMain + "3";
            var hashContentBytes = Encoding.UTF8.GetBytes(hashContent);
            var hashBytes = md5.ComputeHash(hashContentBytes);
            var hash = string.Join("", Enumerable.Range(0, hashBytes.Length)
                .Select(i => hashBytes[i].ToString("x2")));

            if (parentHash != null) {
                hash = $"{parentHash}-{hash}";
            }

            string newShaderName;
            if (shader.name.StartsWith("Hidden/Locked/")) {
                // Special case for Poiyomi
                // This prevents Poiyomi from complaining that the mat isn't locked and bailing on the build
                newShaderName = $"Hidden/Locked/SPSPatched/{hash}";
            } else {
                newShaderName = $"Hidden/SPSPatched/{hash}";
            }
            var alreadyExists = Shader.Find(newShaderName);
            if (alreadyExists != null) {
                return new PatchResult {
                    shader = alreadyExists,
                    patchedPasses = 0
                };
            }

            Replace(
                @"((?:^|\n)\s*Shader\s*"")([^""]*)",
                $"$1{Regex.Escape(newShaderName)}",
                1
            );

            var patchedPasses = 0;
            contents = WithEachPass(contents,
                pass => {
                    patchedPasses++;
                    try {
                        return PatchPass(pass, spsMain, false);
                    } catch (Exception e) {
                        throw new Exception($"Failed to patch pass #{patchedPasses}: " + e.Message, e);
                    }
                },
                rest => {
                    if (GetRegex(@"\n[ \t]*#pragma[ \t]+surface").IsMatch(rest)) {
                        patchedPasses++;
                        try {
                            return PatchPass(rest, spsMain, true);
                        } catch (Exception e) {
                            throw new Exception($"Failed to patch surface shader: " + e.Message, e);
                        }
                    }
                    return rest;
                }
            );
            var childShaders = new Dictionary<Shader, Shader>();
            contents = GetRegex(@"\n[ \t]*UsePass[ \t]+""([^""]+)/([^""/]+)""").Replace(contents, match => {
                var shaderName = match.Groups[1].ToString();
                var passName = match.Groups[2].ToString();
                var includedShader = Shader.Find(shaderName);
                if (!includedShader) {
                    throw new Exception("Failed to find included shader: " + shaderName);
                }

                if (!childShaders.TryGetValue(includedShader, out var rewrittenIncludedShader)) {
                    var output = PatchUnsafe(includedShader, keepImports, hash);
                    patchedPasses += output.patchedPasses;
                    rewrittenIncludedShader = output.shader;
                    childShaders[includedShader] = rewrittenIncludedShader;
                }
                
                return $"\nUsePass \"{rewrittenIncludedShader.name}/{passName}\"\n";
            });
            if (patchedPasses == 0) {
                throw new Exception($"No passes found");
            }

            var newPathDir = $"{TmpFilePackage.GetPath()}/SPS";
            var newPath = $"{newPathDir}/{hash}.shader";
            VRCFuryAssetDatabase.WithAssetEditing(() => {
                Directory.CreateDirectory(newPathDir);
                WriteFile(newPath, contents);
            });
            VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                AssetDatabase.ImportAsset(newPath, ImportAssetOptions.ForceSynchronousImport);
            });

            var newShader = Shader.Find(newShaderName);
            if (!newShader) {
                throw new VRCFBuilderException("Patch succeeded, but shader failed to compile. Check the unity log for compile error.\n\n" + newPath);
            }

            return new PatchResult {
                shader = newShader,
                patchedPasses = patchedPasses
            };
        }

        private static string PatchPass(string pass, string spsMain, bool isSurfaceShader) {
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
                if (foundOldVert.Length > 1) {
                    foundOldVert = foundOldVert.Distinct().ToArray();
                }
                // Special case for Standard
                if (foundOldVert.Length > 1) {
                    foundOldVert = foundOldVert.Where(m => !m.Item2.Contains("Simple")).ToArray();
                }
                // Special case for Fast Fur
                if (foundOldVert.Length > 1) {
                    if (flattenedPass.Contains("FUR_SKIN_LAYER")) {
                        var skinLayerDefined = flattenedPass.Contains("#define FUR_SKIN_LAYER");
                        foundOldVert = foundOldVert.Where(m => m.Item2 == (skinLayerDefined ? "fragInput" : "hullGeomInput")).ToArray();
                    }
                }

                if (foundOldVert.Length == 0) {
                    throw new Exception("Failed to find vertex method: " + oldVertFunction);
                }

                if (foundOldVert.Length > 1) {
                    throw new Exception("Found vertex method multiple times: "
                                        + oldVertFunction
                                        + "\n"
                                        + string.Join("\n", foundOldVert.Select(f => f.Item2 + " " + f.Item1)));
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
                var match = GetRegex(@"([^ \t]+)[ \t]+([^ \t:]+)[ \t:]+" + Regex.Escape(keyword))
                    .Match(oldStructBody);
                if (match.Success) {
                    return match.Groups[2].ToString();
                }
                newStructBody += $"  {defaultType} {defaultName} : {keyword};\n";
                return defaultName;
            }

            var vertexParam = FindParam("POSITION", "spsPosition", "float3");
            var normalParam = FindParam("NORMAL", "spsNormal", "float3");
            var vertexIdParam = FindParam("SV_VertexID", "spsVertexId", "uint");
            var colorParam = FindParam("COLOR", "spsColor", "float4");
            
            var newHeader = new List<string>();
            // Special case for liltoon
            newHeader.Add("#define LIL_APP_POSITION");
            newHeader.Add("#define LIL_APP_NORMAL");
            newHeader.Add("#define LIL_APP_VERTEXID");
            newHeader.Add("#define LIL_APP_COLOR");
            
            var newBody = new List<string>();
            newBody.Add(spsMain);
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

        private static string WithEachPass(string content, Func<string, string> withPass, Func<string, string> withRest) {
            var output = "";
            var lastPassEnd = 0;
            var processedPasses = new List<string>();
            while (true) {
                var nextPassStart = GetRegex(@"\n\s*Pass[\s{]*\s*\n").Match(content, lastPassEnd);
                if (nextPassStart.Success) {
                    var start = nextPassStart.Index;
                    output += content.Substring(lastPassEnd, start - lastPassEnd);
                    var end = IndexOfEndOfNextContext(content, start);
                    var oldPass = content.Substring(start, end - start);
                    var newPass = withPass(oldPass);
                    output += $"__PASS_{processedPasses.Count}__";
                    processedPasses.Add(newPass);
                    lastPassEnd = end;
                } else {
                    output += content.Substring(lastPassEnd);
                    break;
                }
            }

            output = withRest(output);
            for (var i = 0; i < processedPasses.Count; i++) {
                output = output.Replace($"__PASS_{i}__", processedPasses[i]);
            }

            return output;
        }

        private static int IndexOfEndOfNextContext(string str, int start) {
            var bracketLevel = 0;
            var inString = false;
            var inStringEscape = false;
            var inLineComment = false;
            var inBlockComment = false;
            for (var i = start; i < str.Length; i++) {
                var c = str[i];
                if (inLineComment) {
                    if (c == '\n') {
                        inLineComment = false;
                    }
                    continue;
                }
                if (inBlockComment) {
                    if (c == '*' && i != str.Length - 1 && str[i + 1] == '/') {
                        inBlockComment = false;
                    }
                    continue;
                }
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

                if (c == '/' && i != str.Length - 1 && str[i + 1] == '*') {
                    inBlockComment = true;
                    i++;
                } else if (c == '/' && i != str.Length - 1 && str[i + 1] == '/') {
                    inLineComment = true;
                    i++;
                } else if (c == '{') {
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
            return GetRegex(@"(?:^|\n)(\s*#include\s"")([^""]+)("")").Replace(contents, match => {
                var before = match.Groups[1].ToString();
                var path = match.Groups[2].ToString();
                var after = match.Groups[3].ToString();
                if (path.StartsWith("/")) path = path.Substring(1);
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
                throw new Exception("Failed to find source file for the shader");
            }

            if (path.StartsWith("Resources") || path.StartsWith("Library")) {
                if (shader.name == "Standard") {
                    path = $"{GetPathToSps()}/Standard.shader.orig";
                } else if (shader.name == "Standard (Specular setup)") {
                    path = $"{GetPathToSps()}/StandardSpecular.shader.orig";
                } else if (shader.name.Contains("Error")) {
                    throw new VRCFBuilderException(
                        "This is an error shader. Please verify that the base material actually loads.");
                } else {
                    throw new VRCFBuilderException(
                        "SPS does not yet support this built-in unity shader.");
                }
            }

            return ReadFile(path);
        }
        private static string ReadFile(string path) {
            string content;
            if (path.EndsWith("lilcontainer")) {
                var lilShaderContainer = ReflectionUtils.GetTypeFromAnyAssembly("lilToon.lilShaderContainer");
                var unpackMethod = lilShaderContainer.GetMethods().First(m => m.Name == "UnpackContainer" && m.GetParameters().Length == 2);
                content = (string)ReflectionUtils.CallWithOptionalParams(unpackMethod, null, path);
                var shaderLibsPath = (string)lilShaderContainer.GetField("shaderLibsPath", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                content = content.Replace("\"Includes", "\"" + shaderLibsPath);
            } else {
                StreamReader sr = new StreamReader(path);
                try {
                    content = sr.ReadToEnd();
                } finally {
                    sr.Close();
                }
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
