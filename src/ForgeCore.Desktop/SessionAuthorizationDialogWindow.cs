using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ForgeCore.Desktop;

public sealed class SessionAuthorizationDialogWindow : Window
{
    private readonly CheckBox _overnight;
    public TimeSpan Duration => _overnight.IsChecked == true ? TimeSpan.FromHours(10) : TimeSpan.FromMinutes(30);
    public int MaxActions => _overnight.IsChecked == true ? 120 : 12;

    public SessionAuthorizationDialogWindow(string projectPath)
    {
        Title = "Enable ForgeCore trusted-project mode";
        Width = 720; Height = 650; MinWidth = 620; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "Background"); SetResourceReference(ForegroundProperty, "Text");

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        heading.Children.Add(new TextBlock { Text = "Trusted-project mode", FontSize = 23, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(new TextBlock
        {
            Text = "A temporary task grant—not unrestricted PC control", FontSize = 12, Foreground = Brush(142, 160, 179),
            Margin = new Thickness(0, 5, 0, 0)
        });
        root.Children.Add(heading);

        var details = new StackPanel();
        details.Children.Add(Card("SCOPE", $"Project: {projectPath}\nStandard: 30 minutes / 12 actions\nOptional overnight: 10 hours / 120 actions\nStorage: memory only; cleared on restart"));
        details.Children.Add(Card("AUTOMATICALLY APPROVED",
            "• Install or safely upgrade ForgeCore's hash-verified Unity bridge/map/template kits\n• Compile, create reviewed map/game templates, prepare imported generated assets, run tests/play mode, and build Windows\n• Run the bundled local Blender barrel recipe and import its hash-verified output (no visual/rights approval implied)\n• Search for CC0 assets only\n• Download CC0 passive assets from configured hosts into quarantine\n• Inspect and import verified staged assets into this exact project\n• Run typed dotnet build/test for this exact project"));
        details.Children.Add(Card("ALWAYS BLOCKED OR STILL SEPARATELY APPROVED",
            "• Cloud generation, purchases, subscriptions, or other spending\n• Publishing, account changes, credentials, or legal/rights decisions\n• Arbitrary commands or PowerShell\n• Executables, installers, archives, packages, registry changes, or deletion\n• Administrator actions\n• Unknown or non-CC0 automatic asset licences\n• Generic file writes and access outside the configured workspace\n• Any action not listed above", warning: true));
        details.Children.Add(new TextBlock
        {
            Text = "Every action still passes ForgeCore's security policy and is written to the local audit log. Emergency Stop revokes this grant and cancels active work immediately.",
            TextWrapping = TextWrapping.Wrap, Foreground = Brush(174, 190, 205), FontSize = 11.5, LineHeight = 18, Margin = new Thickness(4, 5, 4, 0)
        });
        _overnight = new CheckBox
        {
            Content = "Enable overnight trusted mode (10 hours / 120 guarded actions)",
            Margin = new Thickness(4, 12, 4, 4), FontWeight = FontWeights.SemiBold
        };
        details.Children.Add(_overnight);
        var scroll = new ScrollViewer { Content = details, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1); root.Children.Add(scroll);

        var footer = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock
        {
            Text = "You can revoke this at any time.", VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush(142, 160, 179), FontSize = 11
        });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var cancel = new Button { Content = "Cancel", MinWidth = 110, IsCancel = true };
        cancel.SetResourceReference(StyleProperty, "GhostButton");
        var authorize = new Button { Content = "Enable trusted-project mode", MinWidth = 210, Margin = new Thickness(10, 0, 0, 0), IsDefault = true };
        authorize.SetResourceReference(StyleProperty, "PrimaryButton");
        authorize.Click += (_, _) => { DialogResult = true; Close(); };
        buttons.Children.Add(cancel); buttons.Children.Add(authorize); Grid.SetColumn(buttons, 1); footer.Children.Add(buttons);
        Grid.SetRow(footer, 2); root.Children.Add(footer); Content = root;
    }

    private static Border Card(string title, string text, bool warning = false)
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = title, Foreground = warning ? Brush(242, 184, 75) : Brush(85, 214, 190),
            FontSize = 10, FontWeight = FontWeights.Bold
        });
        content.Children.Add(new TextBlock
        {
            Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 19, Margin = new Thickness(0, 8, 0, 0),
            Foreground = Brush(223, 233, 241)
        });
        return new Border
        {
            Background = warning ? Brush(38, 29, 21) : Brush(12, 18, 25),
            BorderBrush = warning ? Brush(103, 75, 35) : Brush(37, 52, 69), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10), Padding = new Thickness(15), Margin = new Thickness(0, 0, 0, 10), Child = content
        };
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue) => new(Color.FromRgb(red, green, blue));
}
