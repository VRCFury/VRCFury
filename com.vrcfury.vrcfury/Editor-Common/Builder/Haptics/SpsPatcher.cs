using System;
using System.Collections.Generic;
using System.Globalization;
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
    internal sealed class SpsPatcher {
        [ReflectionHelperOptional]
        private abstract class LilReflection : ReflectionHelper {
            public static readonly Type LilShaderContainer = ReflectionUtils.GetTypeFromAnyAssembly("lilToon.lilShaderContainer");
            public static readonly MethodInfo UnpackContainer = LilShaderContainer?.GetMethods()
                .FirstOrDefault(m => m.Name == "UnpackContainer" && m.GetParameters().Length == 2);
            public static readonly FieldInfo ShaderLibsPath = LilShaderContainer?.VFStaticField("shaderLibsPath");
        }

        [ReflectionHelperOptional]
        private abstract class ShaderGraphReflection : ReflectionHelper {
            public static readonly MethodInfo GetShaderText = ReflectionUtils
                .GetTypeFromAnyAssembly("UnityEditor.ShaderGraph.ShaderGraphImporter")
                ?.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "GetShaderText" && m.GetParameters().Length == 4
                    && m.GetParameters()[3].IsOut);
        }

        internal const string ShaderNamePrefix = "Hidden/SPSPatched/";
        internal const string LockedShaderNamePrefix = "Hidden/Locked/SPSPatched/";
        private const string HashBuster = "74";
        private const string ShaderLabComment = @"(?s:/\*.*?\*/)|//[^\r\n]*";
        private const string ShaderLabTrivia = @"(?:\s|" + ShaderLabComment + ")*";
        private const int MaxCachedPatterns = 256;
        private const string ImplicitCgIncludes = "#include \"HLSLSupport.cginc\"\n#include \"UnityShaderVariables.cginc\"\n";
        private static readonly Dictionary<string, Regex> RegexCache = new();
        private static readonly Regex IncludeRegex = GetRegex(@"\G(?:^|\n)(\s*#(?:include|include_with_pragmas)\s"")([^""]+)("")");
        private static readonly Regex SkippedLightModeRegex = GetRegex(@"""LightMode""\s*=\s*""(?:Meta|Never)""");
        
        // A patcher is shared by one bake. Relative includes are resolved once per file.
        private readonly Dictionary<string, IncludeContent> includeFiles = new();
        private readonly Dictionary<string, string> flattenedSpsFiles = new();
        private readonly Dictionary<string, (string source, HashSet<string> visited)> flattenedCgIncludes = new();
        private readonly Dictionary<
            (Shader shader, bool keepImports, bool hasBlendshapes),
            (PatchResult root, List<PatchResult> imports, bool batchImported)
        > preparedShaders = new();

        public static void Patch(Material mat, bool keepImports, bool hasBlendshapes) {
            new SpsPatcher().PatchMaterial(mat, keepImports, hasBlendshapes);
        }

        internal void PatchMaterial(Material mat, bool keepImports, bool hasBlendshapes) {
            if (!mat.shader) return;
            if (mat.shader.name == "VRChat/Mobile/Particles/Additive") return;
            SpsPlugReceiverService.RequireMaterialContacts();
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
                    $"Failed to patch shader with SPS. Report this on the VRCFury Discord. " +
                    $"Maybe this shader isn't supported yet: {mat.shader.name}",
                    e
                );
            }
        }

        private static Regex GetRegex(string pattern) {
            lock (RegexCache) {
                if (RegexCache.TryGetValue(pattern, out var regex)) return regex;
                // These patterns can include user-defined function and struct names.
                if (RegexCache.Count >= MaxCachedPatterns) RegexCache.Clear();
                return RegexCache[pattern] = new Regex(pattern, RegexOptions.Compiled);
            }
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

        private void PatchUnsafe(Material mat, bool keepImports, bool hasBlendshapes) {
            var shader = mat.shader;
            var newShader = PatchUnsafe(shader, keepImports, hasBlendshapes);
            ShaderExtensions.WithAsyncCompilationDuringBuild(() => mat.shader = newShader.shader);
            mat.enableInstancing = false;
            mat.Dirty();
        }

        public class PatchResult {
            public Shader shader;
            public int patchedPrograms;
            public Shader source;
            public string name;
            public string path;
            public string contents;
        }

        internal void PrepareBatch(IEnumerable<(Shader shader, bool hasBlendshapes)> shaders, bool keepImports) {
            var requests = new Dictionary<
                (Shader shader, bool keepImports, bool hasBlendshapes),
                (PatchResult root, List<PatchResult> imports)
            >();
            foreach (var (shader, hasBlendshapes) in shaders.Distinct()) {
                if (shader == null || shader.name == "VRChat/Mobile/Particles/Additive") continue;
                try {
                    SpsPlugReceiverService.RequireMaterialContacts();
                    var imports = new List<PatchResult>();
                    var root = PrepareShader(
                        shader,
                        keepImports,
                        hasBlendshapes,
                        null,
                        imports
                    );
                    requests[(shader, keepImports, hasBlendshapes)] = (root, imports);
                } catch (Exception) {
                    // Retrying in the material pass includes the plug, material, and clip in the error.
                }
            }
            var batchImported = false;
            try {
                ImportShaderBatch(requests.Values.SelectMany(request => request.imports).ToList());
                batchImported = true;
            } catch (Exception) {
                // Keep the generated sources for a serial retry with the material's error context.
            }
            foreach (var (key, patch) in requests) {
                preparedShaders[key] = (patch.root, patch.imports, batchImported);
            }
        }

        private PatchResult PatchUnsafe(
            Shader shader,
            bool keepImports,
            bool hasBlendshapes
        ) {
            var key = (shader, keepImports, hasBlendshapes);
            if (!preparedShaders.TryGetValue(key, out var prepared)) {
                var imports = new List<PatchResult>();
                var root = PrepareShader(
                    shader,
                    keepImports,
                    hasBlendshapes,
                    null,
                    imports
                );
                prepared = (root, imports, false);
            }
            if (prepared.root.shader != null) return prepared.root;

            // Unity can finish importing the batch and still return a missing or broken shader.
            // Retry the entire request in dependency order so the parent sees every UsePass target.
            if (!prepared.batchImported || !prepared.imports.All(patch => TryLoadPatchedShader(patch))) {
                ImportShadersInOrder(prepared.imports);
            }
            foreach (var patch in prepared.imports) patch.contents = null;
            preparedShaders[key] = prepared;
            return prepared.root;
        }

        private PatchResult PrepareShader(
            Shader shader,
            bool keepImports,
            bool hasBlendshapes,
            string parentHash,
            List<PatchResult> pending,
            HashSet<string> requiredPasses = null
        ) {
            var pathToSps = GetPathToSps();
            var sourcePath = ResolveShaderSource(shader);
            var hash = GetPatchHash(
                sourcePath,
                pathToSps,
                keepImports,
                hasBlendshapes,
                parentHash,
                requiredPasses
            );

            string newShaderName;
            if (shader.name.StartsWith("Hidden/Locked/")) {
                // Special case for Poiyomi
                // This prevents Poiyomi from complaining that the mat isn't locked and bailing on the build
                newShaderName = LockedShaderNamePrefix + hash;
            } else {
                newShaderName = ShaderNamePrefix + hash;
            }
            var alreadyExists = Shader.Find(newShaderName);
            if (alreadyExists != null && !ShaderUtil.ShaderHasError(alreadyExists)) {
                return new PatchResult {
                    shader = alreadyExists,
                    patchedPrograms = 0,
                    name = newShaderName
                };
            }

            var contents = ReadFile(sourcePath, true);
            if (requiredPasses != null) contents = KeepOnlyRequiredPasses(shader, contents, requiredPasses);
            contents = ApplyPcssFix(shader, contents);
            contents = DisableLegacyDeformation(contents);

            if (contents.Contains("_SPS_Bake")) {
                throw new Exception("Shader appears to already be patched, which should be impossible");
            }

            var properties = parentHash == null ? ReadSpsSource($"{pathToSps}/deform/sps_props.cginc") : null;
            contents = RewriteShaderHeader(contents, newShaderName, properties);

            string spsMain;
            if (keepImports) {
                spsMain = $"{(hasBlendshapes ? "#define SPS_HAS_BLENDSHAPES\n" : "")}#include \"{pathToSps}/deform/sps_main.cginc\"";
            } else {
                spsMain = (hasBlendshapes ? "#define SPS_HAS_BLENDSHAPES\n" : "")
                    + ReadSpsSource($"{pathToSps}/deform/sps_main.cginc");
            }
            
            var cgIncludes = "";
            WithEachCgInclude(contents, include => {
                cgIncludes += include + "\n";
            });

            var patchedPrograms = 0;
            var passNum = 0;
            contents = WithEachSubShader(contents, subShader => {
                if (IsUrpSubShader(subShader)) return subShader;
                return WithEachPass(subShader,
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
            });
            if (contents.Contains("UsePass")) {
                var usePass = GetRegex(@"(?:^|\n)[ \t]*UsePass[ \t]+""([^""]+)/([^""/]+)""");
                Shader FindIncludedShader(string shaderName) {
                    var includedShader = Shader.Find(shaderName);
                    if (!includedShader) {
                        throw new Exception("Failed to find included shader: " + shaderName);
                    }
                    return includedShader;
                }

                var usedPasses = usePass.Matches(contents).Cast<Match>().ToLookup(
                    match => FindIncludedShader(match.Groups[1].Value),
                    match => match.Groups[2].Value
                );
                var childShaders = new Dictionary<Shader, PatchResult>();
                foreach (var passes in usedPasses) {
                    var output = PrepareShader(
                        passes.Key,
                        keepImports,
                        hasBlendshapes,
                        hash,
                        pending,
                        passes.ToHashSet(StringComparer.OrdinalIgnoreCase)
                    );
                    patchedPrograms += output.patchedPrograms;
                    childShaders[passes.Key] = output;
                }
                if (childShaders.Count > 0) {
                    contents = usePass.Replace(contents, match => {
                        var child = childShaders[FindIncludedShader(match.Groups[1].Value)];
                        return $"\nUsePass \"{child.name}/{match.Groups[2].Value}\"\n";
                    });
                }
            }
            if (patchedPrograms == 0) {
                throw new Exception($"No programs found");
            }

            var patch = new PatchResult {
                source = shader,
                name = newShaderName,
                path = $"{TmpFilePackage.GetPath()}/SPS/{hash}.shader",
                contents = contents,
                patchedPrograms = patchedPrograms
            };
            pending.Add(patch);
            return patch;
        }

        private static string RewriteShaderHeader(string contents, string newName, string properties) {
            var declaration = GetRegex(
                @"\A" + ShaderLabTrivia + @"Shader\b" + ShaderLabTrivia
                + @"""(?<name>[^""]*)""" + ShaderLabTrivia + @"\{"
            ).Match(contents);
            if (!declaration.Success) throw new VRCFBuilderException("Failed to find the shader declaration.");

            if (properties != null) {
                var block = GetRegex(ShaderLabComment + @"|""(?:\\.|[^""\\])*""|(?<block>\bProperties\s*\{)")
                    .Matches(contents, declaration.Length)
                    .Cast<Match>()
                    .FirstOrDefault(match => match.Groups["block"].Success);
                // ShaderLab also allows shaders without a Properties block.
                contents = block == null
                    ? contents.Insert(declaration.Length, $"\nProperties {{\n{properties}\n}}\n")
                    : contents.Insert(block.Index + block.Length, $"\n{properties}\n");
            }

            var name = declaration.Groups["name"];
            return contents.Remove(name.Index, name.Length).Insert(name.Index, newName);
        }

        private static string KeepOnlyRequiredPasses(Shader shader, string contents, HashSet<string> requiredPasses) {
            // This copy is private to its UsePass consumer. Keep more complex layouts intact
            // because other subshaders and generated passes can determine pass availability.
            var shaderData = ShaderUtil.GetShaderData(shader);
            if (shaderData.SerializedSubshaderCount != 1) return contents;
            var subShaderData = shaderData.GetSerializedSubshader(0);
            var passCount = subShaderData.PassCount;
            var passIndex = 0;
            var subShaderCount = 0;
            var canTrim = true;
            var hasPatchedPass = false;
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var name = GetRegex(@"(?s:/\*.*?\*/)|//[^\r\n]*|(?:^|\n)[ \t]*Name[ \t]+""(?<name>[^""\r\n]+)""");
            var program = GetRegex(@"(?:^|\n)[ \t]*(?:CG|HLSL)PROGRAM\b");
            var usePass = GetRegex(@"(?:^|\n)[ \t]*UsePass\b");
            var trimmed = WithEachSubShader(contents, subShader => {
                if (++subShaderCount > 1 || IsUrpSubShader(subShader)) return subShader;
                return WithEachPass(
                    subShader,
                    pass => {
                        var passName = name.Matches(pass).Cast<Match>()
                            .FirstOrDefault(match => match.Groups["name"].Success)?.Groups["name"].Value;
                        if (passIndex >= passCount || !string.Equals(
                                passName,
                                subShaderData.GetPass(passIndex++).Name,
                                StringComparison.OrdinalIgnoreCase
                            )) {
                            canTrim = false;
                            return pass;
                        }
                        if (!requiredPasses.Contains(passName)) return null;
                        found.Add(passName);
                        hasPatchedPass |= program.IsMatch(pass) && !SkippedLightModeRegex.IsMatch(pass);
                        return pass;
                    },
                    rest => {
                        canTrim &= !program.IsMatch(rest) && !usePass.IsMatch(rest);
                        return rest;
                    }
                );
            });
            return subShaderCount == 1 && passIndex == passCount && canTrim && hasPatchedPass && found.SetEquals(requiredPasses)
                ? trimmed : contents;
        }

        /**
         * Queues the whole batch in its own asset-editing scope so Unity can import all the
         * shaders together before the avatar build resumes.
         */
        private static void ImportShaderBatch(List<PatchResult> pending) {
            if (pending.Count == 0) return;
            VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                VRCFuryAssetDatabase.WithAssetEditing(() => {
                    VRCFuryAssetDatabase.CreateFolder(VRCFuryAssetDatabase.GetDirectoryName(pending[0].path));
                    foreach (var patch in pending) WriteFile(patch.path, patch.contents);
                    foreach (var patch in pending) AssetDatabase.ImportAsset(patch.path, ImportAssetOptions.ForceSynchronousImport);
                });
            });
        }

        /**
         * Imports shaders individually for a standalone patch or a failed batch, validating each
         * UsePass dependency before importing the shader that refers to it.
         */
        private static void ImportShadersInOrder(List<PatchResult> pending) {
            VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                VRCFuryAssetDatabase.CreateFolder(VRCFuryAssetDatabase.GetDirectoryName(pending[0].path));
                foreach (var patch in pending) {
                    WriteFile(patch.path, patch.contents);
                    AssetDatabase.ImportAsset(patch.path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    TryLoadPatchedShader(patch, reportErrors: true);
                }
            });
        }

        private static bool TryLoadPatchedShader(PatchResult patch, bool reportErrors = false) {
            var newShader = AssetDatabase.LoadAssetAtPath<Shader>(patch.path);
            var hasErrors = newShader && ShaderUtil.ShaderHasError(newShader);
            if (newShader && !hasErrors && newShader.name == patch.name) {
                patch.shader = newShader;
                return true;
            }
            if (!reportErrors) return false;
            if (!newShader) {
                throw new VRCFBuilderException("Patch succeeded, but shader failed to generate. Check the Unity log for compiler errors.\n\n" + patch.path);
            }
            if (!hasErrors) {
                throw new VRCFBuilderException($"Expected generated shader '{patch.name}', but Unity imported '{newShader.name}' at {patch.path}.");
            }

            var shader = patch.source;
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

        private (string,int) PatchPass(
            string pass,
            string spsMain,
            string cgIncludes,
            bool isSurfaceShader
        ) {
            if (SkippedLightModeRegex.IsMatch(pass)) {
                return (pass, 0);
            }

            var patchedPrograms = 0;
            pass = WithEachProgram(pass, (program, isCgProgram) => {
                patchedPrograms++;
                try {
                    return PatchProgramWithIncludes(program, isCgProgram, spsMain, cgIncludes, isSurfaceShader);
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to patch program #{patchedPrograms}", e);
                }
            });

            return (pass, patchedPrograms);
        }

        private static string StripBuiltInShaderGraphVariants(string program) {
            if (!program.Contains("BUILTIN_TARGET_API")) return program;
            if (!GetRegex(@"(?m)^[ \t]*#define[ \t]+BUILTIN_TARGET_API[ \t]+1\b").IsMatch(program)) return program;

            // Shader Graph emits SRP lighting keywords even for the Built-In pipeline.
            // Built-In uses its own fwdbase/fwdadd/shadowcaster variants. Keep those,
            // the shared lightmap keywords, and all user-authored Graph Keywords.
            return GetRegex(@"(?ms)^([ \t]*// Keywords\r?\n)(.*?)(?=^[ \t]*// GraphKeywords\b)")
                .Replace(program, block => block.Groups[1].Value + GetRegex(
                    @"(?m)^[ \t]*#pragma[ \t]+multi_compile[ \t]+(?:"
                    + @"_[ \t]+(?:_SCREEN_SPACE_OCCLUSION|_ADDITIONAL_LIGHT_SHADOWS|_SHADOWS_SOFT)"
                    + @"|_[ \t]+_MAIN_LIGHT_SHADOWS[ \t]+_MAIN_LIGHT_SHADOWS_CASCADE[ \t]+_MAIN_LIGHT_SHADOWS_SCREEN"
                    + @"|_ADDITIONAL_LIGHTS_VERTEX[ \t]+_ADDITIONAL_LIGHTS[ \t]+_ADDITIONAL_OFF"
                    + @")[ \t]*\r?\n"
                ).Replace(block.Groups[2].Value, ""));
        }

        internal static string PatchProgram(
            string originalProgram,
            bool isCgProgram,
            string spsMain,
            string cgIncludes,
            bool isSurfaceShader
        ) => new SpsPatcher().PatchProgramWithIncludes(originalProgram, isCgProgram, spsMain, cgIncludes, isSurfaceShader);

        private string PatchProgramWithIncludes(
            string originalProgram,
            bool isCgProgram,
            string spsMain,
            string cgIncludes,
            bool isSurfaceShader
        ) {
            // The native contact path uses dynamic indexing/loops and integer bit fields.
            // Low-target shaders otherwise try to unroll the entire path builder.
            var target = GetRegex(@"(#pragma[ \t]+target[ \t]+)([0-9.]+)");
            var program = target.Replace(originalProgram, match =>
                float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) < 4.5f
                    ? match.Groups[1].Value + "4.5" : match.Value);
            program = StripBuiltInShaderGraphVariants(program);
            if (!target.IsMatch(program)) program = "#pragma target 4.5\n" + program;
            // The patched material disables ordinary GPU instancing. Single-pass stereo has its own keyword.
            if (!isSurfaceShader) program = "#pragma skip_variants INSTANCING_ON\n" + program;
            var newVertFunctionName = "spsVert";
            var pragmaKeyword = isSurfaceShader ? "surface" : "vertex";
            string vanillaVertFunctionName = null;
            var pragmaFound = false;
            program = GetRegex(@"(#pragma[ \t]+" + pragmaKeyword + @"[ \t]+)([^\s]+)([^\n]*)").Replace(program, match => {
                string newPragma;
                if (isSurfaceShader) {
                    var pragmaSuffix = match.Groups[3].Value;
                    var comment = pragmaSuffix.IndexOf("//", StringComparison.Ordinal);
                    if (comment >= 0) pragmaSuffix = pragmaSuffix.Substring(0, comment);
                    pragmaSuffix = pragmaSuffix.TrimEnd();
                    // Surface shaders generate their own instancing directives. Disable them at the source.
                    if (!Regex.IsMatch(pragmaSuffix, @"(?:^|\s)noinstancing(?:\s|$)")) pragmaSuffix += " noinstancing";
                    pragmaSuffix = GetRegex(@"vertex:(\S+)").Replace(pragmaSuffix, vertMatch => {
                        vanillaVertFunctionName = vertMatch.Groups[1].ToString();
                        return "vertex:" + newVertFunctionName;
                    });
                    if (vanillaVertFunctionName == null) {
                        pragmaSuffix += $" vertex:{newVertFunctionName}";
                    }
                    newPragma = $"{match.Groups[1]}{match.Groups[2]}{pragmaSuffix}";
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

            var flattenedProgram = FlattenProgram(program, isCgProgram, cgIncludes, isSurfaceShader);

            string returnTypeName;
            string returnSemantic = null;
            ParsedParamList parsedParams;
            var structBodies = new Dictionary<string, string>();

            string GetStructBody(string typeName) {
                if (structBodies.TryGetValue(typeName, out var body)) return body;
                var structMatch = GetRegex(@"struct\s+" + Regex.Escape(typeName) + @"\s*{")
                    .Match(flattenedProgram);
                if (!structMatch.Success) return structBodies[typeName] = null;
                var start = structMatch.Index + structMatch.Length;
                var end = IndexOfEndOfNextContext(flattenedProgram, structMatch.Index);
                return structBodies[typeName] = flattenedProgram.Substring(start, end - start);
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
                var name = desired.MakeUnique(usedNames);
                usedNames.Add(name);
                return name;
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
            var nextSearch = 0;
            while (FindProgramMarker(content, "CGINCLUDE", nextSearch) is { } include) {
                var end = FindProgramMarker(content, "ENDCG", include.after)
                    ?? throw new Exception("Failed to find CGINCLUDE end marker");
                withInclude(content.Substring(include.after, end.before - include.after));
                nextSearch = end.before;
            }
        }
        
        internal static string WithEachProgram(string content, Func<string, bool, string> withProgram) {
            StringBuilder output = null;
            var nextSearch = 0;
            while (true) {
                var cg = FindProgramMarker(content, "CGPROGRAM", nextSearch);
                var hlsl = FindProgramMarker(content, "HLSLPROGRAM", nextSearch);
                if (cg == null && hlsl == null) break;

                var isCg = cg != null && (hlsl == null || cg.Value.before < hlsl.Value.before);
                var start = (isCg ? cg : hlsl).Value.after;
                var end = FindProgramMarker(content, isCg ? "ENDCG" : "ENDHLSL", start)
                    ?? throw new Exception($"Failed to find {(isCg ? "CGPROGRAM" : "HLSLPROGRAM")} end marker");
                output ??= new StringBuilder(content.Length);
                output.Append(content, nextSearch, start - nextSearch);
                output.Append(withProgram(content.Substring(start, end.before - start), isCg));
                nextSearch = end.before;
            }
            return output?.Append(content, nextSearch, content.Length - nextSearch).ToString() ?? content;
        }

        // Include the surrounding line feeds so callers keep exactly the same program text.
        private static (int before, int after)? FindProgramMarker(string source, string marker, int start) {
            var search = start;
            while (true) {
                var index = source.IndexOf(marker, search, StringComparison.Ordinal);
                if (index < 0) return null;
                search = index + marker.Length;
                var before = index;
                while (before > start && (source[before - 1] == ' ' || source[before - 1] == '\t')) before--;
                if (before > start && source[before - 1] == '\n') before--;
                if (before != 0 && source[before] != '\n') continue;

                var after = ProgramMarkerEnd(source, search);
                if (after >= 0) return (before, after);
            }
        }

        private static int ProgramMarkerEnd(string source, int index) {
            while (index < source.Length && (source[index] == ' ' || source[index] == '\t')) index++;
            if (index == source.Length) return index;
            return source[index] == '\n' ? index + 1 : -1;
        }

        internal static string WithEachPass(string content, Func<string, string> withPass, Func<string, string> withRest) {
            return WithEachBlock(content, "Pass", withPass, withRest);
        }

        internal static bool IsUrpSubShader(string subShader) {
            return GetRegex(@"""RenderPipeline""\s*=\s*""UniversalPipeline""").IsMatch(subShader);
        }

        internal static string WithEachSubShader(string content, Func<string, string> withSubShader) {
            return WithEachBlock(content, "SubShader", withSubShader);
        }

        private static string WithEachBlock(
            string content,
            string blockName,
            Func<string, string> withBlock,
            Func<string, string> withRest = null
        ) {
            var output = "";
            var lastBlockEnd = 0;
            var processedBlocks = new List<string>();
            while (true) {
                var nextBlockStart = GetRegex(
                    @"(?:^|\n)[ \t]*" + Regex.Escape(blockName) + @"(?:[ \t]|{)*[ \t]*(?:\n|$)"
                ).Match(content, lastBlockEnd);
                if (nextBlockStart.Success) {
                    var start = nextBlockStart.Index + nextBlockStart.Length;
                    var end = IndexOfEndOfNextContext(content, nextBlockStart.Index);
                    var oldBlock = content.Substring(start, end - start);
                    var newBlock = withBlock(oldBlock);
                    if (newBlock == null) {
                        output += content.Substring(lastBlockEnd, nextBlockStart.Index - lastBlockEnd);
                        lastBlockEnd = end + 1;
                        continue;
                    }
                    output += content.Substring(lastBlockEnd, start - lastBlockEnd);
                    output += $"\n__BLOCK_{processedBlocks.Count}__\n";
                    processedBlocks.Add(newBlock);
                    lastBlockEnd = end;
                } else {
                    output += content.Substring(lastBlockEnd);
                    break;
                }
            }

            if (withRest != null) output = withRest(output);
            for (var i = 0; i < processedBlocks.Count; i++) {
                output = output.Replace($"__BLOCK_{i}__", processedBlocks[i]);
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

                if ((i == start || c == '\n') && TrySkipProgram(str, i, out var endOfProgram)) {
                    i = endOfProgram;
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

        private static bool TrySkipProgram(string str, int start, out int end) {
            end = 0;
            var first = str[start] == '\n' ? start + 1 : start;
            while (first < str.Length && (str[first] == ' ' || str[first] == '\t')) first++;
            if (first >= str.Length || str[first] != 'C' && str[first] != 'H') return false;
            var isCg = str[first] == 'C';
            var marker = isCg ? "CGPROGRAM" : "HLSLPROGRAM";
            if (string.CompareOrdinal(str, first, marker, 0, marker.Length) != 0) return false;
            var next = ProgramMarkerEnd(str, first + marker.Length);
            if (next < 0) return false;
            var endMarker = isCg ? "ENDCG" : "ENDHLSL";
            var programEnd = FindProgramMarker(str, endMarker, next)
                ?? throw new Exception($"Failed to find {endMarker} end marker");
            end = programEnd.after - 1;
            return true;
        }

        private static string ResolveIncludePath(string path, string filePath, bool includeLibraryFiles) {
            if (File.Exists(path)) return path;
            if (path.StartsWith("/")) path = path.Substring(1);
            if (filePath != null) {
                var relativePath = path;
                var directory = VRCFuryAssetDatabase.GetDirectoryName(filePath);
                while (relativePath.StartsWith("..")) {
                    directory = VRCFuryAssetDatabase.GetDirectoryName(directory);
                    relativePath = relativePath.Substring(3);
                }
                var resolved = Path.Combine(directory, relativePath);
                if (File.Exists(resolved)) return resolved;
            }
            if (!includeLibraryFiles || path.Contains("..")) return null;

            var editorPath = EditorApplication.applicationContentsPath;
            var libraryPath = Path.Combine(editorPath, "CGIncludes", path);
            if (File.Exists(libraryPath)) return libraryPath;
            // Moved to a new folder near Unity 6.4.
            libraryPath = Path.Combine(editorPath, "Resources", "CGIncludes", path);
            return File.Exists(libraryPath) ? libraryPath : null;
        }

        internal static string DisableLegacyDeformation(string content) {
            var hasLegacyKeywords = content.Contains("TPS_Penetrator") || content.Contains("RALIV_PENETRATION");
            // Poiyomi's optimizer retains enabled #ifdef branches even when it bakes properties into constants.
            const string disableKeywords = "\n#undef TPS_Penetrator\n#undef RALIV_PENETRATION\n";
            // Locate the header keywords first so large shaders need only a few line-prefix checks.
            content = GetRegex(@"(?:CG|HLSL)(?:PROGRAM|INCLUDE)[ \t]*\r?\n").Replace(content, header => {
                for (var index = header.Index - 1; index >= 0 && content[index] != '\n'; index--) {
                    if (!char.IsWhiteSpace(content[index])) return header.Value;
                }
                return header.Value + disableKeywords;
            });
            if (hasLegacyKeywords) {
                content = GetRegex(@"(?m)^[ \t]*#define[ \t]+(TPS_Penetrator|RALIV_PENETRATION)\b[^\r\n]*")
                    .Replace(content, "#undef $1");
            }

            // These includes contain DPS deformation and its helpers, without unrelated inout functions.
            // Make their parameters input-only so deformation cannot modify the caller's vertex/normal.
            if (content.Contains("RalivDPS_Functions.cginc")
                || content.Contains("XSVert.cginc")
                || content.Contains("PenetratorFunctions.cginc")) {
                content = GetRegex(@"(?m)^[ \t]*#(?:include|include_with_pragmas)[ \t]+""(?:[^""\r\n]*[/\\])?(?:RalivDPS_Functions|XSVert|PenetratorFunctions)\.cginc""[^\r\n]*")
                    .Replace(content, match => "\n#define inout in\n" + match.Value + "\n#undef inout\n");
            }

            // Locked/combined shaders can contain these definitions directly instead of including them.
            if (!content.Contains("PenetratorReshape")
                && !content.Contains("applyRalivDynamicPenetrationSystem")
                && !content.Contains("ApplyTPSPenetrator")) return content;
            var functions = GetRegex(@"(?s:/\*.*?\*/)|//[^\r\n]*|""(?:\\.|[^""\\])*""|(?<function>\bvoid\s+(?:PenetratorReshape|applyRalivDynamicPenetrationSystem|ApplyTPSPenetrator)\s*\([^;{}]*\)\s*\{)");
            foreach (var match in functions.Matches(content).Cast<Match>()
                         .Where(m => m.Groups["function"].Success).Reverse()) {
                var close = IndexOfEndOfNextContext(content, match.Index + match.Length - 1);
                content = content.Substring(0, close + 1) + "\n#undef inout\n" + content.Substring(close + 1);
                content = content.Substring(0, match.Index) + "\n#define inout in\n" + content.Substring(match.Index);
            }
            return content;
        }

        private string ReadSpsSource(string path) => flattenedSpsFiles.GetOrCreate(
            path,
            () => ReadAndFlattenContent(ReadFile(path), firstPath: path)
        );

        internal static IEnumerable<Match> FindIncludes(string source) {
            var next = 0;
            while (true) {
                var marker = source.IndexOf("#include", next, StringComparison.Ordinal);
                if (marker < 0) yield break;
                next = marker + "#include".Length;

                // Start at the earliest line break matching the directive's leading whitespace.
                // Searching for the literal first avoids running the regex on unrelated lines.
                var anchor = marker;
                while (anchor > 0 && char.IsWhiteSpace(source[anchor - 1])) anchor--;
                if (anchor > 0) {
                    anchor = source.IndexOf('\n', anchor, marker - anchor);
                    if (anchor < 0) continue;
                }

                var match = IncludeRegex.Match(source, anchor);
                if (!match.Success) continue;
                yield return match;
                next = match.Index + match.Length;
            }
        }

        private sealed class IncludeContent {
            public readonly string source;
            public readonly List<(int start, int end, string path)> includes = new();

            public IncludeContent(string source, bool includeLibraryFiles) {
                this.source = source;
                foreach (var match in FindIncludes(source)) {
                    var path = ResolveIncludePath(match.Groups[2].Value, null, includeLibraryFiles);
                    if (path != null) includes.Add((match.Index, match.Index + match.Length, path));
                }
            }
        }

        private string FlattenProgram(string program, bool isCgProgram, string cgIncludes, bool isSurfaceShader) {
            if (!isCgProgram) return ReadAndFlattenContent(program, includeFiles, includeLibraryFiles: true);
            if (isSurfaceShader) {
                // A surface program may start with an include that follows the header's trailing whitespace.
                var source = ImplicitCgIncludes + "#include \"Lighting.cginc\"\n" + cgIncludes + "\n" + program;
                return ReadAndFlattenContent(source, includeFiles, includeLibraryFiles: true);
            }

            var header = flattenedCgIncludes.GetOrCreate(cgIncludes, () => {
                var visited = new HashSet<string>();
                var source = ReadAndFlattenContent(
                    ImplicitCgIncludes + cgIncludes + "\n",
                    includeFiles,
                    includeLibraryFiles: true,
                    visited: visited
                );
                return (source, visited);
            });
            return header.source + ReadAndFlattenContent(
                program,
                includeFiles,
                includeLibraryFiles: true,
                visited: new HashSet<string>(header.visited)
            );
        }

        private static string ReadAndFlattenContent(
            string content,
            Dictionary<string, IncludeContent> includeFiles = null,
            bool includeLibraryFiles = false,
            string firstPath = null,
            HashSet<string> visited = null
        ) {
            visited ??= new HashSet<string>();
            if (firstPath != null) visited.Add(firstPath);
            // Share parsed includes across the passes of this shader. Each program still tracks
            // which files it has already expanded, since its include order can differ.
            includeFiles ??= new Dictionary<string, IncludeContent>();
            var output = new StringBuilder(content.Length);

            void AppendWithIncludes(IncludeContent program) {
                var previousEnd = 0;
                foreach (var (start, end, path) in program.includes) {
                    output.Append(program.source, previousEnd, start - previousEnd).Append('\n');
                    if (visited.Add(path)) {
                        if (!includeFiles.TryGetValue(path, out var resolved)) {
                            includeFiles[path] = resolved = new IncludeContent(ReadFile(path), includeLibraryFiles);
                        }
                        AppendWithIncludes(resolved);
                    }
                    output.Append('\n');
                    previousEnd = end;
                }
                output.Append(program.source, previousEnd, program.source.Length - previousEnd);
            }

            AppendWithIncludes(new IncludeContent(content, includeLibraryFiles));
            return output.ToString();
        }

        private static string GetPathToSps() {
            var path = AssetDatabase.GUIDToAssetPath("6cf9adf85849489b97305dfeecc74768");
            if (string.IsNullOrWhiteSpace(path)) {
                throw new Exception(
                    "Failed to find the file path to SPS includes. This usually means the unity asset database is confused. Try removing and then re-adding VRCFury.");
            }
            return path;
        }
        private static string GetPatchHash(
            string sourcePath,
            string pathToSps,
            bool keepImports,
            bool hasBlendshapes,
            string parentHash,
            HashSet<string> requiredPasses
        ) {
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
                // A UsePass edit can change the shader without changing the length of the source file.
                if (File.Exists(sourcePath)) {
                    using var source = File.OpenRead(sourcePath);
                    Add(Convert.ToBase64String(md5.ComputeHash(source)));
                }
                if (parentHash == null) AddFile($"{pathToSps}/deform/sps_props.cginc");
                AddFile($"{pathToSps}/deform/sps_main.cginc");
                if (requiredPasses != null) {
                    foreach (var pass in requiredPasses.OrderBy(pass => pass, StringComparer.OrdinalIgnoreCase)) {
                        Add(pass.ToUpperInvariant());
                    }
                }

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
                } else if (path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase)) {
                    if (!ReflectionHelper.IsReady<ShaderGraphReflection>()) {
                        throw new Exception("Failed to access Shader Graph's shader source generator: " + path);
                    }
                    content = (string)ShaderGraphReflection.GetShaderText.Invoke(null, new object[] { path, null, null, null });
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

            StringBuilder rewritten = null;
            var copiedThrough = 0;
            foreach (var match in FindIncludes(content)) {
                var resolved = ResolveIncludePath(match.Groups[2].Value, path, false);
                if (resolved == null) continue;
                if (resolved.Contains("'")) {
                    throw new Exception(
                        "A unity bug prevents SPS from including shaders stored in a folder with a ' in the name. " +
                        "Please rename the folder to remove the quote symbol: " + resolved);
                }
                rewritten ??= new StringBuilder(content.Length);
                rewritten.Append(content, copiedThrough, match.Index - copiedThrough)
                    .Append('\n').Append(match.Groups[1].Value).Append(resolved).Append(match.Groups[3].Value).Append('\n');
                copiedThrough = match.Index + match.Length;
            }
            if (rewritten != null) {
                content = rewritten.Append(content, copiedThrough, content.Length - copiedThrough).ToString();
            }
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
