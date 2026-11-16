using System.Security.Cryptography;
using System.Text.Json;
using ForgeCore.Tools;
// Read-only replay of the exact app-used validator against the already generated v8 payload.
// No Unity execution, evidence registration, project writes, provider/model or live database.
const string project=@"D:\ForgeCore\Workspace\Games\ForgeCore",folder="Assets/ForgeCore/CharacterAnimations/connected-level-v8";
if(args.Length>1)throw new ArgumentException("At most one bounded attempt label required.");
var attempt=args.Length==1?args[0]:"first";if(!System.Text.RegularExpressions.Regex.IsMatch(attempt,"^[a-z0-9-]{1,24}$"))throw new ArgumentException("Invalid attempt label.");
var output=Path.Combine(Environment.CurrentDirectory,"work","connected-host-validation","actual-v8-artifact-validation-"+attempt+".json");
if(File.Exists(output))throw new IOException("Fresh validation receipt required.");
using var record=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(project,".forgecore","unity","character-animation-evidence","9dd9ec55c81849d58908efbc4d4d01be.json")));
var hashes=new Dictionary<string,string>();
foreach(var name in new[]{"connected-interior-report.json","connected-interior-review.json","ConnectedScreen.png","connected-interior-review.png"}){
    var relative=folder+"/"+name;
    var expected=record.RootElement.GetProperty("artifacts").EnumerateArray().Single(a=>a.GetProperty("path").GetString()==relative).GetProperty("sha256").GetString();
    var actual=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(project,relative)))).ToLowerInvariant();
    if(actual!=expected)throw new IOException("Actual v8 source differs from its original receipt.");hashes[relative]=actual;
}
var result=await ConnectedInteriorArtifactValidator.ValidateAsync(project,folder);
foreach(var pair in hashes)if(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(project,pair.Key)))).ToLowerInvariant()!=pair.Value)throw new IOException("Read-only validation changed an input.");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await using(var stream=new FileStream(output,FileMode.CreateNew))await JsonSerializer.SerializeAsync(stream,new{schemaVersion=1,kind="actual_v8_app_used_interior_artifact_validator",detailProfile=result.Detail.GetProperty("profile").GetString(),comparisonScope=result.Comparison.GetProperty("scope").GetString(),inputHashes=hashes,projectInputsUnchanged=true,projectWrites=false,unityStarted=false,evidenceRegistered=false,databaseOpened=false,providerOrModelStarted=false,humanArtAccepted=false,stageComplete=false,freezeFixed=false,qualification="The same read-only validator called by ToolExecutor preparation, run against four exact original v8 artifacts. JSON types/bounds/non-approval and PNG envelopes/dimensions only, not independent pixel/art quality or a new Unity generation."},new JsonSerializerOptions{WriteIndented=true});
Console.WriteLine("ACTUAL_V8_APP_VALIDATOR_PASS four exact inputs unchanged; no project writes or approval.");
