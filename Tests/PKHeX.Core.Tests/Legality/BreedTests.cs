using System;
using System.Linq;
using FluentAssertions;
using PKHeX.Core;
using Xunit;
using static PKHeX.Core.Move;
using static PKHeX.Core.Species;
using static PKHeX.Core.GameVersion;

namespace PKHeX.Tests.Legality;

public class BreedTests
{
    private const int MovesetCount = 4; // Four moves; zeroed empty slots.

    private static void GetMoves(Span<Move> moves, Span<int> result)
    {
        for (int i = 0; i < moves.Length; i++)
            result[i] = (int) moves[i];
    }

    [Theory]
    [InlineData(GD, Bulbasaur, 0, Tackle, Growl)]
    [InlineData(SI, Igglybuff, 0, FeintAttack, Pound, Curse, ZapCannon)]
    [InlineData( C, Igglybuff, 0, FeintAttack, Pound, Flamethrower, Sing)]
    [InlineData( B, Heracross, 0, Megahorn, NightSlash, CloseCombat, StoneEdge)]
    [InlineData( B, Heracross, 0, Bide, Megahorn, Counter, Reversal)]
    [InlineData( B, Heracross, 0, HornAttack, Endure, Megahorn, TakeDown)]
    [InlineData( B, Heracross, 0, Endure, Megahorn, FocusPunch, Feint)]
    [InlineData( B, Heracross, 0, Megahorn, Reversal, Bulldoze, Fling)]
    [InlineData( X, Growlithe, 0, Bite, Roar, FlareBlitz, MorningSun)]
    [InlineData(OR, Growlithe, 0, MorningSun, IronTail, Crunch, HeatWave)]
    [InlineData(OR, Dratini, 0, Wrap, Leer, DragonDance, ExtremeSpeed)]
    [InlineData(OR, Rotom, 0, Astonish, ThunderWave, ThunderShock, ConfuseRay)]
    [InlineData(BD, Gible, 0, IronHead, BodySlam, SandTomb, Outrage)]
    [InlineData(BD, Gible, 0, IronHead, BodySlam, Outrage, SandTomb)]
    [InlineData(BD, Gible, 0, BodySlam, Outrage, SandTomb, DragonBreath)]
    public void VerifyBreed(GameVersion game, Species species, int form, params Move[] movelist)
    {
        var gen = game.GetGeneration();
        Span<int> moves = stackalloc int[MovesetCount];
        GetMoves(movelist, moves);
        var origins = new byte[moves.Length];
        var valid = MoveBreed.Validate(gen, (int) species, form, game, moves, origins);
        valid.Should().BeTrue();

        var x = origins;

        if (gen != 2)
            x.SequenceEqual(x.OrderBy(z => z)).Should().BeTrue();
        else
            x.SequenceEqual(x.OrderBy(z => z != (byte)EggSource2.Base)).Should().BeTrue();
    }

    [Theory]
    [InlineData(C, Igglybuff, 0, Charm, DefenseCurl, Sing, Flamethrower)] // invalid push-out order
    [InlineData(SH, Honedge, 0, FuryCutter, WideGuard, DestinyBond)] // insufficient move count
    [InlineData(OR, Rotom, 0, Discharge, Charge, Trick, ConfuseRay)] // invalid push-out order
    [InlineData(OR, Rotom, 0, ThunderWave, ThunderShock, ConfuseRay, Discharge)] // no inheriting levelup
    public void CheckBad(GameVersion game, Species species, int form, params Move[] movelist)
    {
        var gen = game.GetGeneration();
        Span<int> moves = stackalloc int[MovesetCount];
        GetMoves(movelist, moves);
        Span<byte> result = stackalloc byte[moves.Length];
        var test = MoveBreed.Validate(gen, (int)species, form, game, moves, result);
        test.Should().BeFalse();
    }

    [Theory]
    [InlineData(GD, Bulbasaur, 0, Growl, Tackle)] // swap order, two base moves
    [InlineData(UM, Charmander, 0, Ember, BellyDrum, Scratch, Growl)] // swap order, inherit + egg moves
    [InlineData(BD, Gible, 0, BodySlam, SandTomb, Outrage, DragonBreath)]
    public void CheckFix(GameVersion game, Species species, int form, params Move[] movelist)
    {
        var gen = game.GetGeneration();
        Span<int> moves = stackalloc int[MovesetCount];
        GetMoves(movelist, moves);

        Span<byte> result = stackalloc byte[moves.Length];
        var valid = MoveBreed.Validate(gen, (int)species, form, game, moves, result);
        valid.Should().BeFalse();

        Span<int> expected = stackalloc int[moves.Length];
        var useNew = MoveBreed.GetExpectedMoves(gen, (int)species, form, game, moves, result, expected);
        useNew.Should().BeTrue();

        // fixed order should be different now.
        expected.SequenceEqual(moves).Should().BeFalse();
        // nonzero move count should be same
        expected.Count(0).Should().Be(moves.Count(0));
    }
}
