using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ForgeCore.Core;
using ForgeCore.Security;
using ForgeCore.Tools;

namespace ForgeCore.Desktop;

/// <summary>Offscreen rendering of our own dialog; no desktop control, Show(), store or human attestation.</summary>
internal static class LocalBlenderReviewUiProbe
{
    public static async Task RunAsync(string workspace,string hash,string destination)
    {
        if(!Directory.Exists(workspace)||Directory.Exists(destination)||File.Exists(destination))throw new IOException("Probe requires an existing workspace and fresh output folder.");
        Application.Current.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        var reader=new LocalBlenderReviewReader(new WorkspacePathGuard(workspace));
        var packet=await reader.ReadAsync(hash,default);Directory.CreateDirectory(destination);
        var images=new List<object>();
        foreach(var kind in new[]{"visual","rights"})foreach(var size in new[]{new Size(1000,720),new Size(760,560)})
        {
            var call=LocalBlenderReviewSpec.Proposal(packet.Identity,kind);
            var window=new LocalBlenderReviewDialogWindow(new(call,"Offscreen rendering only; no attestation"),packet);
            try
            {
                if(window.AcceptanceEnabled||window.AcceptanceIsDefault)throw new InvalidOperationException("Review acceptance must start disabled and must not be a default key action.");
                var content=(FrameworkElement)window.Content;var inner=new Size(size.Width-40,size.Height-40);
                content.Measure(inner);content.Arrange(new Rect(inner));content.UpdateLayout();
                if(content.DesiredSize.Width>inner.Width+1||content.DesiredSize.Height>inner.Height+1)throw new InvalidOperationException("Review dialog does not fit its tested viewport.");
                var visual=new DrawingVisual();using(var context=visual.RenderOpen())
                {
                    context.DrawRectangle(window.Background,null,new Rect(size));
                    context.DrawRectangle(new VisualBrush(content),null,new Rect(new Point(20,20),inner));
                }
                var bitmap=new RenderTargetBitmap((int)size.Width,(int)size.Height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
                var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));var name=$"source-{kind}-{size.Width}x{size.Height}.png";
                using(var file=new FileStream(Path.Combine(destination,name),FileMode.CreateNew))png.Save(file);
                images.Add(new{kind,path=name,width=size.Width,height=size.Height,acceptEnabled=false,acceptIsDefault=false});
            }
            finally{window.Close();Application.Current.MainWindow=null;}
        }
        // Simulate only a failing dialog construction against an in-memory inbox. No window reaches Show().
        var host=new Window();Application.Current.MainWindow=host;var inbox=new ProbeInbox();
        var service=new WpfApprovalService(new TaskAuthorizationSession(),inbox,new BrokenPreviewReader(packet));
        var request=LocalBlenderReviewSpec.Proposal(packet.Identity,"visual");var failed=false;
        try{await service.RequestApprovalAsync(new(request,"Synthetic failure test",Guid.NewGuid()),default);}
        catch(Exception error) when(error is NotSupportedException or IOException or ArgumentException){failed=true;}
        finally{host.Close();Application.Current.MainWindow=null;}
        if(!failed||service.IsLive(inbox.Id)||inbox.FinalStatus!=ApprovalInboxStatus.Cancelled)
            throw new InvalidOperationException("Dialog load failure must cancel its pending inbox and remove its live approval.");
        using var output=new FileStream(Path.Combine(destination,"ui-probe.json"),FileMode.CreateNew);
        await JsonSerializer.SerializeAsync(output,new{schemaVersion=1,packet.Identity,images,dialogFailureCancelledInbox=true,
            anyDialogShown=false,humanDecisionRecorded=false,qualification="Offscreen rendering of owned WPF code and synthetic failure cleanup. Not live user interaction or human asset acceptance."},new JsonSerializerOptions{WriteIndented=true});
    }
    private sealed class BrokenPreviewReader(LocalBlenderReviewPacket source):ILocalBlenderReviewReader
    {
        public Task<LocalBlenderReviewPacket> ReadAsync(string hash,CancellationToken token)=>Task.FromResult(source with{PreviewPng=[1,2,3]});
    }
    private sealed class ProbeInbox:IApprovalInboxStore
    {
        public Guid Id{get;}=Guid.NewGuid();public ApprovalInboxStatus? FinalStatus{get;private set;}
        public Task<Guid> EnqueueApprovalAsync(Guid taskId,ApprovalRequest request,DateTimeOffset expires,CancellationToken token)=>Task.FromResult(Id);
        public Task<IReadOnlyList<ApprovalInboxItem>> GetPendingApprovalsAsync(int limit,CancellationToken token)=>Task.FromResult<IReadOnlyList<ApprovalInboxItem>>([]);
        public Task<bool> ResolveApprovalAsync(Guid id,ApprovalInboxStatus resolution,CancellationToken token){FinalStatus=resolution;return Task.FromResult(true);}
        public Task<ApprovalInboxItem?> TryClaimApprovalExecutionAsync(Guid id,CancellationToken token)=>Task.FromResult<ApprovalInboxItem?>(null);
        public Task<bool> CompleteApprovalExecutionAsync(Guid id,ApprovalInboxStatus outcome,CancellationToken token)=>Task.FromResult(false);
    }
}
