using System.Text.Json;
using ForgeCore.Core;
using ForgeCore.Demo;
using ForgeCore.Security;
using ForgeCore.Tools;
namespace ForgeCore.Security.Tests;
public sealed partial class SecurityTests
{
    [Fact]public async Task BundledRecordingsRetainExactSourcesRightsMetadataAndRefuseModifiedPackOverwrite()
    {
        await CreateBareUnityProjectAsync();using var tools=new ToolExecutor(new WorkspacePathGuard(_root),[]);
        var call=ToolCall.Create(ToolKind.ScaffoldUnityGameTemplate,PermissionKind.Write,"source-bound recording candidate",("project_path","Game"),("template_id","first_person_action"));
        var installed=await tools.ExecuteAsync(call,default);Assert.True(installed.Success,installed.Error);
        var path=Path.Combine(_root,"Game/Assets/ForgeCoreGameTemplates/Editor/RecordedFootsteps.json");
        var text=await File.ReadAllTextAsync(path);using var json=JsonDocument.Parse(text);var pack=json.RootElement;
        Assert.Equal(RecordedFootstepBank.Id,pack.GetProperty("id").GetString());Assert.Equal(RecordedFootstepBank.License,pack.GetProperty("license").GetString());
        Assert.Equal(RecordedFootstepBank.SourcePage,pack.GetProperty("sourcePage").GetString());Assert.False(pack.GetProperty("humanListeningAccepted").GetBoolean());
        var entries=pack.GetProperty("files");Assert.Equal(6,entries.GetArrayLength());
        for(var i=0;i<6;i++){
            var bytes=Convert.FromBase64String(entries[i].GetProperty("base64").GetString()!);
            Assert.True(RecordedFootstepBank.MatchesSource(i,bytes));bytes[^1]^=1;Assert.False(RecordedFootstepBank.MatchesSource(i,bytes));
        }
        using var resource=typeof(ToolExecutor).Assembly.GetManifestResourceStream("ForgeCore.Tools.RecordedFootsteps.json");Assert.NotNull(resource);
        using var reader=new StreamReader(resource);Assert.Equal(text,await reader.ReadToEndAsync());
        var modified=text.Replace("CC0-1.0","Unreviewed",StringComparison.Ordinal);await File.WriteAllTextAsync(path,modified);
        var refused=await tools.ExecuteAsync(call,default);Assert.False(refused.Success);Assert.Equal(modified,await File.ReadAllTextAsync(path));
    }
}
