﻿using static PKHeX.Core.LegalityCheckStrings;

namespace PKHeX.Core;

/// <summary>
/// Verifies the <see cref="IHyperTrain"/> values.
/// </summary>
public sealed class HyperTrainingVerifier : Verifier
{
    protected override CheckIdentifier Identifier => CheckIdentifier.Training;

    public override void Verify(LegalityAnalysis data)
    {
        var pk = data.Entity;
        if (pk is not IHyperTrain t)
            return; // No Hyper Training before Gen7

        if (!t.IsHyperTrained())
            return;

        if (!t.IsHyperTrainingAvailable(data.Info.EvoChainsAllGens))
        {
            data.AddLine(GetInvalid(LHyperPerfectUnavailable));
            return;
        }

        if (pk.CurrentLevel != 100)
        {
            data.AddLine(GetInvalid(LHyperBelow100));
            return;
        }

        int max = pk.MaxIV;
        if (pk.IVTotal == max * 6)
        {
            data.AddLine(GetInvalid(LHyperPerfectAll));
            return;
        }

        // LGPE gold bottle cap applies to all IVs regardless
        if (pk.GG && t.IsHyperTrainedAll()) // already checked for 6IV, therefore we're flawed on at least one IV
            return;

        for (int i = 0; i < 6; i++) // Check individual IVs
        {
            if (pk.GetIV(i) != max || !t.IsHyperTrained(i))
                continue;
            data.AddLine(GetInvalid(LHyperPerfectOne));
            break;
        }
    }
}
