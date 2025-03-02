using static PKHeX.Core.RibbonIndex;

namespace PKHeX.Core;

/// <summary>
/// Parsing logic for <see cref="IRibbonSetCommon4"/>.
/// </summary>
public static class RibbonVerifierCommon4
{
    public static void Parse(this IRibbonSetCommon4 r, RibbonVerifierArguments args, ref RibbonResultList list)
    {
        var pk = args.Entity;
        var evos = args.History;
        if (r.RibbonFootprint && !RibbonRules.IsRibbonValidFootprint(pk, evos))
            list.Add(Footprint);
        if (r.RibbonRecord)
            list.Add(Record); // Unobtainable

        bool gen4 = evos.HasVisitedGen4;
        bool gen6 = evos.HasVisitedGen6;
        bool bdsp = evos.HasVisitedBDSP;
        bool oras6 = gen6 && !(pk.IsUntraded && pk.XY);
        bool sinnoh4 = gen4 && args.Encounter is not EncounterStatic4 { Species: (int)Species.Pichu, Form: 1 };
        bool sinnohChamp = sinnoh4 || bdsp; // no gen4 hg/ss
        bool cosmetic = sinnoh4 || oras6 || bdsp; // no gen4 hg/ss
        bool daily = gen4 || gen6 || bdsp;

        if (r.RibbonLegend && !gen4)
            list.Add(Legend);
        if (r.RibbonChampionSinnoh && !sinnohChamp)
            list.Add(ChampionSinnoh);
        if (!daily)
            FlagDaily(r, ref list);
        if (!cosmetic)
            FlagCosmetic(r, ref list);
    }

    private static void FlagDaily(IRibbonSetCommon4 r, ref RibbonResultList list)
    {
        if (r.RibbonAlert) list.Add(Alert);
        if (r.RibbonShock) list.Add(Shock);
        if (r.RibbonDowncast) list.Add(Downcast);
        if (r.RibbonCareless) list.Add(Careless);
        if (r.RibbonRelax) list.Add(Relax);
        if (r.RibbonSnooze) list.Add(Snooze);
        if (r.RibbonSmile) list.Add(Smile);
    }

    private static void FlagCosmetic(IRibbonSetCommon4 r, ref RibbonResultList list)
    {
        if (r.RibbonGorgeous) list.Add(Gorgeous);
        if (r.RibbonRoyal) list.Add(Royal);
        if (r.RibbonGorgeousRoyal) list.Add(GorgeousRoyal);
    }

    public static void ParseEgg(this IRibbonSetCommon4 r, ref RibbonResultList list)
    {
        if (r.RibbonFootprint)
            list.Add(Footprint);
        if (r.RibbonRecord)
            list.Add(Record);
        if (r.RibbonLegend)
            list.Add(Legend);
        if (r.RibbonChampionSinnoh)
            list.Add(ChampionSinnoh);

        FlagDaily(r, ref list);
        FlagCosmetic(r, ref list);
    }
}
