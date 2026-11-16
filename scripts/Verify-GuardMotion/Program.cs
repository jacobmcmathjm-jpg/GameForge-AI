using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using ForgeCore.Tools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// Read-only Unity assembly references; does not start Unity, alter its project or
// simulate import/physics/animation. Compiler evidence is not a runtime test pass.
if(args.Length!=2)throw new ArgumentException("Provide Unity Editor Data path and a fresh receipt path.");
var data=Path.GetFullPath(args[0]);var output=Path.GetFullPath(args[1]);
if(File.Exists(output)||Directory.Exists(output))throw new IOException("Receipt exists; no overwrite.");
var type=typeof(ToolExecutor).Assembly.GetType("ForgeCore.Tools.UnityGameTemplateKit",true)!;
var files=(IReadOnlyDictionary<string,string>)type.GetField("Files",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
var version=(string)type.GetField("Version",BindingFlags.Public|BindingFlags.Static)!.GetRawConstantValue()!;
var options=new CSharpParseOptions(LanguageVersion.CSharp9,preprocessorSymbols:["UNITY_EDITOR"]);
var parsed=files.Where(f=>f.Key.EndsWith(".cs",StringComparison.Ordinal)).ToDictionary(f=>f.Key,f=>CSharpSyntaxTree.ParseText(f.Value,options,f.Key));
var parseErrors=parsed.Values.SelectMany(t=>t.GetDiagnostics()).Where(d=>d.Severity==DiagnosticSeverity.Error).Select(d=>d.ToString()).ToArray();
var referencePaths=Directory.GetFiles(Path.Combine(data,"NetStandard/ref/2.1.0"),"*.dll")
    .Concat(Directory.GetFiles(Path.Combine(data,"Managed/UnityEngine"),"*.dll"))
    // UnityEditor.dll is a legacy monolithic reference; combining it with the
    // modular editor assemblies falsely reports duplicate definitions.
    .Concat(Directory.GetFiles(Path.Combine(data,"Managed"),"UnityEditor.*.dll")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
var references=referencePaths.Select(p=>MetadataReference.CreateFromFile(p)).ToList();
var reports=new List<object>();var failed=parseErrors.Length!=0;
MetadataReference? Compile(string name,IEnumerable<SyntaxTree> trees,IEnumerable<MetadataReference> refs)
{
    var compilation=CSharpCompilation.Create(name,trees,refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));
    using var assembly=new MemoryStream();var result=compilation.Emit(assembly);
    var errors=result.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error).Select(d=>d.ToString()).ToArray();
    reports.Add(new{name,success=result.Success,errors});failed|=!result.Success;
    Console.WriteLine(name+": "+(result.Success?"static compilation passed":string.Join("\n",errors.Take(10))));
    return result.Success?MetadataReference.CreateFromImage(assembly.ToArray()):null;
}
var runtime=Compile("ForgeCoreGameTemplates.Runtime",parsed.Where(p=>p.Key.StartsWith("Runtime/",StringComparison.Ordinal)).Select(p=>p.Value),references);
if(runtime!=null)_=Compile("ForgeCoreGameTemplates.Editor",parsed.Where(p=>p.Key.StartsWith("Editor/",StringComparison.Ordinal)).Select(p=>p.Value),references.Append(runtime));
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await using(var stream=new FileStream(output,FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{
    version,kind="static_unity_source_compilation",sourceFiles=parsed.Count,parseErrors,compilations=reports,
    files=files.Where(f=>f.Key.EndsWith(".cs",StringComparison.Ordinal)).Select(f=>new{path=f.Key,sha256=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(f.Value))).ToLowerInvariant()}),
    qualification="Roslyn C# 9 syntax and Runtime/Editor semantic compilation against installed Unity reference assemblies only. Tests parsed, not executed. No Unity editor/import/render/physics or live project write occurred."},new JsonSerializerOptions{WriteIndented=true});
return failed?1:0;
