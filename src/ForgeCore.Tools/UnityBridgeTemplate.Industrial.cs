namespace ForgeCore.Tools;
internal static partial class UnityBridgeTemplate
{
    private const string IndustrialBridgeSource = """
#if UNITY_EDITOR
namespace ForgeCore.Editor
{
    public static partial class ForgeCoreBridge
    {
        private static string PrepareIndustrialProp(BridgeRequest request)
        {
            GeneratedAssetAbsolutePath(request.assetPath);AssetAbsolutePath(request.scenePath);PreviewAbsolutePath(request.outputPath);
            if(string.IsNullOrEmpty(request.assetSetId)||!Regex.IsMatch(request.assetSetId,"^[a-z][a-z0-9_-]{1,47}$",RegexOptions.CultureInvariant))
                throw new InvalidOperationException("Industrial prop asset set must be a lowercase bounded identifier.");
            var builder=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("ForgeCoreGameTemplates.Editor.ForgeCoreIndustrialPropBuilder",false,false)).FirstOrDefault(t=>t!=null);
            if(builder==null)throw new InvalidOperationException("Verified industrial prop kit is not compiled.");
            var method=builder.GetMethod("Build",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(string),typeof(string),typeof(string),typeof(string),typeof(string)},null);
            if(method==null||method.ReturnType!=typeof(string))throw new InvalidOperationException("Industrial builder contract mismatch.");
            try{return (string)method.Invoke(null,new object[]{request.assetPath,request.assetSha256,request.assetSetId,request.scenePath,request.outputPath});}
            catch(TargetInvocationException e){throw new InvalidOperationException("Industrial prop builder failed: "+(e.InnerException??e).Message);}
        }
    }
}
#endif
""";
}
