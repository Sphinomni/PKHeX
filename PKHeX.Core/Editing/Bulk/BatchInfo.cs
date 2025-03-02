using System.Collections.Generic;

namespace PKHeX.Core;

/// <summary>
/// Information wrapper used for Batch Editing to apply suggested values.
/// </summary>
public sealed class BatchInfo
{
    internal PKM Entity { get; }
    internal BatchInfo(PKM pk) => Entity = pk;

    private LegalityAnalysis? la;
    internal LegalityAnalysis Legality => la ??= new LegalityAnalysis(Entity);

    public bool Legal => Legality.Valid;
    internal IReadOnlyList<int> SuggestedRelearn => Legality.GetSuggestedRelearnMoves();
}
