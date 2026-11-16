#nullable disable
using System;
namespace ForgeCore.Demo
{
    // Cached bone roles, no per-vertex arrays or Unity skeleton/property calls.
    public struct DemoSkinSupport {public bool torso;public byte hand;}
    public static class DemoSkinSupportMap
    {
        public static byte Role(string name)=>name=="Hips"||name=="Spine"||name=="Chest"?(byte)1:name=="Hand.L"?(byte)2:name=="Hand.R"?(byte)3:(byte)0;
        public static DemoSkinSupport Evaluate(byte[] roles,int i0,int i1,int i2,int i3,float w0,float w1,float w2,float w3){
            if(roles==null)throw new ArgumentNullException(nameof(roles));
            float torso=0;byte hand=0;
            Add(roles,i0,w0,ref torso,ref hand);Add(roles,i1,w1,ref torso,ref hand);
            Add(roles,i2,w2,ref torso,ref hand);Add(roles,i3,w3,ref torso,ref hand);
            return new DemoSkinSupport{torso=torso>.5f,hand=hand};
        }
        private static void Add(byte[] roles,int index,float weight,ref float torso,ref byte hand){
            if((uint)index>=(uint)roles.Length)throw new ArgumentOutOfRangeException(nameof(index));
            var role=roles[index];if(role==1)torso+=weight;
            if(weight>.5f){if(role==2)hand=1;if(role==3)hand=2;}
        }
    }
}
