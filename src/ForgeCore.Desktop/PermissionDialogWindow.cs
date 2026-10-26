using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ForgeCore.Core;

namespace ForgeCore.Desktop;

public sealed class PermissionDialogWindow : Window
{
    public event Action<bool>? DecisionMade;

    public PermissionDialogWindow(ApprovalRequest request)
    {
        var isGameArtReview = request.Call.Kind == ToolKind.ReviewGameArtAsset;
        Title = "ForgeCore permission request";
        Width = 680; Height = 540; MinWidth = 560; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false; ResizeMode = ResizeMode.CanResize;
        SetResourceReference(BackgroundProperty, "Background"); SetResourceReference(ForegroundProperty, "Text");

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Border
        {
            Background = Brush(38, 29, 21), BorderBrush = Brush(103, 75, 35), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 14)
        };
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(10), Background = Brush(76, 54, 25) };
        icon.Child = new TextBlock
        {
            Text = "!", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Brush(242, 184, 75),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        headerGrid.Children.Add(icon);
        var titles = new StackPanel { Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock { Text = isGameArtReview ? "Human review required" : "Approval required", FontSize = 19, FontWeight = FontWeights.SemiBold });
        titles.Children.Add(new TextBlock
        {
            Text = isGameArtReview ? "Only you can accept or reject this exact asset evidence" : "Review the exact scope before ForgeCore continues", FontSize = 11,
            Foreground = Brush(174, 159, 137), Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetColumn(titles, 1); headerGrid.Children.Add(titles);
        var permission = new Border
        {
            CornerRadius = new CornerRadius(9), Padding = new Thickness(9, 5, 9, 5),
            Background = Brush(63, 44, 22), VerticalAlignment = VerticalAlignment.Center
        };
        permission.Child = new TextBlock
        {
            Text = request.Call.RequiredPermission.ToString().ToUpperInvariant(), Foreground = Brush(255, 202, 111),
            FontSize = 9, FontWeight = FontWeights.Bold
        };
        Grid.SetColumn(permission, 2); headerGrid.Children.Add(permission); header.Child = headerGrid; root.Children.Add(header);

        var details = new TextBox
        {
            Text = Format(request), IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(16),
            Background = Brush(12, 18, 25), Foreground = Brush(223, 233, 241), BorderBrush = Brush(37, 52, 69),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 12.5
        };
        Grid.SetRow(details, 1); root.Children.Add(details);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var reject = new Button { Content = isGameArtReview ? "Reject asset" : "Reject action", MinWidth = 120, IsCancel = true };
        reject.SetResourceReference(StyleProperty, "GhostButton");
        var approve = new Button { Content = isGameArtReview ? "Approve asset" : "Approve once", MinWidth = 120, Margin = new Thickness(10, 0, 0, 0), IsDefault = true };
        approve.SetResourceReference(StyleProperty, "PrimaryButton");
        reject.Click += (_, _) => Decide(false); approve.Click += (_, _) => Decide(true);
        buttons.Children.Add(reject); buttons.Children.Add(approve);
        var footer = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock
        {
            Text = isGameArtReview ? "Your decision is recorded for these exact hashes; closing records nothing." : "Approval applies only to this displayed action.", Foreground = Brush(142, 160, 179),
            FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(buttons, 1); footer.Children.Add(buttons); Grid.SetRow(footer, 2); root.Children.Add(footer);
        Content = root;
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue) => new(Color.FromRgb(red, green, blue));
    private void Decide(bool approved) { DecisionMade?.Invoke(approved); Close(); }

    private static string Format(ApprovalRequest request)
    {
        var text = new StringBuilder();
        text.AppendLine(request.Call.Description).AppendLine();
        text.AppendLine(request.Reason).AppendLine();
        text.Append("Tool: ").AppendLine(request.Call.Kind.ToString());
        text.Append("Permission: ").AppendLine(request.Call.RequiredPermission.ToString());
        text.AppendLine().AppendLine("Arguments:");
        if (request.Call.Kind == ToolKind.ReviewGameArtAsset)
        {
            var kind = request.Call.Arguments["review_kind"];
            text.AppendLine("HUMAN ATTESTATION — ForgeCore and its model cannot choose this decision.");
            text.AppendLine();
            text.AppendLine(kind == GameArtReviewSpec.Visual
                ? "APPROVE means: I inspected the exact four-view preview identified below and accept this asset's visible quality, coherence, scale cues, materials, and suitability for this project."
                : "APPROVE means: I independently reviewed the imported provenance and applicable licence/provider terms for the exact asset identified below, and I approve its use in this project.");
            text.AppendLine("REJECT records an explicit failed review. Closing this window records no review decision.");
            text.AppendLine("This is not a blanket approval and does not apply after any source or preview hash changes.");
            text.AppendLine();
            foreach (var argument in request.Call.Arguments)
                text.Append(argument.Key).Append(" = ").AppendLine(argument.Value);
            return text.ToString();
        }
        if (request.Call.Kind == ToolKind.ApplyTextPatch)
        {
            text.Append("path = ").AppendLine(request.Call.Arguments["path"]);
            if (request.Call.Arguments.TryGetValue("expected_sha256", out var hash)) text.Append("expected_sha256 = ").AppendLine(hash);
            text.AppendLine().AppendLine("--- existing selection");
            AppendDiff(text, request.Call.Arguments["old_text"], '-');
            text.AppendLine("+++ replacement");
            AppendDiff(text, request.Call.Arguments["new_text"], '+');
            return text.ToString();
        }
        if (request.Call.Kind == ToolKind.ImportStagedAsset)
        {
            var project = request.Call.Arguments["project_path"];
            var file = request.Call.Arguments["file_name"];
            text.Append("quarantined source = ").Append(project).Append("/.forgecore/staging/").AppendLine(file);
            text.Append("project destination = ").Append(project).Append('/').AppendLine(request.Call.Arguments["destination_path"]);
            text.Append("expected SHA-256 = ").AppendLine(request.Call.Arguments["expected_sha256"]);
            text.AppendLine("overwrite = never");
            text.AppendLine("provenance = destination .asset.json sidecar");
            return text.ToString();
        }
        if (request.Call.Kind == ToolKind.Generate3DAsset)
        {
            var provider = request.Call.Arguments["provider"];
            text.Append("provider = ").AppendLine(provider);
            text.Append("project = ").AppendLine(request.Call.Arguments["project_path"]);
            text.Append("asset name = ").AppendLine(request.Call.Arguments["asset_name"]);
            text.Append("model = ").AppendLine(request.Call.Arguments["model"]);
            text.Append("exact cloud prompt = ").AppendLine(request.Call.Arguments["prompt"]);
            text.AppendLine("engine target = Unity");
            text.AppendLine("cost = cloud credits may be consumed");
            if (provider == "meshy") text.AppendLine("workflow = Meshy 6 preview mesh, then PBR refine; both stages consume credits when successful");
            text.AppendLine("data boundary = prompt leaves this PC; API key is read only from an environment variable");
            text.AppendLine("downloads = passive GLB/images only; signed URL query credentials are never persisted");
            text.AppendLine("destination = project .forgecore/staging quarantine");
            text.AppendLine("Unity import = separate approval after inspection");
            return text.ToString();
        }
        if (request.Call.Kind == ToolKind.InstallUnityBridge)
        {
            var project = request.Call.Arguments["project_path"];
            text.Append("project = ").AppendLine(project);
            text.Append("bridge destination = ").Append(project).AppendLine("/Assets/ForgeCore/Editor/ForgeCoreBridge.cs");
            text.AppendLine("source = deterministic bridge bundled with this ForgeCore release");
            text.AppendLine("overwrite = never");
            text.AppendLine("network/package installation = none");
            return text.ToString();
        }
        if (request.Call.Kind == ToolKind.UnityEditorAction)
        {
            text.Append("project = ").AppendLine(request.Call.Arguments["project_path"]);
            text.Append("typed operation = ").AppendLine(request.Call.Arguments["operation"]);
            foreach (var argument in request.Call.Arguments.Where(x => x.Key is not "project_path" and not "operation"))
                text.Append(argument.Key).Append(" = ").AppendLine(argument.Value);
            text.AppendLine("mode = bounded Unity batch process");
            text.AppendLine("shell = disabled");
            text.AppendLine("package manager = locked local packages only when configured; manifest and lock file protected");
            text.AppendLine("administrator = never");
            text.AppendLine("project C# code = executes as the current normal user");
            return text.ToString();
        }
        if (request.Call.Kind == ToolKind.UnityPlayerSmokeTest)
        {
            text.Append("project = ").AppendLine(request.Call.Arguments["project_path"]);
            text.Append("exact build = ").AppendLine(request.Call.Arguments["output_path"]);
            text.Append("bounded duration = ").Append(request.Call.Arguments["duration_seconds"]).AppendLine(" seconds");
            text.AppendLine("mode = headless standalone player startup test");
            text.AppendLine("provenance/hash verification = required before launch");
            text.AppendLine("termination = only the player process started by this test");
            text.AppendLine("shell/network/package manager/administrator = never");
            text.AppendLine("project C# code = executes as the current normal user");
            return text.ToString();
        }
        if (request.Call.Kind == ToolKind.UnityPlayerProfileTest)
        {
            text.Append("project = ").AppendLine(request.Call.Arguments["project_path"]);
            text.Append("exact development build = ").AppendLine(request.Call.Arguments["output_path"]);
            text.Append("bounded sampling duration = ").Append(request.Call.Arguments["duration_seconds"]).AppendLine(" seconds");
            text.AppendLine("rendering = visible standalone player at fixed 1280x720");
            text.AppendLine("gameplay exercise = fixed scripted_gameplay_v1 movement and genre-action inputs");
            text.AppendLine("evidence = frame, available GPU timing, peak memory, scene complexity, movement, genre actions, and renderer identity");
            text.AppendLine("provenance/hash verification = schema-3 build, executable, data, scenes, template kit, and manifest required before launch");
            text.AppendLine("one-use request/result = hash-bound and removed after the run; log and immutable evidence retained");
            text.AppendLine("termination = only the player process started by this test");
            text.AppendLine("shell/network/package manager/administrator = never");
            text.AppendLine("project C# code = executes as the current normal user");
            return text.ToString();
        }
        if (request.Call.Kind == ToolKind.UnityMcpAction)
        {
            text.Append("project = ").AppendLine(request.Call.Arguments["project_path"]);
            text.Append("MCP operation = ").AppendLine(request.Call.Arguments["operation"]);
            if (request.Call.Arguments.TryGetValue("tool_name", out var toolName)) text.Append("exact allow-listed tool = ").AppendLine(toolName);
            if (request.Call.Arguments.TryGetValue("arguments_json", out var arguments))
                text.Append("arguments = ").AppendLine(arguments.Length <= 4_000 ? arguments : arguments[..4_000] + "... (truncated in dialog)");
            text.AppendLine("transport = official local Unity MCP relay over redirected stdio");
            text.AppendLine("first connection = must also be approved in the intended Unity Editor");
            text.AppendLine("credentials = managed by Unity; ForgeCore does not read or store them");
            text.AppendLine("administrator = never");
            text.AppendLine("warning = Unity/project code and configured AI services may run as the current normal user");
            return text.ToString();
        }
        foreach (var argument in request.Call.Arguments)
        {
            var value = argument.Value.Length <= 2_000 ? argument.Value : argument.Value[..2_000] + "... (truncated in dialog)";
            text.Append(argument.Key).Append(" = ").AppendLine(value);
        }
        return text.ToString();
    }

    private static void AppendDiff(StringBuilder text, string value, char prefix)
    {
        var displayed = value.Length <= 4_000 ? value : value[..4_000] + "... (truncated in dialog)";
        foreach (var line in displayed.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')) text.Append(prefix).Append(' ').AppendLine(line);
    }
}
