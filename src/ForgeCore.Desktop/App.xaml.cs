using System.Windows;
using ForgeCore.Agent;
using ForgeCore.Core;
using ForgeCore.LocalAI;
using ForgeCore.Memory;
using ForgeCore.Security;
using ForgeCore.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ForgeCore.Desktop;

public partial class App : Application
{
    private IHost? _host;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Deployment validation must not open the user's store, resume work, or load a model.
        if (e.Args.Length == 1 && e.Args[0] == "--verify-package")
        {
            Shutdown(PackageSelfCheck.Run(AppContext.BaseDirectory));
            return;
        }
        _host = Host.CreateDefaultBuilder(e.Args).ConfigureAppConfiguration(builder => builder
            .SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json", optional: false).AddEnvironmentVariables("FORGECORE_"))
            .ConfigureServices((context, services) => ConfigureServices(context.Configuration, services)).Build();
        await _host.StartAsync();
        await _host.Services.GetRequiredService<IForgeStore>().InitializeAsync();
        _host.Services.GetRequiredService<MainWindow>().Show();
    }

    private static void ConfigureServices(IConfiguration config, IServiceCollection services)
    {
        var forge = config.GetSection("ForgeCore").Get<ForgeCoreOptions>() ?? new();
        var local = config.GetSection("LocalModel").Get<LocalModelOptions>() ?? new();
        var guard = new WorkspacePathGuard(forge.WorkspaceRoot);
        services.AddSingleton(forge).AddSingleton(local).AddSingleton(guard)
            .AddSingleton<IGlobalCancellation, GlobalCancellation>()
            .AddSingleton<TaskAuthorizationSession>()
            .AddSingleton<IAutomaticApprovalGrant>(provider => provider.GetRequiredService<TaskAuthorizationSession>())
            .AddSingleton<ISystemMetrics, ProcessSystemMetrics>()
            .AddSingleton<ISecurityPolicy>(_ => new SecurityPolicy(guard, forge.AllowedCommands, forge.AllowedAssetHosts, forge.EnableOnlineCatalogSearch,
                forge.UnityEditorPath, forge.UnityEditorVersion, forge.EnableUnityMcp, forge.UnityMcpRelayPath,
                forge.AllowedUnityMcpTools, forge.MaxUnityMcpArgumentsCharacters,
                enableSeeleGeneration: forge.EnableSeeleGeneration,
                seeleApiKeyEnvironmentVariable: forge.SeeleApiKeyEnvironmentVariable,
                enableMeshyGeneration: forge.EnableMeshyGeneration,
                meshyApiKeyEnvironmentVariable: forge.MeshyApiKeyEnvironmentVariable,
                blenderPath: forge.BlenderPath, blenderVersion: forge.BlenderVersion))
            .AddSingleton<SqliteForgeStore>(_ => new SqliteForgeStore(forge.DatabasePath))
            .AddSingleton<IForgeStore>(provider => provider.GetRequiredService<SqliteForgeStore>())
            .AddSingleton<IProjectIndexStore>(provider => provider.GetRequiredService<SqliteForgeStore>())
            .AddSingleton<IApprovalInboxStore>(provider => provider.GetRequiredService<SqliteForgeStore>())
            .AddSingleton<IWorkQueueStore>(provider => provider.GetRequiredService<SqliteForgeStore>())
            .AddSingleton<IUnityGameplayEvidenceCatalog>(_ => new UnityGameplayEvidenceCatalog(guard, forge.UnityProjectPath))
            .AddSingleton<IToolExecutor>(provider => new ToolExecutor(guard, forge.AllowedCommands, forge.AllowedAssetHosts, forge.MaxAssetBytes,
                httpClient: null, dotnetPath: forge.DotnetPath, dotnetTimeoutSeconds: forge.DotnetTimeoutSeconds,
                maxProcessOutputCharacters: forge.MaxProcessOutputCharacters, projectIndex: provider.GetRequiredService<IProjectIndexStore>(),
                maxIndexFiles: forge.MaxIndexFiles, maxIndexFileBytes: forge.MaxIndexFileBytes, maxIndexTotalBytes: forge.MaxIndexTotalBytes,
                enableOnlineCatalogSearch: forge.EnableOnlineCatalogSearch, maxCatalogResults: forge.MaxCatalogResults,
                maxCatalogResponseBytes: forge.MaxCatalogResponseBytes, onlineResultCacheMinutes: forge.OnlineResultCacheMinutes,
                unityEditorPath: forge.UnityEditorPath, unityEditorVersion: forge.UnityEditorVersion, unityTimeoutSeconds: forge.UnityTimeoutSeconds,
                enableUnityMcp: forge.EnableUnityMcp, unityMcpRelayPath: forge.UnityMcpRelayPath,
                allowedUnityMcpTools: forge.AllowedUnityMcpTools, unityMcpTimeoutSeconds: forge.UnityMcpTimeoutSeconds,
                maxUnityMcpResponseCharacters: forge.MaxUnityMcpResponseCharacters,
                enableUnityProjectPackages: forge.EnableUnityProjectPackages,
                enableSeeleGeneration: forge.EnableSeeleGeneration,
                seeleApiKeyEnvironmentVariable: forge.SeeleApiKeyEnvironmentVariable,
                seeleGenerationTimeoutSeconds: forge.SeeleGenerationTimeoutSeconds,
                seelePollSeconds: forge.SeelePollSeconds,
                enableMeshyGeneration: forge.EnableMeshyGeneration,
                meshyApiKeyEnvironmentVariable: forge.MeshyApiKeyEnvironmentVariable,
                meshyGenerationTimeoutSeconds: forge.MeshyGenerationTimeoutSeconds,
                meshyPollSeconds: forge.MeshyPollSeconds,
                blenderPath: forge.BlenderPath, blenderVersion: forge.BlenderVersion, blenderTimeoutSeconds: forge.BlenderTimeoutSeconds))
            .AddSingleton<ILocalModel>(provider =>
            {
                ILocalModel baseModel;
                if (!local.Provider.Equals("LLamaSharp", StringComparison.OrdinalIgnoreCase)) baseModel = new MockLocalModel();
                else
                {
                    var backend = LocalInferenceBackendFactory.Create(local, (path, options) =>
                        new LlamaSharpInferenceBackend(path, options, provider.GetRequiredService<ILogger<LlamaSharpInferenceBackend>>()));
                    baseModel = new LlamaSharpLocalModel(backend, local);
                }
                return new BuiltInBlenderAssetModel(new BuiltInSupervised3DModel(new BuiltInUnityDiagnosticsModel(baseModel, forge.UnityProjectPath),
                    forge.UnityProjectPath, forge.Preferred3DGenerationProvider), forge.UnityProjectPath);
            })
            .AddSingleton(new AgentOptions
            {
                MaxActions = forge.MaxActions,
                MaxRetries = forge.MaxRetries,
                MaxHistoryMessages = local.MaxHistoryMessages,
                MaxPlanningIterations = forge.MaxPlanningIterations,
                MaxRenderedToolResultCharacters = forge.MaxRenderedToolResultCharacters
            })
            .AddSingleton<WpfApprovalService>()
            .AddSingleton<IApprovalService>(provider => provider.GetRequiredService<WpfApprovalService>())
            .AddSingleton<IWorkQueueService, WorkQueueService>()
            .AddSingleton(new GameProductionOptions { ProjectPath = forge.UnityProjectPath })
            .AddSingleton<IGameProductionService, GameProductionService>()
            .AddSingleton<IApprovalRecoveryService, ApprovalRecoveryService>()
            .AddSingleton<IAgentRunner, AgentRunner>()
            .AddSingleton<MainViewModel>().AddSingleton<MainWindow>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            if (_host is IAsyncDisposable asyncHost) await asyncHost.DisposeAsync(); else _host.Dispose();
        }
        base.OnExit(e);
    }
}

