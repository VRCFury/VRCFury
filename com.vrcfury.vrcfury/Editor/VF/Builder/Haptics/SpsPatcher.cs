using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Inspector;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class SpsPatcher {
        private const string HashBuster = "13";
        
        public static void Patch(Material mat, bool keepImports) {
            if (!mat.shader) return;
            try {
                var renderQueue = mat.renderQueue;
                PatchUnsafe(mat, keepImports);
                mat.renderQueue = renderQueue;
            } catch(SpsErrorMatException e) {
                var msg = $"Your avatar is using a material ({mat.name}) that couldn't load properly.\n\n" +
                          $"The shader used by this material may be broken or out of date in your project. Ask the creator of this asset what shader and version should be used.";
                if (e.Message != null) {
                    msg += "\n\n" + e.Message;
                }
                throw new SneakyException(msg);
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
            public int patchedPrograms;
        }
        private static PatchResult PatchUnsafe(Shader shader, bool keepImports, string parentHash = null) {
            var testMat = new Material(shader);
            VrcfObjectFactory.Register(testMat);
            for (int i = 0; i < testMat.passCount; i++) {
                ShaderUtil.CompilePass(testMat, i, true);
            }

            if (ShaderUtil.ShaderHasError(shader)) {
                var error = ShaderUtil
                    .GetShaderMessages(shader)
                    .First(x => x.severity == ShaderCompilerMessageSeverity.Error);
                throw new SpsErrorMatException($"The vanilla shader at {AssetDatabase.GetAssetPath(shader)} has an internal error:\n\n" + error.file+":"+error.line+" "+error.message);
            }
            
            var pathToSps = GetPathToSps();
            var (contents,isBuiltIn) = ReadFile(shader);

            void Replace(string pattern, string replacement, int count) {
                var startLen = contents.Length + "" + contents.GetHashCode();
                contents = GetRegex(pattern).Replace(contents, replacement, count);
                if (startLen == contents.Length + "" + contents.GetHashCode()) {
                    throw new VRCFBuilderException("Failed to find " + pattern);
                }
            }

            if (contents.Contains("_SPS_Bake")) {
                throw new Exception("Shader appears to already be patched, which should be impossible");
            }

            if (parentHash == null) {
                var propertiesContent = ReadAndFlattenPath($"{pathToSps}/sps_props.cginc");
                Replace(
                    @"((?:^|\n)\s*Properties\s*{)",
                    $"$1\n{propertiesContent}\n",
                    1
                );
                contents = GetRegex(@"\n\s+CustomEditor [^\n]+").Replace(contents, "");
            }

            string spsMain;
            if (keepImports) {
                spsMain = $"#include \"{pathToSps}/sps_main.cginc\"";
            } else {
                spsMain = ReadAndFlattenPath($"{pathToSps}/sps_main.cginc");
            }
            
            var md5 = MD5.Create();
            var hashContent = contents + spsMain + HashBuster;
            if (isBuiltIn) hashContent += Application.unityVersion;
            var hashContentBytes = Encoding.UTF8.GetBytes(hashContent);
            var hashBytes = md5.ComputeHash(hashContentBytes);
            var hash = Enumerable.Range(0, hashBytes.Length)
                .Select(i => hashBytes[i].ToString("x2"))
                .Join("");

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
            if (alreadyExists != null && !ShaderUtil.ShaderHasError(alreadyExists)) {
                return new PatchResult {
                    shader = alreadyExists,
                    patchedPrograms = 0
                };
            }

            Replace(
                @"((?:^|\n)\s*Shader\s*"")([^""]*)",
                $"$1{Regex.Escape(newShaderName)}",
                1
            );

            var cgIncludes = "";
            WithEachCgInclude(contents, include => {
                cgIncludes += include + "\n";
            });

            var patchedPrograms = 0;
            var passNum = 0;
            contents = WithEachPass(contents,
                pass => {
                    passNum++;
                    try {
                        var (newPass, num) = PatchPass(pass, spsMain, cgIncludes, false);
                        patchedPrograms += num;
                        return newPass;
                    } catch (Exception e) {
                        throw new Exception($"Failed to patch pass #{passNum}: " + e.Message, e);
                    }
                },
                rest => {
                    try {
                        var (newRest, num) = PatchPass(rest, spsMain, cgIncludes, true);
                        patchedPrograms += num;
                        return newRest;
                    } catch (Exception e) {
                        throw new Exception($"Failed to patch non-pass segment: " + e.Message, e);
                    }
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
                    patchedPrograms += output.patchedPrograms;
                    rewrittenIncludedShader = output.shader;
                    childShaders[includedShader] = rewrittenIncludedShader;
                }
                
                return $"\nUsePass \"{rewrittenIncludedShader.name}/{passName}\"\n";
            });
            if (patchedPrograms == 0) {
                throw new Exception($"No programs found");
            }

            var newPathDir = $"{TmpFilePackage.GetPath()}/SPS";
            var newPath = $"{newPathDir}/{hash}.shader";
            VRCFuryAssetDatabase.WithAssetEditing(() => {
                VRCFuryAssetDatabase.CreateFolder(newPathDir);
                WriteFile(newPath, contents);
            });
            VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                AssetDatabase.ImportAsset(newPath, ImportAssetOptions.ForceSynchronousImport);
            });

            var newShader = Shader.Find(newShaderName);
            if (!newShader) {
                throw new VRCFBuilderException("Patch succeeded, but shader failed to generate. Check the unity log for compile error?\n\n" + newPath);
            }

            if (ShaderUtil.ShaderHasError(newShader)) {
                var error = ShaderUtil
                    .GetShaderMessages(newShader)
                    .First(x => x.severity == ShaderCompilerMessageSeverity.Error);
                throw new VRCFBuilderException("Patch succeeded, but shader failed to compile.\n\n" + error.file+":"+error.line+" "+error.message);
            }

            return new PatchResult {
                shader = newShader,
                patchedPrograms = patchedPrograms
            };
        }

        private static (string,int) PatchPass(string pass, string spsMain, string cgIncludes, bool isSurfaceShader) {
            if (!isSurfaceShader) {
                // If lightmode is unset (the default of "Always"), set it to ForwardBase
                // so that we actually receive light data
                if (!pass.Contains("\"LightMode\"")) {
                    pass = "\n    Tags { \"LightMode\" = \"ForwardBase\" }\n" + pass;
                }
            }

            var patchedPrograms = 0;
            pass = WithEachProgram(pass, (program, isCgProgram) => {
                patchedPrograms++;
                try {
                    return PatchProgram(program, isCgProgram, spsMain, cgIncludes, isSurfaceShader);
                } catch (Exception e) {
                    throw new Exception($"Failed to patch program #{patchedPrograms}: " + e.Message, e);
                }
            });

            return (pass, patchedPrograms);
        }

        private static string PatchProgram(string program, bool isCgProgram, string spsMain, string cgIncludes, bool isSurfaceShader) {
            var newVertFunction = "spsVert";
            var pragmaKeyword = isSurfaceShader ? "surface" : "vertex";
            string oldVertFunction = null;
            var foundPragma = false;
            program = GetRegex(@"(#pragma[ \t]+" + pragmaKeyword + @"[ \t]+)([^\s]+)([^\n]*)").Replace(program, match => {
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

            var flattenedProgram = program;
            if (isCgProgram) {
                var autoCgHeader = "";
                autoCgHeader += "#include \"HLSLSupport.cginc\"\n";
                autoCgHeader += "#include \"UnityShaderVariables.cginc\"\n";
                if (isSurfaceShader) {
                    autoCgHeader += "#include \"Lighting.cginc\"\n";
                }
                autoCgHeader += cgIncludes + "\n";
                flattenedProgram = autoCgHeader + flattenedProgram;
            }
            flattenedProgram = ReadAndFlattenContent(flattenedProgram, includeLibraryFiles: true);

            string oldStructType;
            string returnType;
            string newInputParams;
            string newPassParams;
            string mainParamName;
            bool useStructExtends = !isSurfaceShader;
            if (oldVertFunction != null) {
                var foundOldVert = GetRegex(
                        Regex.Escape(oldVertFunction)
                        + @"\s*" // whitespace before param list
                        + @"\(" // start param list
                        + "(" // param list
                            + "(" // Repeating params or preprocessor directive
                                + @"([^#\);]*)"
                                + @"|"
                                + @"(#[^\n]*\n)"
                            + ")*"
                        + ")"
                        + @"\)\s*\{" // end param list and start function bracket
                    )
                    .Matches(flattenedProgram)
                    .Cast<Match>()
                    .Select(m => {
                        // The reason we search backward for the return type instead of just including it in the regex
                        // is because it makes the regex REALLY SLOW to have wildcards at the start.
                        var ps = m.Groups[1].ToString();
                        var i = m.Index - 1;
                        if (!Char.IsWhiteSpace(flattenedProgram[i])) return null;
                        while (Char.IsWhiteSpace(flattenedProgram[i])) i--;
                        var endOfReturnType = i + 1;
                        while (!Char.IsWhiteSpace(flattenedProgram[i])) i--;
                        var startOfReturnType = i + 1;
                        var r = flattenedProgram.Substring(startOfReturnType, endOfReturnType - startOfReturnType);
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
                    if (flattenedProgram.Contains("FUR_SKIN_LAYER")) {
                        var skinLayerDefined = flattenedProgram.Contains("#define FUR_SKIN_LAYER");
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
                                        + foundOldVert.Select(f => f.Item2 + " " + f.Item1).Join('\n')
                    );
                }

                var paramList = foundOldVert[0].Item1;
                returnType = foundOldVert[0].Item2;

                var rewrittenInputParams = RewriteParamList(paramList, rewriteFirstParamTypeTo: "SpsInputs");
                newInputParams = rewrittenInputParams.rewritten;
                oldStructType = rewrittenInputParams.firstParamType;
                mainParamName = rewrittenInputParams.firstParamName;
                var rewrittenPassParams = RewriteParamList(paramList, stripTypes: true,
                    rewriteFirstParamNameTo: $"({oldStructType}){mainParamName}");
                newPassParams = rewrittenPassParams.rewritten;
            } else {
                oldStructType = "appdata_full";
                returnType = "void";
                newInputParams = "inout SpsInputs input";
                mainParamName = "input";
                newPassParams = null;
            }

            var oldStructBody = "";
            if (oldStructType != null) {
                var foundOldParam = GetRegex(@"struct\s+" + Regex.Escape(oldStructType) + @"\s*{")
                    .Match(flattenedProgram);
                if (foundOldParam.Success) {
                    var start = foundOldParam.Index + foundOldParam.Length;
                    var end = IndexOfEndOfNextContext(flattenedProgram, foundOldParam.Index);
                    oldStructBody = flattenedProgram.Substring(start, end - start);
                } else {
                    throw new Exception("Failed to find old struct: " + oldStructType);
                }
            }

            var newStructBody = new List<string>();
            if (!useStructExtends) newStructBody.Add(oldStructBody);
            newStructBody.Add(GetKeywordDefinesFromStruct(oldStructBody));

            void AddParamIfMissing(string keyword, string defaultName, string defaultType) {
                newStructBody.Add($"#ifndef SPS_STRUCT_{keyword}_NAME");
                newStructBody.Add($"  {defaultType} {defaultName} : {keyword};");
                newStructBody.Add($"  #define SPS_STRUCT_{keyword}_TYPE {defaultType}");
                newStructBody.Add($"  #define SPS_STRUCT_{keyword}_TYPE_{defaultType}");
                newStructBody.Add($"  #define SPS_STRUCT_{keyword}_NAME {defaultName}");
                newStructBody.Add($"#endif");
            }
            newStructBody.Add("");
            newStructBody.Add("// Add parameters needed by SPS if missing from the existing struct");
            AddParamIfMissing("POSITION", "spsPosition", "float3");
            AddParamIfMissing("NORMAL", "spsNormal", "float3");
            AddParamIfMissing("TANGENT", "spsTangent", "float4");
            AddParamIfMissing("SV_VertexID", "spsVertexId", "uint");
            AddParamIfMissing("COLOR", "spsColor", "float4");

            var newBody = new List<string>();
            
            // Silent Crosstone
            var useEndif = false;
            if (flattenedProgram.Contains("SCSS_FORWARD_VERTEX_INCLUDED") && !isSurfaceShader) {
                useEndif = true;
                newBody.Add("#if (defined(SHADER_STAGE_VERTEX) || defined(SHADER_STAGE_GEOMETRY))");
            }

            var extends = useStructExtends ? $" : {oldStructType}" : "";
            newBody.Add($"struct SpsInputs{extends} {{");
            newBody.AddRange(newStructBody);
            newBody.Add("};");

            newBody.Add("#define SPS_VANILLA_VERT_PARAM_TYPE " + oldStructType);

            newBody.Add(spsMain);

            newBody.Add($"{returnType} {newVertFunction}({newInputParams}) {{");

            newBody.Add($"  sps_apply({mainParamName});");

            if (newPassParams != null) {
                var ret = returnType == "void" ? "" : "return ";
                newBody.Add($"  {ret}{oldVertFunction}({newPassParams});");
            }

            newBody.Add("}");
            
            // Silent Crosstone
            if (useEndif) {
                newBody.Add("#endif");
            }
            
            program = "\n"
                      + program
                      + "\n"
                      + newBody.Join('\n')
                      + "\n";

            return program;
        }

        public static string GetKeywordDefinesFromStruct(string structBody) {
            // Remove comments
            structBody = Regex.Replace(structBody, @"(//[^\n]*)|(/\*.*?\*/)", "", RegexOptions.Singleline);
            var output = new List<string>();
            var sinceLastIf = "";
            void ProcessSinceLast() {
                var matches = GetRegex(@"([^\s:;]+)[\s]+([^\s:;]+)[\s]*:[\s]*([^\s:;]+)")
                    .Matches(sinceLastIf)
                    .Cast<Match>();
                foreach (var match in matches) {
                    var type = match.Groups[1].ToString();
                    var name = match.Groups[2].ToString();
                    var keyword = match.Groups[3].ToString();
                    if (keyword.EndsWith("0")) {
                        keyword = keyword.Substring(0, keyword.Length - 1);
                    }
                    output.Add($"#define SPS_STRUCT_{keyword}_TYPE {type}");
                    output.Add($"#define SPS_STRUCT_{keyword}_TYPE_{type}");
                    output.Add($"#define SPS_STRUCT_{keyword}_NAME {name}");
                }
                sinceLastIf = "";
            }
            foreach (var line in structBody.Split('\n')) {
                if (line.TrimStart().StartsWith("#")) {
                    ProcessSinceLast();
                    output.Add(line);
                } else {
                    sinceLastIf += "\n" + line;
                }
            }
            ProcessSinceLast();
            return output.Join('\n');
        }

        public class RewriteParamListOutput {
            public string firstParamName;
            public string firstParamType;
            public string rewritten;
        }
        private static RewriteParamListOutput RewriteParamList(string paramList, string rewriteFirstParamTypeTo = null, string rewriteFirstParamNameTo = null, bool stripTypes = false) {
            string firstParamName = null;
            string firstParamType = null;
            var rewritten = paramList.Split('\n').Select(line => {
                if (line.Trim().StartsWith("#")) return line;
                return line.Split(',').Select(p => {
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
                }).Join(',');
            }).Join('\n');
            return new RewriteParamListOutput() {
                firstParamType = firstParamType,
                firstParamName = firstParamName,
                rewritten = rewritten,
            };
        }
        
        private static void WithEachCgInclude(string content, Action<string> withInclude) {
            var lastIncludeEnd = 0;
            while (true) {
                var nextProgramStart = GetRegex(@"\n\s*(CGINCLUDE)\s*\n").Match(content, lastIncludeEnd);
                if (nextProgramStart.Success) {
                    var start = nextProgramStart.Index + nextProgramStart.Length;
                    var endMatch = GetRegex(@"\n\s*ENDCG\s*\n").Match(content, start);
                    if (!endMatch.Success) {
                        throw new Exception("Failed to find CGINCLUDE end marker");
                    }
                    var end = endMatch.Index;
                    var oldProgram = content.Substring(start, end - start);
                    withInclude(oldProgram);
                    lastIncludeEnd = end;
                } else {
                    break;
                }
            }
        }
        
        private static string WithEachProgram(string content, Func<string, bool, string> withProgram) {
            var output = "";
            var lastProgramEnd = 0;
            while (true) {
                var nextProgramStart = GetRegex(@"\n\s*(CGPROGRAM|HLSLPROGRAM)\s*\n").Match(content, lastProgramEnd);
                if (nextProgramStart.Success) {
                    var start = nextProgramStart.Index + nextProgramStart.Length;
                    var isCg = nextProgramStart.Groups[1].ToString() == "CGPROGRAM";
                    output += content.Substring(lastProgramEnd, start - lastProgramEnd);
                    var endMatch = GetRegex(@"\n\s*" + (isCg ? "ENDCG" : "ENDHLSL") + @"\s*\n").Match(content, start);
                    if (!endMatch.Success) {
                        throw new Exception($"Failed to find {nextProgramStart.Groups[1].ToString()} end marker");
                    }
                    var end = endMatch.Index;
                    var oldProgram = content.Substring(start, end - start);
                    var newProgram = withProgram(oldProgram, isCg);
                    output += newProgram;
                    lastProgramEnd = end;
                } else {
                    output += content.Substring(lastProgramEnd);
                    break;
                }
            }
            return output;
        }

        private static string WithEachPass(string content, Func<string, string> withPass, Func<string, string> withRest) {
            var output = "";
            var lastPassEnd = 0;
            var processedPasses = new List<string>();
            while (true) {
                var nextPassStart = GetRegex(@"\n\s*Pass[\s{]*\s*\n").Match(content, lastPassEnd);
                if (nextPassStart.Success) {
                    var start = nextPassStart.Index + nextPassStart.Length;
                    output += content.Substring(lastPassEnd, start - lastPassEnd);
                    var end = IndexOfEndOfNextContext(content, nextPassStart.Index);
                    var oldPass = content.Substring(start, end - start);
                    var newPass = withPass(oldPass);
                    output += $"\n__PASS_{processedPasses.Count}__\n";
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
                    if (bracketLevel == 0) return i;
                } else if (c == '"') {
                    inString = true;
                }
            }
            throw new Exception("Failed to find matching closing bracket");
        }

        private static string WithEachInclude(string contents, string filePath, Func<string, string> replacer = null, bool replaceWithFullPath = false, bool includeLibraryFiles = false) {
            return GetRegex(@"(?:^|\n)(\s*#(?:include|include_with_pragmas)\s"")([^""]+)("")").Replace(contents, match => {
                var before = match.Groups[1].ToString();
                var path = match.Groups[2].ToString();
                var after = match.Groups[3].ToString();
                string fullPath;
                var attempts = new List<string>();
                var isLib = false;
                {
                    fullPath = path;
                    attempts.Add(fullPath);
                }
                if (path.StartsWith("/")) path = path.Substring(1);
                if (filePath != null && !File.Exists(fullPath)) {
                    var p = path;
                    fullPath = VRCFuryAssetDatabase.GetDirectoryName(filePath);
                    while (p.StartsWith("..")) {
                        fullPath = VRCFuryAssetDatabase.GetDirectoryName(fullPath);
                        p = p.Substring(3);
                    }
                    fullPath = Path.Combine(fullPath, p);
                    attempts.Add(fullPath);
                }
                if (!path.Contains("..") && !File.Exists(fullPath)) {
                    fullPath = Path.Combine(EditorApplication.applicationContentsPath, "CGIncludes", path);
                    attempts.Add(fullPath);
                    isLib = true;
                }
                if (!File.Exists(fullPath)) {
                    Debug.LogWarning("Failed to find include at " + attempts.Join(" or "));
                    return match.Groups[0].ToString();
                }
                if (!includeLibraryFiles && isLib) {
                    return match.Groups[0].ToString();
                }

                if (replacer != null) {
                    return "\n" + replacer(fullPath) + "\n";
                } else if (replaceWithFullPath) {
                    if (fullPath.Contains("'")) {
                        throw new Exception(
                            "A unity bug prevents SPS from including shaders stored in a folder with a ' in the name. " +
                            "Please rename the folder to remove the quote symbol: " + fullPath);
                    }
                    return "\n" + before + fullPath + after + "\n";
                } else {
                    return "\n" + before + path + after + "\n";
                }
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
            }, includeLibraryFiles: includeLibraryFiles);
            output.Add(content);
            return output.Join('\n');
        }

        private static string GetPathToSps() {
            var path = AssetDatabase.GUIDToAssetPath("6cf9adf85849489b97305dfeecc74768");
            if (string.IsNullOrWhiteSpace(path)) {
                throw new Exception(
                    "Failed to find the file path to SPS includes. This usually means the unity asset database is confused. Try removing and then re-adding VRCFury.");
            }
            return path;
        }
        private static (string,bool) ReadFile(Shader shader) {
            var path = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrWhiteSpace(path)) {
                throw new Exception("Failed to find source file for the shader");
            }

            var isBuiltIn = false;
            if (path.StartsWith("Resources") || path.StartsWith("Library")) {
                isBuiltIn = true;
                if (shader.name == "Standard") {
                    path = $"{GetPathToSps()}/Standard.shader.orig";
                } else if (shader.name == "Standard (Specular setup)") {
                    path = $"{GetPathToSps()}/StandardSpecular.shader.orig";
                } else if (shader.name.Contains("Error")) {
                    throw new SpsErrorMatException();
                } else {
                    throw new VRCFBuilderException(
                        "SPS does not yet support this built-in unity shader.");
                }
            }

            return (ReadFile(path, true), isBuiltIn);
        }
        private static string ReadFile(string path, bool isMainShader = false) {
            string content;
            if (isMainShader && !path.EndsWith(".shader") && !path.EndsWith(".shader.orig")) {
                var sourceAsset = AssetDatabase.LoadAllAssetsAtPath(path).OfType<TextAsset>().FirstOrDefault();
                if (sourceAsset != null) {
                    content = sourceAsset.text;
                } else if (path.EndsWith(".lilcontainer")) {
                    var lilShaderContainer = ReflectionUtils.GetTypeFromAnyAssembly("lilToon.lilShaderContainer");
                    var unpackMethod = lilShaderContainer.GetMethods()
                        .First(m => m.Name == "UnpackContainer" && m.GetParameters().Length == 2);
                    content = (string)ReflectionUtils.CallWithOptionalParams(unpackMethod, null, path);
                    var shaderLibsPath = (string)lilShaderContainer.GetField("shaderLibsPath",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                    content = content.Replace("\"Includes", "\"" + shaderLibsPath);
                } else {
                    throw new Exception("Failed to find source for post-processed shader: " + path);
                }
            } else {
                StreamReader sr = new StreamReader(path);
                try {
                    content = sr.ReadToEnd();
                } finally {
                    sr.Close();
                }
            }

            content = WithEachInclude(content, path, replaceWithFullPath: true);
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
