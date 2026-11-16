using System;
using System.Linq;
using System.Security.Cryptography;
namespace ForgeCore.Demo
{
    // Release-curated CC0 source identity, not a general downloader or a human listening approval.
    public static class RecordedFootstepBank
    {
        public const string Id="gbox_subway_hardfloor_v1",ProcessingRecipe="dc_remove_peak055_fade3ms10ms_v1";
        public const string Creator="GboxMikeFozzy",License="CC0-1.0",SourcePage="https://opengameart.org/content/footsteps-0";
        public static readonly string[] SourceHashes={
            "33c9bef5e8aeb1069455699a34a0c5e1ef1787fd3f61594b0859d7e6bb9f9dec","f05396c807eb8eaecffb2a1ead611ec971c5a7914d1d081c91d7796e852692a2",
            "9817cc93897ed1de5da87675b26be4196bfda3daa6f1de9dc4398a68d250cdb9","b373ff981e44d6f7ba2291225a4803078771029615ed81606136fdb9599906d5",
            "f1b4893dd3cebe23438567a0dcaf047848a52b594e7eb03e5c212d99e83d2298","39147cc1b98abb4b5378ad07b15d50d18f2aa0f0fd2f5514f078c1e574d14773"};
        public static readonly int[] SourceFrames={7388,8098,15202,15984,15060,11792};
        public static readonly string[] Keys=Enumerable.Range(0,6).Select(i=>"recorded-hardfloor-"+i).ToArray();
        public static string SourceFile(int index){CheckIndex(index);return "Source-gbox-hardfloor-"+(index+1).ToString("00")+".ogg";}
        public static bool MatchesSource(int index,byte[] bytes) {
            CheckIndex(index);if(bytes==null||bytes.Length<64||bytes.Length>128*1024)return false;
            using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(bytes).Select(b=>b.ToString("x2")))==SourceHashes[index];
        }
        public static string Key(DemoFootstepSurface surface,int stepNumber) {
            if(surface!=DemoFootstepSurface.Asphalt&&surface!=DemoFootstepSurface.Concrete)throw new ArgumentException("Hard-floor mapping only.");
            // All six genuine source takes, deterministic permutation; no synthetic pitch substitution.
            return Keys[new[]{0,3,1,4,2,5}[(int)((uint)stepNumber%6)]];
        }
        public static float[] Process(int index,float[] decoded,int sampleRate,int channels) {
            CheckIndex(index);
            if(decoded==null||sampleRate!=48000||channels!=1||decoded.Length!=SourceFrames[index])throw new ArgumentException("Exact bounded source decode required.");
            double mean=0;foreach(var v in decoded){if(float.IsNaN(v)||float.IsInfinity(v)||Math.Abs(v)>1.01f)throw new ArgumentException("Non-finite/out-of-range decoded audio.");mean+=v;}
            mean/=decoded.Length;double peak=0;foreach(var v in decoded)peak=Math.Max(peak,Math.Abs(v-mean));
            if(peak<.001)throw new ArgumentException("Silent recording rejected.");
            var output=new float[decoded.Length];var gain=.55/peak;
            for(var i=0;i<output.Length;i++)output[i]=(float)((decoded[i]-mean)*gain*Math.Min(1,i/144.0)*Math.Min(1,(output.Length-1-i)/480.0));
            return output;
        }
        private static void CheckIndex(int index){if(index<0||index>=6)throw new ArgumentOutOfRangeException("index");}
    }
}
