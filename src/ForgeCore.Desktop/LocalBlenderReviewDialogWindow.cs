using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ForgeCore.Core;

namespace ForgeCore.Desktop;

/// <summary>Live source-only attestation. An explicit unchecked-by-default acknowledgement gates acceptance.</summary>
public sealed class LocalBlenderReviewDialogWindow:Window
{
    public event Action<bool>? DecisionMade;
    private readonly Button _approve;
    internal bool AcceptanceEnabled=>_approve.IsEnabled;
    internal bool AcceptanceIsDefault=>_approve.IsDefault;
    public LocalBlenderReviewDialogWindow(ApprovalRequest request,LocalBlenderReviewPacket packet)
    {
        var spec=LocalBlenderReviewSpec.FromProposal(request.Call);
        if(spec.Identity!=packet.Identity)throw new InvalidDataException("Displayed source packet differs from the exact proposed review.");
        Title="ForgeCore — exact local source review";Width=1040;Height=750;MinWidth=640;MinHeight=480;
        MaxWidth=SystemParameters.WorkArea.Width;MaxHeight=SystemParameters.WorkArea.Height;
        ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=Paint(11,17,24);Foreground=Paint(222,233,243);
        var root=new Grid{Margin=new Thickness(20)};
        root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new());root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var header=new StackPanel{Margin=new Thickness(0,0,0,12)};
        header.Children.Add(new TextBlock{Text="Human source "+spec.ReviewKind+" review — "+packet.AssetName,FontSize=21,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
        header.Children.Add(new TextBlock{Text="Exact editable source version • not Unity/gameplay/placement approval",Foreground=Paint(245,190,91),Margin=new Thickness(0,5,0,0),TextWrapping=TextWrapping.Wrap});
        root.Children.Add(header);
        var body=new Grid();body.ColumnDefinitions.Add(new(){Width=new GridLength(1.15,GridUnitType.Star)});body.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        var previewPanel=new Grid{Margin=new Thickness(0,0,14,0)};
        previewPanel.RowDefinitions.Add(new(){Height=GridLength.Auto});previewPanel.RowDefinitions.Add(new());previewPanel.RowDefinitions.Add(new(){Height=GridLength.Auto});
        previewPanel.Children.Add(new TextBlock{Text="VERIFIED SOURCE PREVIEW",FontSize=11,Foreground=Paint(95,214,195),Margin=new Thickness(0,0,0,8)});
        using(var stream=new MemoryStream(packet.PreviewPng,false))
        {
            var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth=1600;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
            var frame=new Border{Background=Paint(5,9,14),BorderBrush=Paint(42,60,76),BorderThickness=new Thickness(1),Padding=new Thickness(4),Child=new Image{Source=bitmap,Stretch=Stretch.Uniform}};
            Grid.SetRow(frame,1);previewPanel.Children.Add(frame);
        }
        var warning=new TextBlock{Text="A Blender source preview is not an animated Unity test. Review both the measurements and the exact source/provenance before deciding.",TextWrapping=TextWrapping.Wrap,FontSize=12,Foreground=Paint(156,176,194),Margin=new Thickness(0,10,0,0)};
        Grid.SetRow(warning,2);previewPanel.Children.Add(warning);body.Children.Add(previewPanel);
        var id=packet.Identity;
        var details=new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            FontSize=12,FontFamily=new FontFamily("Consolas"),Background=Paint(15,23,32),Foreground=Foreground,BorderBrush=Paint(42,60,76),Padding=new Thickness(12),
            Text=packet.Summary+"\n\nEditable source:\n"+packet.SourcePath+"\n\nRecipe (text, not executed by this review):\n"+packet.RecipePath+
            "\n\nEXACT REVIEW IDENTITY\nBundle: "+id.BundleSha256+"\nGLB: "+id.AssetSha256+"\nSource: "+id.SourceSha256+"\nPreview: "+id.PreviewSha256+"\nMetrics: "+id.MetricsSha256};
        Grid.SetColumn(details,1);body.Children.Add(details);Grid.SetRow(body,1);root.Children.Add(body);
        var footer=new StackPanel{Margin=new Thickness(0,14,0,0)};
        var confirmation=new CheckBox{IsChecked=false,Margin=new Thickness(0,0,0,10),
            Content=new TextBlock{Text=spec.ReviewKind=="visual"?"I inspected this exact source preview and measurements and accept its visible source quality for library use.":
                "I independently checked this exact source/recipe provenance and applicable rights and accept its use in my asset library.",TextWrapping=TextWrapping.Wrap}};
        footer.Children.Add(confirmation);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        var reject=new Button{Content="Record rejection",MinWidth=135,Margin=new Thickness(0,0,10,0)};reject.SetResourceReference(StyleProperty,"GhostButton");
        _approve=new Button{Content="Accept source "+spec.ReviewKind,MinWidth=170,IsEnabled=false,IsDefault=false};_approve.SetResourceReference(StyleProperty,"PrimaryButton");
        confirmation.Checked+=(_,_)=>_approve.IsEnabled=true;confirmation.Unchecked+=(_,_)=>_approve.IsEnabled=false;
        reject.Click+=(_,_)=>{DecisionMade?.Invoke(false);Close();};
        _approve.Click+=(_,_)=>{if(confirmation.IsChecked!=true)return;DecisionMade?.Invoke(true);Close();};
        buttons.Children.Add(reject);buttons.Children.Add(_approve);footer.Children.Add(buttons);
        footer.Children.Add(new TextBlock{Text="Closing or Escape records nothing. Source/preview changes invalidate this decision. No blanket approval or automatic vault promotion.",FontSize=11,Foreground=Paint(156,176,194),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,9,0,0)});
        Grid.SetRow(footer,2);root.Children.Add(footer);Content=root;
        KeyDown+=(_,e)=>{if(e.Key==Key.Escape){e.Handled=true;Close();}};
    }
    private static SolidColorBrush Paint(byte r,byte g,byte b)=>new(Color.FromRgb(r,g,b));
}
