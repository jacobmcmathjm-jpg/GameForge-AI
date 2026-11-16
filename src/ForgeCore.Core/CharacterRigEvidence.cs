using System.Text.Json.Serialization;

namespace ForgeCore.Core;
public sealed record CharacterPoseEvidence(
    [property:JsonPropertyName("pose")]string Pose,
    [property:JsonPropertyName("movedVertices")]int MovedVertices,
    [property:JsonPropertyName("maximumDisplacement")]double MaximumDisplacement,
    [property:JsonPropertyName("maximumEdgeStretch")]double MaximumEdgeStretch,
    [property:JsonPropertyName("finite")]bool Finite);
public sealed record CharacterRigEvidence(
    [property:JsonPropertyName("schemaVersion")]int SchemaVersion,
    [property:JsonPropertyName("assetSha256")]string AssetSha256,
    [property:JsonPropertyName("rigConvention")]string RigConvention,
    [property:JsonPropertyName("boneCount")]int BoneCount,
    [property:JsonPropertyName("vertexCount")]int VertexCount,
    [property:JsonPropertyName("unweightedVertices")]int UnweightedVertices,
    [property:JsonPropertyName("maximumWeightSumError")]double MaximumWeightSumError,
    [property:JsonPropertyName("maximumRestError")]double MaximumRestError,
    [property:JsonPropertyName("maximumRecoveryError")]double MaximumRecoveryError,
    [property:JsonPropertyName("height")]double Height,
    [property:JsonPropertyName("groundOffset")]double GroundOffset,
    [property:JsonPropertyName("poses")]CharacterPoseEvidence[] Poses)
{
    public void Validate(string expectedHash)
    {
        static bool Range(double x,double min,double max)=>double.IsFinite(x)&&x>=min&&x<=max;
        if(SchemaVersion!=1 || AssetSha256!=expectedHash || RigConvention!="generic_named_bones_v1" ||
            BoneCount is < 21 or > 64 || VertexCount is < 100 or > 150000 || UnweightedVertices!=0 ||
            !Range(MaximumWeightSumError,0,.002) || !Range(MaximumRestError,0,.002) || !Range(MaximumRecoveryError,0,.0001) ||
            !Range(Height,1.59,2.01) || !Range(GroundOffset,0,.01) || Poses is not {Length:3} ||
            !Poses.Select(x=>x?.Pose).SequenceEqual(new[]{"arms","legs","combined"}))
            throw new ArgumentException("Character rig identity, bind pose, weights or canonical dimensions failed.");
        foreach(var pose in Poses)
            if(pose is null || !pose.Finite || pose.MovedVertices<100 || pose.MovedVertices>VertexCount ||
                !Range(pose.MaximumDisplacement,.02,Height*2) || !Range(pose.MaximumEdgeStretch,1,3.5))
                throw new ArgumentException("Character diagnostic deformation failed; this is not human visual acceptance.");
    }
}
