using System.Security.Cryptography;
using ForgeCore.Motion;
namespace ForgeCore.Core.Tests;
public sealed class AcceptedGuardMovementTests
{
    // Captured from the v4 build the user praised, before the death-only changes.
    [Theory]
    [InlineData(0,"f25a820a80a05036d491fd8316dd16da7336c16d5e9dd379ca2482a69aa331e6")]
    [InlineData(1,"de9b3481c1c6eddcdce36c410676565fc3341a5f830a244df88d34c84d9799e6")]
    [InlineData(2,"4c7a72606b40f8041f88074980c942899590d5a640908b7deb203e1a1af55423")]
    [InlineData(3,"71a46c3bb77fb330936bfc9b4242e9e4c4b995f1cb21fb89dd0a79728c1051fe")]
    [InlineData(4,"6431e888c88fc86b46af9d2a2d91cf47ac76a9a3d46021e0aa1132f428e211a8")]
    [InlineData(5,"1ff3384f7947524ad3cf42bc085aaa7c77b5ac5c485bffb313c7167bdc65907b")]
    [InlineData(6,"8fdee9d1e1edfa013ea8979eca77dea860955370ff39db112ea0412f9c7901ea")]
    public void LivingActionSamplesRemainBitExact(int clip,string expected)
    {
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
        for(var i=0;i<=240;i++) {
            var p=GuardMotionProfile.Sample((GuardMotionClip)clip,i/240f);
            foreach(var n in new[]{p.PelvisSide,p.PelvisHeight,p.PelvisForward,p.PelvisYaw,p.PelvisPitch,p.PelvisRoll,p.ChestPitch,p.ChestYaw,p.ChestRoll,p.HeadPitch,p.HeadYaw,p.Recoil,p.ReloadReach,p.KneeFold})writer.Write(n);
        }
        writer.Flush();Assert.Equal(expected,Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant());
    }
}
