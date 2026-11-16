using System;
namespace ForgeCore.Demo
{
    [Serializable]public sealed class DemoSettings
    {
        public int schemaVersion=2,quality=2,frameLimit=60;
        public float sensitivity=1.7f,fieldOfView=72,masterVolume=.6f;
        public float effectsVolume=1,footstepsVolume=1,ambienceVolume=1,interfaceVolume=1;
        public bool invertY,largeText,audioCaptions=true;
    }
    [Serializable]public sealed class DemoCheckpoint
    {
        public int schemaVersion,stage,defeatedMask,health,reserve,magazine;
        public string profile="";
        public float x,y,z,yaw,pitch;
    }
    public static class DemoStateRules
    {
        public const string Profile="industrial_mission_terminals_v1";
        private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        public static bool SettingsValid(DemoSettings s)=>s!=null&&s.schemaVersion==2&&s.quality>=0&&s.quality<=2&&
            (s.frameLimit==30||s.frameLimit==60||s.frameLimit==120)&&Finite(s.sensitivity)&&s.sensitivity>=.2f&&s.sensitivity<=5&&
            Finite(s.fieldOfView)&&s.fieldOfView>=60&&s.fieldOfView<=100&&Volume(s.masterVolume)&&Volume(s.effectsVolume)&&Volume(s.footstepsVolume)&&Volume(s.ambienceVolume)&&Volume(s.interfaceVolume);
        private static bool Volume(float v)=>Finite(v)&&v>=0&&v<=1;
        public static DemoSettings UpgradeSettings(DemoSettings s) {
            if(s==null)throw new ArgumentNullException(nameof(s));if(s.schemaVersion!=1)return s;
            return new DemoSettings{schemaVersion=2,quality=s.quality,frameLimit=s.frameLimit,sensitivity=s.sensitivity,fieldOfView=s.fieldOfView,
                masterVolume=s.masterVolume,invertY=s.invertY,largeText=s.largeText,audioCaptions=s.audioCaptions,
                effectsVolume=1,footstepsVolume=1,ambienceVolume=1,interfaceVolume=1};
        }
        public static float AudioGain(DemoSettings s,string kind) {
            if(s==null)return 1;var v=kind=="footstep"?s.footstepsVolume:kind=="ambient"?s.ambienceVolume:kind=="ui"?s.interfaceVolume:s.effectsVolume;
            return Finite(v)?Math.Max(0,Math.Min(1,v)):0;
        }
        public static bool CheckpointValid(DemoCheckpoint c)=>c!=null&&c.schemaVersion==1&&c.profile==Profile&&
            c.stage>=0&&c.stage<=2&&c.defeatedMask>=0&&c.defeatedMask<=7&&
            (c.stage!=0||c.defeatedMask==0)&&(c.stage!=1||(c.defeatedMask&4)==0)&&(c.stage!=2||(c.defeatedMask&3)==3)&&
            c.health>=1&&c.health<=100&&c.reserve>=0&&c.reserve<=60&&c.magazine>=0&&c.magazine<=6&&
            Finite(c.x)&&Finite(c.y)&&Finite(c.z)&&Math.Abs(c.x)<=15&&Math.Abs(c.z)<=15&&c.y>=0&&c.y<=3&&
            Finite(c.yaw)&&c.yaw>=0&&c.yaw<360&&Finite(c.pitch)&&c.pitch>=-75&&c.pitch<=75;
    }
}
