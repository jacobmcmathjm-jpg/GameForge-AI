using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;
namespace ForgeCore.Desktop;

/// <summary>Offscreen owned-WPF validation only. Never shows a dialog or records a human decision.</summary>
internal static class GameArtReviewUiProbe
{
    public static async Task RunAsync(string workspace,string proposalPath,string destination)
    {
        if(!Directory.Exists(workspace)||Directory.Exists(destination)||File.Exists(destination))throw new IOException("Probe requires existing workspace and fresh destination.");
        if(new FileInfo(proposalPath).Length is <1 or >16384)throw new IOException("Proposal exceeds probe bound.");
        var call=JsonSerializer.Deserialize<ToolCall>(await File.ReadAllBytesAsync(proposalPath))??throw new InvalidDataException("No exact target proposal.");
        var reader=new GameArtReviewReader(new WorkspacePathGuard(workspace));var packet=await reader.ReadAsync(call,default);
        Application.Current.ShutdownMode=ShutdownMode.OnExplicitShutdown;Directory.CreateDirectory(destination);
        var images=new List<object>();
        foreach(var kind in new[]{"visual","rights"})foreach(var size in new[]{new Size(1000,720),new Size(760,560)})
        {
            var proposal=call with{Arguments=new Dictionary<string,string>(call.Arguments){["review_kind"]=kind}};
            var exact=packet with{Proposal=GameArtReviewSpec.FromProposal(proposal)};
            var window=new GameArtReviewDialogWindow(new(proposal,"Offscreen validation only; no attestation"),exact);
            try
            {
                if(window.AcceptanceEnabled||window.AcceptanceIsDefault)throw new InvalidOperationException("Target acceptance must start disabled and not default.");
                var content=(FrameworkElement)window.Content;var inner=new Size(size.Width-40,size.Height-40);
                content.Measure(inner);content.Arrange(new Rect(inner));content.UpdateLayout();
                if(content.DesiredSize.Width>inner.Width+1||content.DesiredSize.Height>inner.Height+1)throw new InvalidOperationException("Target review does not fit tested viewport.");
                var visual=new DrawingVisual();using(var context=visual.RenderOpen()) {
                    context.DrawRectangle(window.Background,null,new Rect(size));
                    context.DrawRectangle(new VisualBrush(content),null,new Rect(new Point(20,20),inner));
                }
                var bitmap=new RenderTargetBitmap((int)size.Width,(int)size.Height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));var name=$"target-{kind}-{size.Width}x{size.Height}.png";
                using(var file=new FileStream(Path.Combine(destination,name),FileMode.CreateNew))encoder.Save(file);
                images.Add(new{kind,path=name,width=size.Width,height=size.Height,acceptEnabled=false,acceptIsDefault=false});
                // Owned offscreen control-state test only: no click, decision event or store write.
                var acknowledgement=Descendants<System.Windows.Controls.CheckBox>(content).Single();
                acknowledgement.IsChecked=true;if(!window.AcceptanceEnabled)throw new InvalidOperationException("Acknowledgement did not enable target decision.");
                acknowledgement.IsChecked=false;if(window.AcceptanceEnabled)throw new InvalidOperationException("Removing acknowledgement did not disable target decision.");
            }
            finally{window.Close();Application.Current.MainWindow=null;}
        }
        var host=new Window();Application.Current.MainWindow=host;var inbox=new ProbeInbox();
        var service=new WpfApprovalService(new TaskAuthorizationSession(),inbox,new UnusedLocalReader(),new BrokenTargetReader(packet));
        var failed=false;
        try{await service.RequestApprovalAsync(new(call,"Synthetic target decode failure",Guid.NewGuid()),default);}
        catch(Exception error) when(error is NotSupportedException or IOException or ArgumentException){failed=true;}
        finally{host.Close();Application.Current.MainWindow=null;}
        if(!failed||service.IsLive(inbox.Id)||inbox.FinalStatus!=ApprovalInboxStatus.Cancelled)throw new InvalidOperationException("Failed target dialog must cancel pending inbox/live registration.");
        var unavailable=new WpfApprovalService(new TaskAuthorizationSession(),new ProbeInbox(),new UnusedLocalReader());
        try{await unavailable.RequestApprovalAsync(new(call,"Unavailable target reader"),default);throw new InvalidDataException("Missing reader fell back to text approval.");}
        catch(InvalidOperationException error) when(error.Message=="Verified Unity target review reader is unavailable."){}
        using var output=new FileStream(Path.Combine(destination,"ui-probe.json"),FileMode.CreateNew);
        await JsonSerializer.SerializeAsync(output,new{schemaVersion=1,packet.Proposal,images,acknowledgementToggleVerified=true,dialogFailureCancelledInbox=true,missingReaderFailsClosed=true,
            anyDialogShown=false,humanDecisionRecorded=false,qualification="Offscreen owned-WPF render and synthetic decode-failure cleanup only. Not live interaction or human asset acceptance."},new JsonSerializerOptions{WriteIndented=true});
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T match)yield return match;foreach(var nested in Descendants<T>(child))yield return nested;}
    }
    private sealed class BrokenTargetReader(GameArtReviewPacket packet):IGameArtReviewReader {
        public Task<GameArtReviewPacket> ReadAsync(ToolCall call,CancellationToken token)=>Task.FromResult(packet with{PreviewPng=[1,2,3]});
    }
    private sealed class UnusedLocalReader:ILocalBlenderReviewReader {
        public Task<LocalBlenderReviewPacket> ReadAsync(string hash,CancellationToken token)=>throw new InvalidOperationException("Target probe must not read local source.");
    }
    private sealed class ProbeInbox:IApprovalInboxStore {
        public Guid Id{get;}=Guid.NewGuid();public ApprovalInboxStatus? FinalStatus{get;private set;}
        public Task<Guid> EnqueueApprovalAsync(Guid id,ApprovalRequest request,DateTimeOffset expires,CancellationToken token)=>Task.FromResult(Id);
        public Task<IReadOnlyList<ApprovalInboxItem>> GetPendingApprovalsAsync(int limit,CancellationToken token)=>Task.FromResult<IReadOnlyList<ApprovalInboxItem>>([]);
        public Task<bool> ResolveApprovalAsync(Guid id,ApprovalInboxStatus status,CancellationToken token){FinalStatus=status;return Task.FromResult(true);}
        public Task<ApprovalInboxItem?> TryClaimApprovalExecutionAsync(Guid id,CancellationToken token)=>Task.FromResult<ApprovalInboxItem?>(null);
        public Task<bool> CompleteApprovalExecutionAsync(Guid id,ApprovalInboxStatus status,CancellationToken token)=>Task.FromResult(false);
    }
}
