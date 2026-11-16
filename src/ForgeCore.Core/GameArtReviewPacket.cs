namespace ForgeCore.Core;

/// <summary>Read-only exact imported-art preview for a human decision. Never an approval or placement certificate.</summary>
public sealed record GameArtReviewPacket(GameArtReviewSpec Proposal,string SourceProvenanceSha256,string UnityMetaSha256,
    string Summary,byte[] PreviewPng);
public interface IGameArtReviewReader
{
    Task<GameArtReviewPacket> ReadAsync(ToolCall proposal,CancellationToken token);
}
