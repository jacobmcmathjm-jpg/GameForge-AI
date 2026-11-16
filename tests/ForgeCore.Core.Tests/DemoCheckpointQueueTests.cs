using ForgeCore.Demo;
namespace ForgeCore.Core.Tests;
public sealed class DemoCheckpointQueueTests
{
    [Fact] public async Task RequestsAreNonblockingOrderedAndUseImmutableSnapshots()
    {
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();var values=new List<string>();var active=0;var maximum=0;
        var queue=new DemoCheckpointQueue((text,_)=>{var count=Interlocked.Increment(ref active);maximum=Math.Max(maximum,count);entered.Set();
            try{if(!release.Wait(5000))return false;values.Add(text);return true;}finally{Interlocked.Decrement(ref active);}});
        var original="first";var first=queue.Request(original);Assert.True(entered.Wait(5000));original="mutated";
        var second=queue.Request(original);Assert.True(queue.Pending);Assert.False(first.IsCompleted);Assert.False(second.IsCompleted);
        release.Set();Assert.True(await second.WaitAsync(TimeSpan.FromSeconds(5)));Assert.True(await first);Assert.False(queue.Pending);Assert.False(queue.Failed);
        Assert.Equal(new[]{"first","mutated"},values);Assert.Equal(1,maximum);
    }
    [Fact] public async Task FailedWriteStopsQueuedAndFutureWritesWithoutAmbiguousReplay()
    {
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();var writes=0;
        var queue=new DemoCheckpointQueue((_,_)=>{Interlocked.Increment(ref writes);entered.Set();release.Wait(5000);return false;});
        var first=queue.Request("one");Assert.True(entered.Wait(5000));var second=queue.Request("two");release.Set();
        Assert.False(await second.WaitAsync(TimeSpan.FromSeconds(5)));Assert.False(await first);Assert.False(await queue.Request("three"));
        Assert.Equal(1,writes);Assert.True(queue.Failed);Assert.False(queue.Pending);
    }
    [Fact] public async Task BacklogAndUtf8SnapshotSizeAreBoundedWithoutPoisoningValidQueue()
    {
        using var release=new ManualResetEventSlim();var queue=new DemoCheckpointQueue((_,_)=>release.Wait(5000));
        Assert.False(await queue.Request(""));Assert.False(await queue.Request(new string('\u20ac',22000)));
        var accepted=Enumerable.Range(0,16).Select(i=>queue.Request(i.ToString())).ToArray();Assert.False(await queue.Request("overflow"));
        release.Set();Assert.All(await Task.WhenAll(accepted).WaitAsync(TimeSpan.FromSeconds(5)),Assert.True);Assert.False(queue.Failed);
        Assert.True(await queue.Request("after drain").WaitAsync(TimeSpan.FromSeconds(5)));
    }
    [Fact] public async Task WriterExceptionsBecomeStickyFailureNotUnobservedFaultedTasks()
    {
        var queue=new DemoCheckpointQueue((_,_)=>throw new IOException("owned test"));Assert.False(await queue.Request("one"));
        Assert.False(await queue.Completion);Assert.True(queue.Failed);Assert.False(await queue.Request("two"));
    }
    [Fact] public void AtomicWriterRetainsPreviousAndOccupiedTemporaryAndDiagnosticFailureCannotUndoCommit()
    {
        var root=Path.Combine(Path.GetTempPath(),"ForgeCoreCheckpointTest-"+Guid.NewGuid().ToString("N"));
        Assert.True(DemoAtomicStateFiles.TryWrite(root,"checkpoint.json","first",(_,_)=>throw new InvalidOperationException(),out var error));Assert.Empty(error);
        var measured=new List<DemoWorkKind>();Assert.True(DemoAtomicStateFiles.TryWrite(root,"checkpoint.json","second",(kind,ms)=>{Assert.True(ms>=0);measured.Add(kind);},out error));
        var file=Path.Combine(root,"checkpoint.json");Assert.Equal("second",File.ReadAllText(file));Assert.Equal("first",File.ReadAllText(file+".previous"));
        Assert.Equal(new[]{DemoWorkKind.BackgroundFlush,DemoWorkKind.BackgroundReplace,DemoWorkKind.BackgroundWrite},measured);
        File.WriteAllText(file+".new","interrupted");Assert.False(DemoAtomicStateFiles.TryWrite(root,"checkpoint.json","third",null,out error));Assert.NotEmpty(error);
        Assert.Equal("second",File.ReadAllText(file));Assert.Equal("first",File.ReadAllText(file+".previous"));Assert.Equal("interrupted",File.ReadAllText(file+".new"));
    }
    [Fact] public void AtomicWriterRejectsUnknownPathsAndOversizedUtf8BeforeCreatingDirectories()
    {
        var root=Path.Combine(Path.GetTempPath(),"ForgeCoreCheckpointReject-"+Guid.NewGuid().ToString("N"));
        foreach(var name in new[]{"../escape","anything.json","checkpoint.json.new"})Assert.False(DemoAtomicStateFiles.TryWrite(root,name,"{}",null,out _));
        Assert.False(DemoAtomicStateFiles.TryWrite(root,"checkpoint.json",new string('\u20ac',22000),null,out _));Assert.False(Directory.Exists(root));
        Assert.False(DemoAtomicStateFiles.TryWrite("relative","checkpoint.json","{}",null,out _));
        Assert.False(DemoAtomicStateFiles.TryWrite(Path.GetPathRoot(root)!,"checkpoint.json","{}",null,out _));
        Assert.False(DemoAtomicStateFiles.TryWrite("\\\\server\\share\\state","checkpoint.json","{}",null,out _));
    }
}
