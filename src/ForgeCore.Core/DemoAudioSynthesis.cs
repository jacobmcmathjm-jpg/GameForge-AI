using System;
using System.Collections.Generic;
namespace ForgeCore.Demo
{
    public enum DemoFootstepSurface { Asphalt, Concrete, Metal, Gravel, Wood }
    // Portable original procedural candidates, not recordings or a claim of listening acceptance.
    public static class DemoAudioSynthesis
    {
        public const string Recipe="forgecore_layered_surface_audio_v2";
        public const int SampleRate=48000,Variants=4;
        public static readonly string[] Kinds={"shot","impact","footstep","reload","ui","ambient"};
        public static readonly string[] ClipKeys=CreateKeys();
        private static string[] CreateKeys() {
            var keys=new List<string>(Kinds);
            for(var v=1;v<Variants;v++)keys.Add(Key("shot",DemoFootstepSurface.Asphalt,v));
            foreach(DemoFootstepSurface surface in Enum.GetValues(typeof(DemoFootstepSurface)))
                for(var v=0;v<Variants;v++)if(surface!=DemoFootstepSurface.Asphalt||v!=0)keys.Add(Key("footstep",surface,v));
            return keys.ToArray();
        }
        public static string Key(string kind,DemoFootstepSurface surface,int variant) {
            if(variant<0||variant>=Variants||!Enum.IsDefined(typeof(DemoFootstepSurface),surface))throw new ArgumentOutOfRangeException();
            if(kind=="shot")return variant==0?"shot":"shot-"+variant;
            if(kind=="footstep")return surface==DemoFootstepSurface.Asphalt&&variant==0?"footstep":"footstep-"+surface.ToString().ToLowerInvariant()+"-"+variant;
            if(Array.IndexOf(Kinds,kind)<0)throw new ArgumentException("Unknown bounded audio kind.");return kind;
        }
        public static float[] Generate(string key) {
            var index=Array.IndexOf(ClipKeys,key);if(index<0)throw new ArgumentException("Unknown bounded audio key.");
            var shot=key=="shot"||key.StartsWith("shot-",StringComparison.Ordinal);var foot=key.StartsWith("footstep",StringComparison.Ordinal);
            var surface=DemoFootstepSurface.Asphalt;
            if(foot)foreach(DemoFootstepSurface s in Enum.GetValues(typeof(DemoFootstepSurface)))if(key.Contains(s.ToString().ToLowerInvariant()))surface=s;
            var seconds=shot?.6:foot?.29:key=="impact"?.15:key=="reload"?.25:key=="ui"?.1:4;
            var data=new float[(int)(SampleRate*seconds)];uint state=(uint)(8317+index*7919);
            double body=0,texture=0,previous=0,rumble=0,ringA=0,ringB=0,peak=0;var colour=1+(index%7-3)*.025;
            for(var i=0;i<data.Length;i++) {
                state=unchecked(state*1664525+1013904223);var noise=(state>>8)/8388607.5-1;var t=i/(double)SampleRate;
                body=body*.965+noise*.035;texture=texture*.58+noise*.42;rumble=rumble*.993+noise*.007;
                var high=noise-texture;double sample;
                if(shot) {
                    // Broadband pressure crack, noisy body, quieter mechanics and reflections; no pitched oscillator.
                    sample=(high*.65+texture*.24)*Pulse(t,0,.00015,.0045);
                    sample+=(body*4.4+texture*.18)*Pulse(t,0,.0004,.035*colour);
                    sample+=high*.16*Pulse(t,.047,.0004,.008);
                    sample+=(texture*.19+body*.6)*Pulse(t,.073,.002,.032);
                    sample+=(texture*.09+body*.32)*Pulse(t,.137,.003,.055);
                    sample+=body*.18*Pulse(t,.223,.006,.075);
                }else if(foot) {
                    // Heel/toe loading and contact scrape, not a repeated low-frequency sine note.
                    var heel=Pulse(t,.002,.0015,.018);var toe=Pulse(t,.059+(index%3)*.007,.004,.025);
                    sample=(body*2.3+texture*.10)*(heel+toe*.57);
                    if(surface==DemoFootstepSurface.Asphalt)sample+=high*.11*Pulse(t,.025,.007,.046);
                    if(surface==DemoFootstepSurface.Concrete)sample+=high*.20*(heel+toe*.7);
                    if(surface==DemoFootstepSurface.Gravel) {
                        sample+=high*.20*Pulse(t,.014,.004,.09);
                        for(var grain=0;grain<5;grain++)sample+=texture*.16*Pulse(t,.027+grain*.023+(index%4)*.002,.0004,.006);
                    }
                    if(surface==DemoFootstepSurface.Metal||surface==DemoFootstepSurface.Wood) {
                        var a=surface==DemoFootstepSurface.Metal?.975:.945;var b=surface==DemoFootstepSurface.Metal?.935:.85;
                        ringA=a*ringA+(noise-previous)*.025;ringB=b*ringB+(texture-body)*.08;
                        sample+=(ringA*(surface==DemoFootstepSurface.Metal?2.8:1.1)+ringB*.6)*(heel+toe*.7);
                        sample+=high*(surface==DemoFootstepSurface.Metal?.28:.09)*heel;
                    }
                }else if(key=="impact")sample=(high*.45+body*1.6)*Pulse(t,0,.0003,.018);
                else if(key=="reload")sample=high*.28*(Pulse(t,.025,.0003,.007)+Pulse(t,.14,.0004,.012));
                else if(key=="ui")sample=Math.Sin(2*Math.PI*640*t)*.15*Math.Sin(Math.PI*t/seconds);
                else sample=(rumble*.2+body*.06)*Math.Pow(Math.Sin(Math.PI*t/seconds),2);
                previous=noise;sample*=Math.Min(1,t/.0002)*Math.Min(1,(seconds-t)/.015);data[i]=(float)sample;peak=Math.Max(peak,Math.Abs(sample));
            }
            // Normalize the entire candidate, not hard-clip individual peaks.
            var ceiling=shot?.86:foot?.55:key=="ambient"?.08:.40;var gain=peak>0?ceiling/peak:0;
            for(var i=0;i<data.Length;i++)data[i]=(float)(data[i]*gain);return data;
        }
        private static double Pulse(double t,double at,double attack,double decay) {var d=t-at;return d<0?0:(1-Math.Exp(-d/attack))*Math.Exp(-d/decay);}
    }
    // Reusable clock: measured horizontal travel and true contact, not movement-button presses.
    public sealed class DemoFootstepClock
    {
        private float travel,since;public int Steps {get;private set;}
        public bool Advance(float distance,float dt,bool grounded,bool playing,float stride) {
            if(float.IsNaN(distance)||float.IsInfinity(distance)||distance<0||float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>1||
                float.IsNaN(stride)||float.IsInfinity(stride)||stride<.2f||stride>4)throw new ArgumentOutOfRangeException();
            if(!playing||!grounded||distance>Math.Max(.65f,8*dt+.1f)||distance<.0001f){travel=since=0;return false;}
            if(dt==0)return false;travel+=distance;since+=dt;
            if(travel<stride||since<.22f)return false;travel=Math.Max(0,travel-stride);since=0;Steps++;return true;
        }
    }
}