public sealed class ForgeCoreOptions
{
    public string WorkspaceRoot { get; set; } = @"D:\ForgeCore\Workspace";
    public string DatabasePath { get; set; } = @"D:\ForgeCore\Data\forgecore.db";
    public int MaxActions { get; set; } = 12;
    public int MaxRetries { get; set; } = 2;
    public int MaxPlanningIterations { get; set; } = 12;
    public int MaxRenderedToolResultCharacters { get; set; } = 12_000;
    public string[] AllowedCommands { get; set; } = [];
    public string[] AllowedAssetHosts { get; set; } = [];
    public long MaxAssetBytes { get; set; } = 100 * 1024 * 1024;
    public string DotnetPath { get; set; } = "dotnet";
    public int DotnetTimeoutSeconds { get; set; } = 120;
    public int MaxProcessOutputCharacters { get; set; } = 1_000_000;
    public int MaxIndexFiles { get; set; } = 2_000;
    public long MaxIndexFileBytes { get; set; } = 256 * 1024;
    public long MaxIndexTotalBytes { get; set; } = 20 * 1024 * 1024;
    public bool EnableOnlineCatalogSearch { get; set; }
    public int MaxCatalogResults { get; set; } = 8;
    public int MaxCatalogResponseBytes { get; set; } = 1_000_000;
    public int OnlineResultCacheMinutes { get; set; } = 10;
    public string UnityEditorPath { get; set; } = string.Empty;
    public string BlenderPath { get; set; } = string.Empty;
    public string BlenderVersion { get; set; } = "5.2";
    public int BlenderTimeoutSeconds { get; set; } = 600;
    public string UnityEditorVersion { get; set; } = string.Empty;
    public int UnityTimeoutSeconds { get; set; } = 600;
    public bool EnableUnityMcp { get; set; }
    public string UnityMcpRelayPath { get; set; } = string.Empty;
    public string[] AllowedUnityMcpTools { get; set; } = [];
    public int UnityMcpTimeoutSeconds { get; set; } = 120;
    public int MaxUnityMcpArgumentsCharacters { get; set; } = 16_000;
    public int MaxUnityMcpResponseCharacters { get; set; } = 100_000;
    public bool EnableUnityProjectPackages { get; set; }
    public bool EnableSeeleGeneration { get; set; }
    public string SeeleApiKeyEnvironmentVariable { get; set; } = "SEELE_API_KEY";
    public int SeeleGenerationTimeoutSeconds { get; set; } = 1_800;
    public int SeelePollSeconds { get; set; } = 15;
    public bool EnableMeshyGeneration { get; set; }
    public string MeshyApiKeyEnvironmentVariable { get; set; } = "MESHY_API_KEY";
    public int MeshyGenerationTimeoutSeconds { get; set; } = 1_800;
    public int MeshyPollSeconds { get; set; } = 10;
    public string Preferred3DGenerationProvider { get; set; } = "seele";
    public string UnityProjectPath { get; set; } = "Games/ForgeCore";
}
