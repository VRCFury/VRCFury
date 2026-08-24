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
using VF.Exceptions;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class SpsPatcher {
        [ReflectionHelperOptional]
        private abstract class LilReflection : ReflectionHelper {
            public static readonly Type LilShaderContainer = ReflectionUtils.GetTypeFromAnyAssembly("lilToon.lilShaderContainer");
            public static readonly MethodInfo UnpackContainer = LilShaderContainer?.GetMethods()
                .FirstOrDefault(m => m.Name == "UnpackContainer" && m.GetParameters().Length == 2);
            public static readonly FieldInfo ShaderLibsPath = LilShaderContainer?.VFStaticField("shaderLibsPath");
        }

        private const string HashBuster = "17";
        
        public static void Patch(Material mat, bool keepImports, bool hasBlendshapes) {
            if (!mat.shader) return;
            if (mat.shader.name == "VRChat/Mobile/Particles/Additive") return;
            try {
                var renderQueue = mat.renderQueue;
                PatchUnsafe(mat, keepImports, hasBlendshapes);
                mat.renderQueue = renderQueue;
            } catch(SpsErrorMatException e) {
                var msg = $"Your avatar is using a material ({mat.name}) that couldn't load properly.\n\n" +
                          $"The shader used by this material may be broken or out of date in your project. Ask the creator of this asset what shader and version should be used.";
                if (e.Message != null) {
                    msg += "\n\n" + e.Message;
                }
                throw new SneakyException(msg);
            } catch (Exception e) {
                throw new ExceptionWithCause(
                    $"Failed to patch shader with SPS. Report this on the VRCFury discord." +
                    $"Maybe this shader isn't supported yet: {mat.shader.name}",
                    e
                );
            }
        }

        private static Regex GetRegex(string pattern) {
            return new Regex(pattern, RegexOptions.Compiled);
        }

        /**
         * PCSS has a broken META pass which does not compile.
         * Rather than wait, we can just patch it here.
         */
        private static string ApplyPcssFix(Shader shader, string contents) {
            if (shader == null) return contents;
            if (shader.name.IndexOf("PCSS", StringComparison.OrdinalIgnoreCase) < 0) return contents;
            return GetRegex("(?m)^(\\s*#include\\s*\"[^\"]*[\\\\/]custom\\.hlsl\"\\s*)$").Replace(
                contents,
                "$1\n#undef LIL_V2F_POSITION_WS",
                1
            );
        }

        private static void PatchUnsafe(Material mat, bool keepImports, bool hasBlendshapes) {
            var shader = mat.shader;
            var newShader = PatchUnsafe(shader, keepImports, hasBlendshapes);
            mat.shader = newShader.shader;
            mat.Dirty();
        }

        public class PatchResult {
            public Shader shader;
            public int patchedPrograms;
        }
        private static PatchResult PatchUnsafe(Shader shader, bool keepImports, bool hasBlendshapes, string parentHash = null) {
            var pathToSps = GetPathToSps();
            var sourcePath = ResolveShaderSource(shader);
            var hash = GetPatchHash(sourcePath, pathToSps, keepImports, hasBlendshapes, parentHash);

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

            var contents = ReadFile(sourcePath, true);
            contents = ApplyPcssFix(shader, contents);

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
                var propertiesContent = ReadAndFlattenPath($"{pathToSps}/deform/sps_deform_props.cginc");
                Replace(
                    @"((?:^|\n)\s*Properties\s*{)",
                    $"$1\n{propertiesContent}\n",
                    1
                );
                contents = GetRegex(@"(?:^|\n)[ \t]*CustomEditor[ \t]+[^\n]+").Replace(contents, "");
            }

            string spsMain;
            if (keepImports) {
                spsMain = $"{(hasBlendshapes ? "#define SPS_HAS_BLENDSHAPES\n" : "")}#include \"{pathToSps}/deform/sps_deform_main.cginc\"";
            } else {
                spsMain = (hasBlendshapes ? "#define SPS_HAS_BLENDSHAPES\n" : "")
                    + ReadAndFlattenPath($"{pathToSps}/deform/sps_deform_main.cginc");
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
                        throw new ExceptionWithCause($"Failed to patch pass #{passNum}", e);
                    }
                },
                rest => {
                    try {
                        var (newRest, num) = PatchPass(rest, spsMain, cgIncludes, true);
                        patchedPrograms += num;
                        return newRest;
                    } catch (Exception e) {
                        throw new ExceptionWithCause($"Failed to patch non-pass segment", e);
                    }
                }
            );
            var childShaders = new Dictionary<Shader, Shader>();
            contents = GetRegex(@"(?:^|\n)[ \t]*UsePass[ \t]+""([^""]+)/([^""/]+)""").Replace(contents, match => {
                var shaderName = match.Groups[1].ToString();
                var passName = match.Groups[2].ToString();
                var includedShader = Shader.Find(shaderName);
                if (!includedShader) {
                    throw new Exception("Failed to find included shader: " + shaderName);
                }

                if (!childShaders.TryGetValue(includedShader, out var rewrittenIncludedShader)) {
                    var output = PatchUnsafe(includedShader, keepImports, hasBlendshapes, hash);
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
                var testMat = VrcfObjectFactory.CreateMaterial(shader);
                for (int i = 0; i < testMat.passCount; i++) {
                    ShaderUtil.CompilePass(testMat, i, true);
                }
                if (ShaderUtil.ShaderHasError(shader)) {
                    var vanillaError = ShaderUtil
                        .GetShaderMessages(shader)
                        .First(x => x.severity == ShaderCompilerMessageSeverity.Error);
                    throw new SpsErrorMatException($"The vanilla shader at {AssetDatabase.GetAssetPath(shader)} has an internal error:\n\n" + vanillaError.file+":"+vanillaError.line+" "+vanillaError.message);
                }

                var patchedError = ShaderUtil
                    .GetShaderMessages(newShader)
                    .First(x => x.severity == ShaderCompilerMessageSeverity.Error);
                throw new VRCFBuilderException("Patch succeeded, but shader failed to compile.\n\n" + patchedError.file+":"+patchedError.line+" "+patchedError.message);
            }

            return new PatchResult {
                shader = newShader,
                patchedPrograms = patchedPrograms
            };
        }

        private static (string,int) PatchPass(string pass, string spsMain, string cgIncludes, bool isSurfaceShader) {
            if (GetRegex(@"""LightMode""\s*=\s*""(?:Meta|Never)""").IsMatch(pass)) {
                return (pass, 0);
            }

            var patchedPrograms = 0;
            pass = WithEachProgram(pass, (program, isCgProgram) => {
                patchedPrograms++;
                try {
                    return PatchProgram(program, isCgProgram, spsMain, cgIncludes, isSurfaceShader);
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to patch program #{patchedPrograms}", e);
                }
            });

            return (pass, patchedPrograms);
        }

        internal static string PatchProgram(string originalProgram, bool isCgProgram, string spsMain, string cgIncludes, bool isSurfaceShader) {
            var program = originalProgram;
            var newVertFunctionName = "spsVert";
            var pragmaKeyword = isSurfaceShader ? "surface" : "vertex";
            string vanillaVertFunctionName = null;
            var pragmaFound = false;
            program = GetRegex(@"(#pragma[ \t]+" + pragmaKeyword + @"[ \t]+)([^\s]+)([^\n]*)").Replace(program, match => {
                string newPragma;
                if (isSurfaceShader) {
                    var pragmaSuffix = match.Groups[3].ToString();
                    pragmaSuffix = GetRegex(@"vertex:(\S+)").Replace(pragmaSuffix, vertMatch => {
                        vanillaVertFunctionName = vertMatch.Groups[1].ToString();
                        return "vertex:" + newVertFunctionName;
                    });
                    if (vanillaVertFunctionName == null) {
                        newPragma = $"{match.Groups[0]} vertex:{newVertFunctionName}";
                    } else {
                        newPragma = $"{match.Groups[1]}{match.Groups[2]}{pragmaSuffix}";
                    }
                } else {
                    vanillaVertFunctionName = match.Groups[2].ToString();
                    newPragma = $"{match.Groups[1]}{newVertFunctionName}{match.Groups[3]}";
                }

                pragmaFound = true;
                return $"// {match.Groups[0]}\n{newPragma}\n";
            }, 1);
            if (!pragmaFound) {
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

            string returnTypeName;
            string returnSemantic = null;
            ParsedParamList parsedParams;

            string GetStructBody(string typeName) {
                var structMatch = GetRegex(@"struct\s+" + Regex.Escape(typeName) + @"\s*{")
                    .Match(flattenedProgram);
                if (!structMatch.Success) return null;
                var start = structMatch.Index + structMatch.Length;
                var end = IndexOfEndOfNextContext(flattenedProgram, structMatch.Index);
                return flattenedProgram.Substring(start, end - start);
            }

            if (vanillaVertFunctionName != null) {
                var vanillaVertCandidates = FindFunctionCandidates(flattenedProgram, vanillaVertFunctionName);
                if (vanillaVertCandidates.Length > 1) {
                    vanillaVertCandidates = vanillaVertCandidates.Distinct().ToArray();
                }
                // Special case for Standard
                if (vanillaVertCandidates.Length > 1) {
                    vanillaVertCandidates = vanillaVertCandidates.Where(m => !m.returnTypeName.Contains("Simple")).ToArray();
                }
                // Special case for Fast Fur
                if (vanillaVertCandidates.Length > 1) {
                    if (flattenedProgram.Contains("FUR_SKIN_LAYER")) {
                        var skinLayerDefined = flattenedProgram.Contains("#define FUR_SKIN_LAYER");
                        vanillaVertCandidates = vanillaVertCandidates.Where(m => m.returnTypeName == (skinLayerDefined ? "fragInput" : "hullGeomInput")).ToArray();
                    }
                }

                if (vanillaVertCandidates.Length == 0) {
                    throw new Exception("Failed to find vertex method: " + vanillaVertFunctionName);
                }

                if (vanillaVertCandidates.Length > 1) {
                    throw new Exception("Found vertex method multiple times: "
                                        + vanillaVertFunctionName
                                        + "\n"
                                        + vanillaVertCandidates.Select(f => f.returnTypeName + " " + f.paramList).Join('\n')
                    );
                }

                var vanillaParamList = vanillaVertCandidates[0].paramList;
                returnTypeName = vanillaVertCandidates[0].returnTypeName;
                returnSemantic = vanillaVertCandidates[0].returnSemantic;

                if (vanillaParamList.Trim().IsEmpty()) {
                    // Used occasionally as an "empty" pass. The vertex shader doesn't accept any params, so it's basically impossible
                    // for the pass to render anything, so just return it as is.
                    return originalProgram;
                }

                parsedParams = ParseParamList(
                    vanillaParamList,
                    isSurfaceShader,
                    GetStructBody
                );
            } else {
                returnTypeName = "void";
                parsedParams = ParseParamList("inout appdata_full input", isSurfaceShader, GetStructBody);
                parsedParams.vanillaVertArgs = null;
            }

            var newStructBody = new List<string>();
            newStructBody.Add(parsedParams.newInputStructBody);
            newStructBody.Add(parsedParams.vanillaStructDefines);
            newStructBody.Add(GetKeywordDefinesFromStruct(parsedParams.newInputStructBody));

            void AddParamIfMissing(string keyword, string defaultName, string defaultTypeName) {
                newStructBody.Add($"#ifndef SPS_STRUCT_{keyword}_NAME");
                newStructBody.Add($"  {defaultTypeName} {defaultName} : {keyword};");
                newStructBody.Add($"  #define SPS_STRUCT_{keyword}_TYPE {defaultTypeName}");
                newStructBody.Add($"  #define SPS_STRUCT_{keyword}_TYPE_{defaultTypeName}");
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
            var wrapsSilentCrosstoneStages = false;
            if (flattenedProgram.Contains("SCSS_FORWARD_VERTEX_INCLUDED") && !isSurfaceShader) {
                wrapsSilentCrosstoneStages = true;
                newBody.Add("#if (defined(SHADER_STAGE_VERTEX) || defined(SHADER_STAGE_GEOMETRY))");
            }

            newBody.Add("struct SpsInputs {");
            newBody.AddRange(newStructBody);
            newBody.Add("};");

            newBody.Add(spsMain);

            var returnSemanticSuffix = returnSemantic.IsEmpty() ? "" : $" : {returnSemantic}";
            newBody.Add($"{returnTypeName} {newVertFunctionName}({parsedParams.newVertParams}){returnSemanticSuffix} {{");

            newBody.Add($"  sps_apply({parsedParams.inputName});");

            if (parsedParams.vanillaVertArgs != null) {
                if (parsedParams.beforeVanillaCall.IsNotEmpty()) {
                    newBody.Add(parsedParams.beforeVanillaCall);
                }
                if (parsedParams.afterVanillaCall.IsEmpty()) {
                    var returnPrefix = returnTypeName == "void" ? "" : "return ";
                    newBody.Add($"  {returnPrefix}{vanillaVertFunctionName}({parsedParams.vanillaVertArgs});");
                } else if (returnTypeName == "void") {
                    newBody.Add($"  {vanillaVertFunctionName}({parsedParams.vanillaVertArgs});");
                    newBody.Add(parsedParams.afterVanillaCall);
                } else {
                    newBody.Add($"  {returnTypeName} {parsedParams.returnValueName} = {vanillaVertFunctionName}({parsedParams.vanillaVertArgs});");
                    newBody.Add(parsedParams.afterVanillaCall);
                    newBody.Add($"  return {parsedParams.returnValueName};");
                }
            }

            newBody.Add("}");
            
            // Silent Crosstone
            if (wrapsSilentCrosstoneStages) {
                newBody.Add("#endif");
            }
            
            program = "\n"
                      + program
                      + "\n"
                      + newBody.Join('\n')
                      + "\n";

            return program;
        }

        public static string GetKeywordDefinesFromStruct(string structBody, string prefix = "SPS_STRUCT") {
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
                    var typeKeyword = Regex.Replace(type, @"[^a-zA-Z0-9_]", "_");
                    var name = match.Groups[2].ToString();
                    var keyword = match.Groups[3].ToString();
                    if (keyword.EndsWith("0")) {
                        keyword = keyword.Substring(0, keyword.Length - 1);
                    }
                    output.Add($"#define {prefix}_{keyword}_TYPE {type}");
                    output.Add($"#define {prefix}_{keyword}_TYPE_{typeKeyword}");
                    output.Add($"#define {prefix}_{keyword}_NAME {name}");
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

        private class ParsedParamList {
            public string inputName;
            public string newInputStructBody;
            public string vanillaStructDefines;
            public string newVertParams;
            public string vanillaVertArgs;
            public string beforeVanillaCall;
            public string afterVanillaCall;
            public string returnValueName;
        }

        private static (string paramList, string returnTypeName, string returnSemantic)[] FindFunctionCandidates(
            string program,
            string functionName
        ) {
            var output = new List<(string,string,string)>();
            var nameMatches = GetRegex(@"\b" + Regex.Escape(functionName) + @"\s*\(")
                .Matches(program)
                .Cast<Match>();
            foreach (var nameMatch in nameMatches) {
                if (nameMatch.Index == 0 || !Char.IsWhiteSpace(program[nameMatch.Index - 1])) continue;
                var openParen = program.IndexOf('(', nameMatch.Index);
                var depth = 1;
                var closeParen = -1;
                var inLineComment = false;
                var inBlockComment = false;
                var inString = false;
                for (var i = openParen + 1; i < program.Length; i++) {
                    var c = program[i];
                    var next = i + 1 < program.Length ? program[i + 1] : '\0';
                    if (inLineComment) {
                        if (c == '\n') inLineComment = false;
                        continue;
                    }
                    if (inBlockComment) {
                        if (c == '*' && next == '/') {
                            inBlockComment = false;
                            i++;
                        }
                        continue;
                    }
                    if (inString) {
                        if (c == '\\') {
                            i++;
                        } else if (c == '"') {
                            inString = false;
                        }
                        continue;
                    }
                    if (c == '/' && next == '/') {
                        inLineComment = true;
                        i++;
                    } else if (c == '/' && next == '*') {
                        inBlockComment = true;
                        i++;
                    } else if (c == '"') {
                        inString = true;
                    } else if (c == '(') {
                        depth++;
                    } else if (c == ')' && --depth == 0) {
                        closeParen = i;
                        break;
                    }
                }
                if (closeParen < 0) continue;

                var afterParams = closeParen + 1;
                while (afterParams < program.Length && Char.IsWhiteSpace(program[afterParams])) afterParams++;
                var returnSemantic = "";
                if (afterParams < program.Length && program[afterParams] == ':') {
                    afterParams++;
                    while (afterParams < program.Length && Char.IsWhiteSpace(program[afterParams])) afterParams++;
                    var semanticStart = afterParams;
                    while (afterParams < program.Length
                           && !Char.IsWhiteSpace(program[afterParams])
                           && program[afterParams] != '{') {
                        afterParams++;
                    }
                    returnSemantic = program.Substring(semanticStart, afterParams - semanticStart);
                    while (afterParams < program.Length && Char.IsWhiteSpace(program[afterParams])) afterParams++;
                }
                if (afterParams >= program.Length || program[afterParams] != '{') continue;

                var typeEnd = nameMatch.Index;
                while (typeEnd > 0 && Char.IsWhiteSpace(program[typeEnd - 1])) typeEnd--;
                var typeStart = typeEnd - 1;
                var templateDepth = 0;
                for (; typeStart >= 0; typeStart--) {
                    var c = program[typeStart];
                    if (c == '>') templateDepth++;
                    if (c == '<') templateDepth--;
                    if (templateDepth == 0 && Char.IsWhiteSpace(c)) break;
                }
                typeStart++;
                if (typeStart >= typeEnd) continue;
                var returnTypeName = program.Substring(typeStart, typeEnd - typeStart);
                var paramList = program.Substring(openParen + 1, closeParen - openParen - 1);
                output.Add((paramList, returnTypeName, returnSemantic));
            }
            return output.ToArray();
        }

        private static ParsedParamList ParseParamList(
            string paramList,
            bool isSurfaceShader,
            Func<string, string> getStructBody
        ) {
            const string paramPattern = @"^(?<modifier>.*?)(?<type>\S+)\s+(?<name>[^\s:\[=]+)(?<array>(?:\[[^\]]*\])*)"
                                        + @"(?:\s*:\s*(?<semantic>[^\s=]+))?(?:\s*=\s*(?<default>.*))?$";
            var withoutComments = Regex.Replace(paramList, @"/\*.*?\*/", "", RegexOptions.Singleline);
            withoutComments = Regex.Replace(withoutComments, @"//[^\n]*", "");
            var parts = new List<string>();
            var pendingParam = "";
            void FlushParam() {
                var normalized = Regex.Replace(pendingParam, @"\s+", " ").Trim();
                normalized = Regex.Replace(normalized, @"\s*([<,\[])\s*", "$1");
                normalized = Regex.Replace(normalized, @"\s*([>\]])", "$1");
                if (normalized.IsNotEmpty()) parts.Add(normalized);
                pendingParam = "";
            }
            var nestedDepth = 0;
            foreach (var line in withoutComments.Split('\n')) {
                if (line.TrimStart().StartsWith("#")) {
                    FlushParam();
                    parts.Add(line.Trim());
                    continue;
                }
                pendingParam += " ";
                foreach (var c in line) {
                    if (c == '<' || c == '[' || c == '(' || c == '{') nestedDepth++;
                    if (c == '>' || c == ']' || c == ')' || c == '}') nestedDepth--;
                    if (c == ',' && nestedDepth == 0) {
                        FlushParam();
                        parts.Add(",");
                    } else {
                        pendingParam += c;
                    }
                }
            }
            FlushParam();

            var parsedParamMatches = new Dictionary<string, Match>();
            foreach (var part in parts.Where(part => !part.StartsWith("#") && part != ",")) {
                var match = Regex.Match(part, paramPattern);
                if (!match.Success) throw new Exception("Failed to parse vertex parameter: " + part);
                parsedParamMatches[part] = match;
            }
            string GetModifier(Match match) {
                return match.Groups["modifier"].ToString().Trim();
            }
            bool IsOutParam(Match match) {
                return GetRegex(@"\bout\b").IsMatch(GetModifier(match));
            }
            bool IsInoutParam(Match match) {
                return GetRegex(@"\binout\b").IsMatch(GetModifier(match));
            }
            string GetBody(Match match) {
                return getStructBody(match.Groups["type"].ToString());
            }
            bool IsStructParam(Match match) {
                return match.Groups["semantic"].ToString().IsEmpty()
                       && !IsOutParam(match)
                       && GetBody(match) != null;
            }

            var allParamMatches = parsedParamMatches.Values.ToArray();
            // A raw shader entry point's inout parameters are outputs even when the function also has out
            // parameters or a return value. Keep those outputs separate from SpsInputs, which also contains
            // input-only semantics such as SV_VertexID and therefore cannot itself be used as an output.
            var useSeparateInoutOutputs = !isSurfaceShader;
            var structParamMatches = allParamMatches
                .Where(match => match.Groups["semantic"].ToString().IsEmpty())
                .Where(match => !IsOutParam(match))
                .Where(match => IsStructParam(match))
                .ToArray();

            var usedNames = allParamMatches
                .Select(match => match.Groups["name"].ToString())
                .ToHashSet();
            string MakeUniqueName(string desired) {
                while (!usedNames.Add(desired)) desired += "_";
                return desired;
            }
            var inputName = MakeUniqueName("input");
            var returnValueName = MakeUniqueName("spsReturnValue");
            var localNames = new Dictionary<string, string>();
            var outputNames = new Dictionary<string, string>();
            string GetLocalName(Match match) {
                var name = match.Groups["name"].ToString();
                if (!localNames.TryGetValue(name, out var localName)) {
                    localName = MakeUniqueName("vanillaInput_" + name);
                    localNames[name] = localName;
                }
                return localName;
            }
            string GetOutputName(Match match) {
                var name = match.Groups["name"].ToString();
                if (!outputNames.TryGetValue(name, out var outputName)) {
                    outputName = MakeUniqueName("spsOutput_" + name);
                    outputNames[name] = outputName;
                }
                return outputName;
            }

            string RenderWithDirectives(Func<Match,string> renderParam, bool preserveCommas, bool requireStatement) {
                var output = new List<string>();
                var hasContent = false;
                foreach (var part in parts) {
                    if (part.StartsWith("#")) {
                        output.Add(part);
                    } else if (part == ",") {
                        if (preserveCommas) output.Add(part);
                    } else {
                        var rendered = renderParam(parsedParamMatches[part]);
                        if (rendered == null) continue;
                        output.Add(rendered);
                        hasContent = true;
                    }
                }
                if (requireStatement && !hasContent) return "";
                return "\n" + output.Join('\n') + "\n";
            }

            var newInputStructBody = RenderWithDirectives(match => {
                if (IsStructParam(match)) return GetBody(match);
                if (match.Groups["semantic"].ToString().IsEmpty() || IsOutParam(match)) return null;
                return $"{match.Groups["type"]} {match.Groups["name"]}{match.Groups["array"]} : {match.Groups["semantic"]};";
            }, preserveCommas: false, requireStatement: false);

            var structParamNames = structParamMatches
                .Select(match => match.Groups["name"].ToString())
                .Distinct()
                .ToArray();
            var vanillaStructDefines = structParamNames.Length == 1
                ? RenderWithDirectives(match => {
                    if (!IsStructParam(match) || match.Groups["name"].ToString() != structParamNames[0]) return null;
                    var typeName = match.Groups["type"].ToString();
                    return "#define SPS_VANILLA_STRUCT_EXISTS\n"
                           + "#define SPS_VANILLA_VERT_PARAM_TYPE " + typeName + "\n"
                           + GetKeywordDefinesFromStruct(GetBody(match), "SPS_VANILLA_STRUCT");
                }, preserveCommas: false, requireStatement: true)
                : "";

            string RenderParamList(string firstParam, Func<Match,string> renderParam) {
                var output = new List<string>();
                var hasParam = firstParam != null;
                var conditionalStack = new Stack<(bool before, bool anyBranch)>();
                if (hasParam) output.Add(firstParam);
                foreach (var part in parts) {
                    if (part.StartsWith("#")) {
                        output.Add(part);
                        var directive = Regex.Match(part, @"^#\s*(\w+)").Groups[1].ToString();
                        if (directive == "if" || directive == "ifdef" || directive == "ifndef") {
                            conditionalStack.Push((hasParam, false));
                        } else if ((directive == "else" || directive == "elif") && conditionalStack.Count > 0) {
                            var conditional = conditionalStack.Pop();
                            conditional.anyBranch |= hasParam;
                            hasParam = conditional.before;
                            conditionalStack.Push(conditional);
                        } else if (directive == "endif" && conditionalStack.Count > 0) {
                            var conditional = conditionalStack.Pop();
                            hasParam |= conditional.anyBranch;
                        }
                        continue;
                    }
                    if (part == ",") continue;
                    var paramMatch = parsedParamMatches[part];
                    var rendered = renderParam(paramMatch);
                    if (rendered == null) continue;
                    output.Add((hasParam ? ", " : "") + rendered);
                    hasParam = true;
                }
                return "\n" + output.Join('\n') + "\n";
            }

            var structModifier = isSurfaceShader
                                 && structParamMatches.Any(IsInoutParam)
                ? "inout "
                : "";
            var newVertParams = RenderParamList(structModifier + "SpsInputs " + inputName, match => {
                if (IsOutParam(match)) return match.Value;
                var embeddedInput = IsStructParam(match) || match.Groups["semantic"].ToString().IsNotEmpty();
                if (useSeparateInoutOutputs && embeddedInput && IsInoutParam(match)) {
                    var modifier = Regex.Replace(GetModifier(match), @"\binout\b", "out");
                    var semantic = match.Groups["semantic"].ToString();
                    var semanticSuffix = semantic.IsEmpty() ? "" : " : " + semantic;
                    return $"{modifier} {match.Groups["type"]} {GetOutputName(match)}{match.Groups["array"]}{semanticSuffix}";
                }
                if (embeddedInput) return null;
                return match.Value;
            });

            var beforeVanillaCall = !useSeparateInoutOutputs ? "" : RenderWithDirectives(match => {
                var embeddedInput = IsStructParam(match) || match.Groups["semantic"].ToString().IsNotEmpty();
                if (!embeddedInput || !IsInoutParam(match)) return null;
                var initialValue = IsStructParam(match)
                    ? $"({match.Groups["type"]}){inputName}"
                    : $"{inputName}.{match.Groups["name"]}";
                return $"  {match.Groups["type"]} {GetLocalName(match)} = {initialValue};";
            }, preserveCommas: false, requireStatement: true);
            var afterVanillaCall = !useSeparateInoutOutputs ? "" : RenderWithDirectives(match => {
                var embeddedInput = IsStructParam(match) || match.Groups["semantic"].ToString().IsNotEmpty();
                if (!embeddedInput || !IsInoutParam(match)) return null;
                return $"  {GetOutputName(match)} = {GetLocalName(match)};";
            }, preserveCommas: false, requireStatement: true);

            var vanillaVertArgs = RenderWithDirectives(match => {
                var name = match.Groups["name"].ToString();
                if (IsOutParam(match)) return name;
                var embeddedInput = IsStructParam(match) || match.Groups["semantic"].ToString().IsNotEmpty();
                if (useSeparateInoutOutputs && embeddedInput && IsInoutParam(match)) return GetLocalName(match);
                if (IsStructParam(match)) {
                    return $"({match.Groups["type"]}){inputName}";
                }
                if (match.Groups["semantic"].ToString().IsNotEmpty()) return $"{inputName}.{name}";
                return name;
            }, preserveCommas: true, requireStatement: false);

            return new ParsedParamList {
                inputName = inputName,
                newInputStructBody = newInputStructBody,
                vanillaStructDefines = vanillaStructDefines,
                newVertParams = newVertParams,
                vanillaVertArgs = vanillaVertArgs,
                beforeVanillaCall = beforeVanillaCall,
                afterVanillaCall = afterVanillaCall,
                returnValueName = returnValueName
            };
        }
        
        private static void WithEachCgInclude(string content, Action<string> withInclude) {
            var lastIncludeEnd = 0;
            while (true) {
                var nextProgramStart = GetRegex(@"(?:^|\n)[ \t]*(CGINCLUDE)[ \t]*(?:\n|$)").Match(content, lastIncludeEnd);
                if (nextProgramStart.Success) {
                    var start = nextProgramStart.Index + nextProgramStart.Length;
                    var endMatch = GetRegex(@"(?:^|\n)[ \t]*ENDCG[ \t]*(?:\n|$)").Match(content, start);
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
        
        internal static string WithEachProgram(string content, Func<string, bool, string> withProgram) {
            var output = "";
            var lastProgramEnd = 0;
            while (true) {
                var nextProgramStart = GetRegex(@"(?:^|\n)[ \t]*(CGPROGRAM|HLSLPROGRAM)[ \t]*(?:\n|$)").Match(content, lastProgramEnd);
                if (nextProgramStart.Success) {
                    var start = nextProgramStart.Index + nextProgramStart.Length;
                    var isCg = nextProgramStart.Groups[1].ToString() == "CGPROGRAM";
                    output += content.Substring(lastProgramEnd, start - lastProgramEnd);
                    var endMatch = GetRegex(@"(?:^|\n)[ \t]*" + (isCg ? "ENDCG" : "ENDHLSL") + @"[ \t]*(?:\n|$)").Match(content, start);
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
                var nextPassStart = GetRegex(@"(?:^|\n)[ \t]*Pass(?:[ \t]|{)*[ \t]*(?:\n|$)").Match(content, lastPassEnd);
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
        private static string GetPatchHash(string sourcePath, string pathToSps, bool keepImports, bool hasBlendshapes, string parentHash) {
            using (var md5 = MD5.Create()) {
                var hashContent = new StringBuilder();
                void Add(string value) {
                    hashContent.Append(value ?? "");
                    hashContent.Append('\n');
                }

                void AddFile(string path) {
                    if (string.IsNullOrWhiteSpace(path)) return;
                    Add(path.Replace('\\', '/'));
                    if (File.Exists(path)) {
                        var info = new FileInfo(path);
                        Add(info.Length.ToString());
                    }
                }

                Add(HashBuster);
                Add(keepImports.ToString());
                Add(hasBlendshapes.ToString());
                AddFile(sourcePath);
                if (parentHash == null) AddFile($"{pathToSps}/deform/sps_deform_props.cginc");
                AddFile($"{pathToSps}/deform/sps_deform_main.cginc");

                var hashContentBytes = Encoding.UTF8.GetBytes(hashContent.ToString());
                var hashBytes = md5.ComputeHash(hashContentBytes);
                var hash = Enumerable.Range(0, hashBytes.Length)
                    .Select(i => hashBytes[i].ToString("x2"))
                    .Join("");

                if (parentHash != null) {
                    hash = $"{parentHash}-{hash}";
                }

                return hash;
            }
        }

        private static string ResolveShaderSource(Shader shader) {
            var path = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrWhiteSpace(path)) {
                throw new Exception("Failed to find source file for the shader");
            }

            if (path.StartsWith("Resources") || path.StartsWith("Library")) {
                if (shader.name == "Standard") {
                    path = $"{GetPathToSps()}/vanilla~/Standard.shader";
                } else if (shader.name == "Standard (Specular setup)") {
                    path = $"{GetPathToSps()}/vanilla~/StandardSpecular.shader";
                } else if (shader.name == "Unlit/Color") {
                    path = $"{GetPathToSps()}/vanilla~/Unlit-Color.shader";
                } else if (shader.name.Contains("Error")) {
                    throw new SpsErrorMatException();
                } else {
                    throw new VRCFBuilderException(
                        "SPS does not yet support this built-in unity shader.");
                }
            }

            return path;
        }
        private static string ReadFile(string path, bool isMainShader = false) {
            string content;
            if (isMainShader && !path.EndsWith(".shader") && !path.EndsWith(".shader.orig")) {
                var sourceAsset = AssetDatabase.LoadAllAssetsAtPath(path).OfType<TextAsset>().FirstOrDefault();
                if (sourceAsset != null) {
                    content = sourceAsset.text;
                } else if (path.EndsWith(".lilcontainer")) {
                    if (!ReflectionHelper.IsReady<LilReflection>()) {
                        throw new Exception("Failed to access lilToon shader container internals");
                    }
                    content = (string)ReflectionUtils.CallWithOptionalParams(LilReflection.UnpackContainer, null, path);
                    var shaderLibsPath = (string)LilReflection.ShaderLibsPath.GetValue(null);
                    content = content.Replace("\"Includes", "\"" + shaderLibsPath);
                } else {
                    throw new Exception("Failed to find source for post-processed shader: " + path);
                }
            } else {
                using (var sr = new StreamReader(path)) {
                    content = sr.ReadToEnd();
                }
            }

            content = WithEachInclude(content, path, replaceWithFullPath: true);
            content = content.Replace("\r", "");
            return content;
        }
        
        private static void WriteFile(string path, string content) {
            using (var sw = new StreamWriter(path)) {
                sw.Write(content);
            }
        }
    }
}
