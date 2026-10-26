using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace ForgeCore.Desktop;
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent(); DataContext = viewModel;
        SourceInitialized += (_, _) => EnableDarkTitleBar();
        Loaded += async (_, _) => await viewModel.InitializeAsync();
        viewModel.Messages.CollectionChanged += (_, _) =>
        {
            if (viewModel.Messages.Count == 0) return;
            _ = Dispatcher.BeginInvoke(() => MessageList.ScrollIntoView(viewModel.Messages[^1]), DispatcherPriority.Background);
        };
    }

    private void EnableDarkTitleBar()
    {
        if (!OperatingSystem.IsWindows()) return;
        var enabled = 1; var handle = new WindowInteropHelper(this).Handle;
        if (DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)) != 0)
            _ = DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int valueSize);
}
