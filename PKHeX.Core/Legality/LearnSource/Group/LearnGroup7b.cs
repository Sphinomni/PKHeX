using System;

namespace PKHeX.Core;

/// <summary>
/// Group that checks the source of a move in <see cref="GameVersion.Gen7b"/>.
/// </summary>
public sealed class LearnGroup7b : ILearnGroup
{
    public static readonly LearnGroup7b Instance = new();
    private const int Generation = 7;

    public ILearnGroup? GetPrevious(PKM pk, EvolutionHistory history, IEncounterTemplate enc, LearnOption option) => null;
    public bool HasVisited(PKM pk, EvolutionHistory history) => history.HasVisitedLGPE;

    public bool Check(Span<MoveResult> result, ReadOnlySpan<int> current, PKM pk, EvolutionHistory history,
        IEncounterTemplate enc, MoveSourceType types = MoveSourceType.All, LearnOption option = LearnOption.Current)
    {
        var evos = history.Gen7b;
        for (var i = 0; i < evos.Length; i++)
            Check(result, current, pk, evos[i], i);

        return MoveResult.AllParsed(result);
    }

    private static void Check(Span<MoveResult> result, ReadOnlySpan<int> current, PKM pk, EvoCriteria evo, int stage)
    {
        var game = LearnSource7GG.Instance;
        if (!game.TryGetPersonal(evo.Species, evo.Form, out var pi))
            return; // should never happen.

        for (int i = result.Length - 1; i >= 0; i--)
        {
            if (result[i].Valid)
                continue;

            var move = current[i];
            var chk = game.GetCanLearn(pk, pi, evo, move);
            if (chk != default)
                result[i] = new(chk, (byte)stage, Generation);
        }
    }

    public void GetAllMoves(Span<bool> result, PKM pk, EvolutionHistory history, IEncounterTemplate enc, MoveSourceType types = MoveSourceType.All, LearnOption option = LearnOption.Current)
    {
        if (types.HasFlagFast(MoveSourceType.Encounter) && enc.Generation == Generation)
            FlagEncounterMoves(enc, result);

        foreach (var evo in history.Gen7b)
            LearnSource7GG.Instance.GetAllMoves(result, pk, evo, types);
    }

    private static void FlagEncounterMoves(IEncounterTemplate enc, Span<bool> result)
    {
        if (enc is IMoveset { Moves: int[] { Length: not 0 } x })
        {
            foreach (var move in x)
                result[move] = true;
        }
        if (enc is IRelearn { Relearn: int[] { Length: not 0 } r })
        {
            foreach (var move in r)
                result[move] = true;
        }
    }
}
