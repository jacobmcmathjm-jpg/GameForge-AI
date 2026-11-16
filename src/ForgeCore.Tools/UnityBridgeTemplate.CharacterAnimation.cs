namespace ForgeCore.Tools;
internal static partial class UnityBridgeTemplate
{
    private const string CharacterAnimationBridgeSource = """
#if UNITY_EDITOR
namespace ForgeCore.Editor
{
    public static partial class ForgeCoreBridge
    {
        private static string PrepareCharacterAnimation(BridgeRequest request)
        {
            if(string.IsNullOrEmpty(request.assetSetId)||!Regex.IsMatch(request.assetSetId,"^[A-Za-z0-9][A-Za-z0-9._-]{1,95}$",RegexOptions.CultureInvariant)||request.assetSetId.Contains(".."))
                throw new InvalidOperationException("Invalid animation asset set.");
            GeneratedAssetAbsolutePath(request.assetPath);AssetAbsolutePath(request.scenePath);PreviewAbsolutePath(request.outputPath);
            if(string.IsNullOrEmpty(request.assetSha256)||!Regex.IsMatch(request.assetSha256,"^[a-f0-9]{64}$",RegexOptions.CultureInvariant))throw new InvalidOperationException("Exact source hash required.");
            Type builder=null;
            var typeName=request.operation=="create_industrial_mission"?"ForgeCoreGameTemplates.Editor.ForgeCoreIndustrialMissionBuilder":
                request.operation=="create_security_combat_lab" ? "ForgeCoreGameTemplates.Editor.ForgeCoreSecurityCombatBuilder" : "ForgeCoreGameTemplates.Editor.ForgeCoreCharacterAnimationBuilder";
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {builder=assembly.GetType(typeName,false,false);if(builder!=null)break;}
            if(builder==null)throw new InvalidOperationException("The verified animation builder is not compiled.");
            var connected=!string.IsNullOrEmpty(request.layoutProfile);
            if(connected&&(request.operation!="create_industrial_mission"||request.layoutProfile!="industrial_connected_v1"||!string.IsNullOrEmpty(request.styleFamily)))throw new InvalidOperationException("Unsupported mission layout or style combination.");
            var retro=request.operation=="create_industrial_mission"&&!string.IsNullOrEmpty(request.styleFamily);
            if(retro&&request.styleFamily!="retro_64")throw new InvalidOperationException("Unsupported mission style.");
            var types=retro?new[]{typeof(string),typeof(string),typeof(string),typeof(string),typeof(string),typeof(string)}:new[]{typeof(string),typeof(string),typeof(string),typeof(string),typeof(string)};
            var method=builder.GetMethod(connected?"BuildConnected":retro?"BuildWithStyle":"Build",BindingFlags.Public|BindingFlags.Static,null,types,null);
            if(method==null||method.ReturnType!=typeof(string))throw new InvalidOperationException("Animation builder contract mismatch.");
            try{return (string)method.Invoke(null,retro?new object[]{request.assetPath,request.assetSha256,request.assetSetId,request.scenePath,request.outputPath,request.styleFamily}:new object[]{request.assetPath,request.assetSha256,request.assetSetId,request.scenePath,request.outputPath});}
            catch(TargetInvocationException ex){throw new InvalidOperationException("Animation builder failed: "+(ex.InnerException==null?ex.Message:ex.InnerException.Message),ex.InnerException??ex);}
        }
    }
}
#endif
""";
}
