#nullable enable
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
namespace ForgeCore.Demo
{
    // Snapshot strings are immutable; continuations explicitly use the pool, never Unity's synchronization context.
    public sealed class DemoCheckpointQueue
    {
        private readonly object gate=new object();private readonly Func<string,Action<DemoWorkKind,double>?,bool> write;
        private Task<bool> tail=Task.FromResult(true);private int pending;private volatile bool failed;
        public DemoCheckpointQueue(Func<string,Action<DemoWorkKind,double>?,bool> write){this.write=write??throw new ArgumentNullException(nameof(write));}
        public bool Failed=>failed;
        public bool Pending {get{lock(gate)return pending>0;}}
        public Task<bool> Completion {get{lock(gate)return tail;}}
        public Task<bool> Request(string snapshot,Action<DemoWorkKind,double>? measured=null) {
            if(string.IsNullOrEmpty(snapshot)||Encoding.UTF8.GetByteCount(snapshot)>65536)return Task.FromResult(false);
            lock(gate){if(failed||pending>=16)return Task.FromResult(false);pending++;
                tail=tail.ContinueWith(prior=>{
                    try{if(!prior.Result){failed=true;return false;}var ok=write(snapshot,measured);if(!ok)failed=true;return ok;}
                    catch{failed=true;return false;}
                    finally{lock(gate)pending--;}
                },CancellationToken.None,TaskContinuationOptions.None,TaskScheduler.Default);return tail;
            }
        }
    }
    public static class DemoAtomicStateFiles
    {
        public static bool TryWrite(string root,string name,string text,Action<DemoWorkKind,double>? measured,out string error) {
            var whole=Stopwatch.StartNew();error="";
            try {
                if(name!="checkpoint.json"&&name!="settings.json")throw new ArgumentException("Unknown state file.");
                if(string.IsNullOrEmpty(text)||Encoding.UTF8.GetByteCount(text)>65536)throw new IOException("State size limit.");
                if(!Path.IsPathRooted(root)||root.StartsWith("\\\\",StringComparison.Ordinal)||root.StartsWith("//",StringComparison.Ordinal))throw new IOException("Local absolute state root required.");
                root=Path.GetFullPath(root);if(root.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)==Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar))throw new IOException("Dedicated state directory required.");
                RejectLinks(root);Directory.CreateDirectory(root);RejectLinks(root);
                var path=Path.Combine(root,name);var temporary=path+".new";var backup=path+".previous";
                foreach(var candidate in new[]{path,temporary,backup})RejectLinks(candidate);
                using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                    var bytes=new UTF8Encoding(false).GetBytes(text);file.Write(bytes,0,bytes.Length);
                    var phase=Stopwatch.StartNew();try{file.Flush(true);}finally{Notify(measured,DemoWorkKind.BackgroundFlush,phase.Elapsed.TotalMilliseconds);}
                }
                var replace=Stopwatch.StartNew();try{if(File.Exists(path))File.Replace(temporary,path,backup);else File.Move(temporary,path);}
                finally{Notify(measured,DemoWorkKind.BackgroundReplace,replace.Elapsed.TotalMilliseconds);}
                return true;
            }catch{error="Save failed; previous state and any retained .new file kept. Check storage before restarting autosave.";return false;}
            finally{Notify(measured,DemoWorkKind.BackgroundWrite,whole.Elapsed.TotalMilliseconds);}
        }
        private static void RejectLinks(string path) {
            for(string? current=Path.GetFullPath(path);current!=null;current=Path.GetDirectoryName(current))
                if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("State links blocked.");
        }
        // Diagnostic failures must not turn a successful atomic commit into an ambiguous failed write/retry.
        private static void Notify(Action<DemoWorkKind,double>? measured,DemoWorkKind kind,double ms){try{measured?.Invoke(kind,ms);}catch{}}
    }
}
