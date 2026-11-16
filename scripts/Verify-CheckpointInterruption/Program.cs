using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using ForgeCore.Demo;
var work=Path.GetFullPath(Path.Combine(Environment.CurrentDirectory,"work"));
if(args.Length==2&&args[0]=="--child"){
    var root=Path.GetFullPath(args[1]);
    if(!root.StartsWith(work+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(root).StartsWith("checkpoint-interruption-",StringComparison.Ordinal))throw new IOException("Owned scratch root required.");
    var ok=DemoAtomicStateFiles.TryWrite(root,"checkpoint.json","new-interrupted",(kind,_)=>{
        if(kind!=DemoWorkKind.BackgroundFlush)return;
        using(var s=new FileStream(Path.Combine(root,"ready"),FileMode.CreateNew))s.WriteByte(1);
        using var hold=new ManualResetEventSlim();hold.Wait(15000);
    },out _);
    Environment.ExitCode=ok?0:2;return;
}
if(args.Length!=0)throw new ArgumentException("No arguments or owned child mode.");
var fixture=Path.Combine(work,"checkpoint-interruption-"+Guid.NewGuid().ToString("N"));
if(!DemoAtomicStateFiles.TryWrite(fixture,"checkpoint.json","old-durable",null,out _))throw new IOException("Could not seed owned fixture.");
var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Environment.CurrentDirectory};
start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);start.ArgumentList.Add("--child");start.ArgumentList.Add(fixture);
using var child=Process.Start(start)??throw new IOException("Owned helper did not start.");
try{
    var deadline=Stopwatch.StartNew();
    while(!File.Exists(Path.Combine(fixture,"ready"))&&deadline.ElapsedMilliseconds<5000&&!child.HasExited)await Task.Delay(25);
    if(!File.Exists(Path.Combine(fixture,"ready")))throw new IOException("Owned helper did not reach the flushed pre-replace boundary.");
    if(child.HasExited)throw new IOException("Owned helper exited before interruption.");
    child.Kill(entireProcessTree:true);await child.WaitForExitAsync();
    var file=Path.Combine(fixture,"checkpoint.json");
    if(File.ReadAllText(file)!="old-durable"||File.ReadAllText(file+".new")!="new-interrupted")throw new IOException("Previous durable state or interrupted candidate was lost.");
    if(DemoAtomicStateFiles.TryWrite(fixture,"checkpoint.json","unsafe-replay",null,out _))throw new IOException("Occupied interrupted state must block replay.");
    if(File.ReadAllText(file)!="old-durable"||File.ReadAllText(file+".new")!="new-interrupted")throw new IOException("Replay changed retained state.");
    using var output=new FileStream(Path.Combine(fixture,"verification.json"),FileMode.CreateNew);
    await JsonSerializer.SerializeAsync(output,new{schemaVersion=1,ownedHelperPid=child.Id,interruptedAfterDurableFlushBeforeReplace=true,previousDurableStatePreserved=true,interruptedCandidateRetained=true,automaticReplayBlocked=true,
        qualification="Owned helper process and synthetic scratch checkpoint only; not a Unity process kill, OS crash or power-loss certification."},new JsonSerializerOptions{WriteIndented=true});
    Console.WriteLine("OWNED_INTERRUPTED_SAVE_RECOVERY_PASS "+fixture);
}finally{if(!child.HasExited){child.Kill(entireProcessTree:true);await child.WaitForExitAsync();}}
