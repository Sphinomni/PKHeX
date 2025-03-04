using System;
using System.Collections.Generic;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PKHeX.Core;

/// <summary>
/// Generation 3 <see cref="SaveFile"/> object for Pokémon XD saves.
/// </summary>
public sealed class SAV3XD : SaveFile, IGCSaveFile
{
    protected internal override string ShortSummary => $"{OT} ({Version}) {PlayTimeString}";
    public override string Extension => this.GCExtension();
    public SAV3GCMemoryCard? MemoryCard { get; init; }

    private const int SLOT_SIZE = 0x28000;
    private const int SLOT_START = 0x6000;
    private const int SLOT_COUNT = 2;

    private int SaveCount = -1;
    private int SaveIndex = -1;
    private int Config;
    private int Trainer1;
    private int Memo;
    private int Shadow;
    private readonly StrategyMemo StrategyMemo;
    private readonly ShadowInfoTableXD ShadowInfo;
    public int MaxShadowID => ShadowInfo.Count;
    private int OFS_PouchHeldItem, OFS_PouchKeyItem, OFS_PouchBalls, OFS_PouchTMHM, OFS_PouchBerry, OFS_PouchCologne, OFS_PouchDisc;
    private readonly int[] subOffsets = new int[16];
    private readonly byte[] BAK;

    public SAV3XD() : base(SaveUtil.SIZE_G3XD)
    {
        BAK = Array.Empty<byte>();
        // create fake objects
        StrategyMemo = new StrategyMemo();
        ShadowInfo = new ShadowInfoTableXD(false);
        Config = 0xA8;
        Trainer1 = 0xCCD8;
        Party = 0xCD08;
        Box = 0x10E08;
        DaycareOffset = 0x1CA68;
        Memo = 0xF678;
        Shadow = 0x1CB48;
        Initialize();
        ClearBoxes();
    }

    public SAV3XD(byte[] data) : base(data)
    {
        BAK = data;
        Japanese = InitializeData(out StrategyMemo, out ShadowInfo);
        Initialize();
    }

    public override IPersonalTable Personal => PersonalTable.RS;
    public override IReadOnlyList<ushort> HeldItems => Legal.HeldItems_XD;

    private readonly bool Japanese;

    private bool InitializeData(out StrategyMemo memo, out ShadowInfoTableXD info)
    {
        // Scan all 3 save slots for the highest counter
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            int slotOffset = SLOT_START + (i * SLOT_SIZE);
            int SaveCounter = ReadInt32BigEndian(Data.AsSpan(slotOffset + 4));
            if (SaveCounter <= SaveCount)
                continue;

            SaveCount = SaveCounter;
            SaveIndex = i;
        }

        // Decrypt most recent save slot
        Data = ReadSlot(Data, SaveIndex);

        // Get Offset Info
        Span<ushort> subLength = stackalloc ushort[16];
        for (int i = 0; i < 16; i++)
        {
            subLength[i] = ReadUInt16BigEndian(Data.AsSpan(0x20 + (2 * i)));
            subOffsets[i] = ReadUInt16BigEndian(Data.AsSpan(0x40 + (4 * i))) | (ReadUInt16BigEndian(Data.AsSpan(0x40 + (4 * i) + 2)) << 16);
        }

        // Offsets are displaced by the 0xA8 savedata region
        Config = subOffsets[0] + 0xA8;
        Trainer1 = subOffsets[1] + 0xA8;
        Party = Trainer1 + 0x30;
        Box = subOffsets[2] + 0xA8;
        DaycareOffset = subOffsets[4] + 0xA8;
        Memo = subOffsets[5] + 0xA8;
        Shadow = subOffsets[7] + 0xA8;
        // Purifier = subOffsets[14] + 0xA8;

        bool jp = subLength[7] == 0x1E00;
        memo = new StrategyMemo(Data, Memo, xd: true);
        info = new ShadowInfoTableXD(Data.AsSpan(Shadow, subLength[7]), jp);
        return jp;
    }

    private static byte[] ReadSlot(Span<byte> data, int index)
    {
        int slotOffset = SLOT_START + (index * SLOT_SIZE);
        var slot = data.Slice(slotOffset, SLOT_SIZE);
        var result = new byte[SLOT_SIZE];
        var destSpan = result.AsSpan();

        // Decrypt Slot
        Span<ushort> keys = stackalloc ushort[4];
        GeniusCrypto.ReadKeys(slot.Slice(8, keys.Length * 2), keys);
        Range r = new(0x10, 0x27FD8);
        GeniusCrypto.Decrypt(slot[r], destSpan[r], keys); // body
        slot[..0x10].CopyTo(destSpan[..0x10]); // checksums
        slot[^0x18..].CopyTo(destSpan[^0x18..]); // tail end
        return result;
    }

    private void Initialize()
    {
        OFS_PouchHeldItem = Trainer1 + 0x4C8;
        OFS_PouchKeyItem = Trainer1 + 0x540;
        OFS_PouchBalls = Trainer1 + 0x5EC;
        OFS_PouchTMHM = Trainer1 + 0x62C;
        OFS_PouchBerry = Trainer1 + 0x72C;
        OFS_PouchCologne = Trainer1 + 0x7E4;
        OFS_PouchDisc = Trainer1 + 0x7F0;

        // Since PartyCount is not stored in the save file,
        // Count up how many party slots are active.
        for (int i = 0; i < 6; i++)
        {
            if (GetPartySlot(Data, GetPartyOffset(i)).Species != 0)
                PartyCount++;
        }
    }

    protected override byte[] GetFinalData()
    {
        var newFile = GetInnerData();

        // Return the gci if Memory Card is not being exported
        if (MemoryCard is null)
            return newFile;

        MemoryCard.WriteSaveGameData(newFile);
        return MemoryCard.Data;
    }

    private byte[] GetInnerData()
    {
        // Set Memo Back
        StrategyMemo.Write(); // .CopyTo(Data, Memo);
        ShadowInfo.Write().CopyTo(Data, Shadow);
        SetChecksums();

        // Put save slot back in original save data
        var destOffset = SLOT_START + (SaveIndex * SLOT_SIZE);
        byte[] dest = MemoryCard != null ? MemoryCard.ReadSaveGameData() : (byte[])BAK.Clone();
        var destSpan = dest.AsSpan(destOffset, Data.Length);

        // Get updated save slot data
        Span<byte> slot = Data;
        Span<ushort> keys = stackalloc ushort[4];
        GeniusCrypto.ReadKeys(slot.Slice(8, keys.Length * 2), keys);
        Range r = new(0x10, 0x27FD8);
        GeniusCrypto.Encrypt(slot[r], destSpan[r], keys);
        slot[..0x10].CopyTo(destSpan[..0x10]); // checksum/keys
        slot[^0x18..].CopyTo(destSpan[^0x18..]); // tail end
        return dest;
    }

    // Configuration
    protected override SaveFile CloneInternal()
    {
        var data = GetInnerData();
        return new SAV3XD(data) { MemoryCard = MemoryCard };
    }

    protected override int SIZE_STORED => PokeCrypto.SIZE_3XSTORED;
    protected override int SIZE_PARTY => PokeCrypto.SIZE_3XSTORED; // unused
    public override PKM BlankPKM => new XK3();
    public override Type PKMType => typeof(XK3);

    public override int MaxMoveID => Legal.MaxMoveID_3;
    public override int MaxSpeciesID => Legal.MaxSpeciesID_3;
    public override int MaxAbilityID => Legal.MaxAbilityID_3;
    public override int MaxBallID => Legal.MaxBallID_3;
    public override int MaxItemID => Legal.MaxItemID_3_XD;
    public override int MaxGameID => Legal.MaxGameID_3;

    public override int MaxEV => 255;
    public override int Generation => 3;
    public override EntityContext Context => EntityContext.Gen3;
    protected override int GiftCountMax => 1;
    public override int OTLength => 7;
    public override int NickLength => 10;
    public override int MaxMoney => 9999999;

    public override int BoxCount => 8;

    public override bool IsPKMPresent(ReadOnlySpan<byte> data) => EntityDetection.IsPresentGC(data);

    // Checksums
    protected override void SetChecksums()
    {
        Data = SetChecksums(Data, subOffsets[0]);
    }

    public override bool ChecksumsValid => !ChecksumInfo.Contains("Invalid");

    public override string ChecksumInfo
    {
        get
        {
            byte[] data = SetChecksums(Data, subOffsets[0]);

            const int start = 0xA8; // 0x88 + 0x20
            int oldHC = ReadInt32BigEndian(Data.AsSpan(start + subOffsets[0] + 0x38));
            int newHC = ReadInt32BigEndian(data.AsSpan(start + subOffsets[0] + 0x38));
            bool header = newHC == oldHC;

            var oldCHK = Data.AsSpan(0x10, 0x10);
            var newCHK = data.AsSpan(0x10, 0x10);
            bool body = newCHK.SequenceEqual(oldCHK);
            return $"Header Checksum {(header ? "V" : "Inv")}alid, Body Checksum {(body ? "V" : "Inv")}alid.";
        }
    }

    private static byte[] SetChecksums(byte[] input, int subOffset0)
    {
        if (input.Length != SLOT_SIZE)
            throw new ArgumentException("Input should be a slot, not the entire save binary.");

        byte[] data = (byte[])input.Clone();
        const int start = 0xA8; // 0x88 + 0x20

        // Header Checksum
        int newHC = 0;
        for (int i = 0; i < 8; i++)
            newHC += data[i];

        WriteInt32BigEndian(data.AsSpan(start + subOffset0 + 0x38), newHC);

        // Body Checksum
        data.AsSpan(0x10, 0x10).Clear(); // Clear old Checksum Data
        Span<uint> checksum = stackalloc uint[4];
        int dt = 8;
        for (int i = 0; i < checksum.Length; i++)
        {
            uint val = 0;
            var end = dt + 0x9FF4;
            for (int j = dt; j < end; j += 2)
                val += ReadUInt16BigEndian(data.AsSpan(j));
            dt = end;
            checksum[i] = val;
        }

        Span<ushort> newchks = stackalloc ushort[8];
        for (int i = 0; i < 4; i++)
        {
            newchks[i*2] = (ushort)(checksum[i] >> 16);
            newchks[(i * 2) + 1] = (ushort)checksum[i];
        }

        for (int i = 0; i < newchks.Length; i++)
        {
            var dest = data.AsSpan(0x10 + (2 * i));
            var chk = newchks[newchks.Length - 1 - i];
            WriteUInt16BigEndian(dest, chk);
        }

        return data;
    }

    // Config
    public GCVersion GCGameIndex   { get => (GCVersion)Data[Config + 0x00];  set => Data[Config + 0x00] = (byte)value; }
    public GCRegion CurrentRegion  { get => (GCRegion)Data[Config + 0x01];   set => Data[Config + 0x01] = (byte)value; }
    public GCRegion OriginalRegion { get => (GCRegion)Data[Config + 0x02];   set => Data[Config + 0x02] = (byte)value; }
    public LanguageGC GCLanguage   { get => (LanguageGC)Data[Config + 0x03]; set => Data[Config + 0x03] = (byte)value; }
    public override int Language { get => (int)GCLanguage.ToLanguageID(); set => GCLanguage = ((LanguageID)value).ToLanguageGC(); }

    private TimeSpan PlayedSpan
    {
        get => TimeSpan.FromSeconds(TotalSeconds);
        set => TotalSeconds = value.TotalSeconds;
    }

    private double TotalSeconds
    {
        get
        {
            if (Japanese)
                return ReadSingleBigEndian(Data.AsSpan(Config + 0x20));
            return ReadDoubleBigEndian(Data.AsSpan(Config + 0x30));
        }
        set
        {
            if (Japanese)
                WriteSingleBigEndian(Data.AsSpan(Config + 0x20), (float)value);
            else
                WriteDoubleBigEndian(Data.AsSpan(Config + 0x30), value);
        }
    }

    public override int PlayedHours
    {
        get => (ushort)PlayedSpan.Hours + (PlayedSpan.Days * 24);
        set { var time = PlayedSpan; PlayedSpan = time - TimeSpan.FromHours(time.Hours) + TimeSpan.FromHours(value); }
    }

    public override int PlayedMinutes
    {
        get => (byte)PlayedSpan.Minutes;
        set { var time = PlayedSpan; PlayedSpan = time - TimeSpan.FromMinutes(time.Minutes) + TimeSpan.FromMinutes(value); }
    }

    public override int PlayedSeconds
    {
        get => (byte)PlayedSpan.Seconds;
        set { var time = PlayedSpan; PlayedSpan = time - TimeSpan.FromSeconds(time.Seconds) + TimeSpan.FromSeconds(value); }
    }

    // Trainer Info
    public override GameVersion Version { get => GameVersion.XD; protected set { } }
    public override string OT { get => GetString(Trainer1 + 0x00, 20); set => SetString(Data.AsSpan(Trainer1 + 0x00, 20), value.AsSpan(), 10, StringConverterOption.ClearZero); }
    public override int SID { get => ReadUInt16BigEndian(Data.AsSpan(Trainer1 + 0x2C)); set => WriteUInt16BigEndian(Data.AsSpan(Trainer1 + 0x2C), (ushort)value); }
    public override int TID { get => ReadUInt16BigEndian(Data.AsSpan(Trainer1 + 0x2E)); set => WriteUInt16BigEndian(Data.AsSpan(Trainer1 + 0x2E), (ushort)value); }

    public override int Gender { get => Data[Trainer1 + 0x8E0]; set => Data[Trainer1 + 0x8E0] = (byte)value; }
    public override uint Money { get => ReadUInt32BigEndian(Data.AsSpan(Trainer1 + 0x8E4)); set => WriteUInt32BigEndian(Data.AsSpan(Trainer1 + 0x8E4), value); }
    public uint Coupons { get => ReadUInt32BigEndian(Data.AsSpan(Trainer1 + 0x8E8)); set => WriteUInt32BigEndian(Data.AsSpan(Trainer1 + 0x8E8), value); }

    // Storage
    public override int GetPartyOffset(int slot) => Party + (SIZE_STORED * slot);
    private int GetBoxInfoOffset(int box) => Box + (((30 * SIZE_STORED) + 0x14) * box);
    public override int GetBoxOffset(int box) => GetBoxInfoOffset(box) + 20;
    public override string GetBoxName(int box) => GetString(GetBoxInfoOffset(box), 16);

    public override void SetBoxName(int box, string value)
    {
        SetString(Data.AsSpan(GetBoxInfoOffset(box), 20), value.AsSpan(), 8, StringConverterOption.ClearZero);
    }

    protected override PKM GetPKM(byte[] data)
    {
        if (data.Length != SIZE_STORED)
            Array.Resize(ref data, SIZE_STORED);
        return new XK3(data);
    }

    protected override byte[] DecryptPKM(byte[] data) => data;
    public override PKM GetPartySlot(byte[] data, int offset) => GetStoredSlot(data, offset);

    public override PKM GetStoredSlot(byte[] data, int offset)
    {
        // Get Shadow Data
        var pk = (XK3)base.GetStoredSlot(data, offset);
        if (pk.ShadowID > 0 && pk.ShadowID < ShadowInfo.Count)
            pk.Purification = ShadowInfo[pk.ShadowID].Purification;
        return pk;
    }

    protected override void SetPKM(PKM pk, bool isParty = false)
    {
        if (pk is not XK3 xk3)
            return; // shouldn't ever hit

        xk3.CurrentRegion = (byte)CurrentRegion;
        xk3.OriginalRegion = (byte)OriginalRegion;

        // Set Shadow Data back to save
        if (xk3.ShadowID <= 0 || xk3.ShadowID >= ShadowInfo.Count)
            return;

        var entry = ShadowInfo[xk3.ShadowID];
        entry.Purification = xk3.Purification;
        entry.Species = xk3.Species;
        entry.PID = xk3.PID;
        entry.IV_HP  = xk3.IV_HP ;
        entry.IV_ATK = xk3.IV_ATK;
        entry.IV_DEF = xk3.IV_DEF;
        entry.IV_SPA = xk3.IV_SPA;
        entry.IV_SPD = xk3.IV_SPD;
        entry.IV_SPE = xk3.IV_SPE;
    }

    protected override void SetDex(PKM pk)
    {
        /*
        if (pk.Species is 0 or > Legal.MaxSpeciesID_3)
            return;
        if (pk.IsEgg)
            return;

        // Dex Related
        var entry = StrategyMemo.GetEntry(pk.Species);
        if (entry.IsEmpty) // Populate
        {
            entry.Species = pk.Species;
            entry.PID = pk.PID;
            entry.TID = pk.TID;
            entry.SID = pk.SID;
        }
        if (entry.Matches(pk.Species, pk.PID, pk.TID, pk.SID))
        {
            entry.Seen = true;
            entry.Owned = true;
        }
        StrategyMemo.SetEntry(entry);
        */
    }

    public override IReadOnlyList<InventoryPouch> Inventory
    {
        get
        {
            InventoryPouch[] pouch =
            {
                new InventoryPouch3GC(InventoryType.Items, Legal.Pouch_Items_XD, 999, OFS_PouchHeldItem, 30), // 20 COLO, 30 XD
                new InventoryPouch3GC(InventoryType.KeyItems, Legal.Pouch_Key_XD, 1, OFS_PouchKeyItem, 43),
                new InventoryPouch3GC(InventoryType.Balls, Legal.Pouch_Ball_RS, 999, OFS_PouchBalls, 16),
                new InventoryPouch3GC(InventoryType.TMHMs, Legal.Pouch_TM_RS, 999, OFS_PouchTMHM, 64),
                new InventoryPouch3GC(InventoryType.Berries, Legal.Pouch_Berries_RS, 999, OFS_PouchBerry, 46),
                new InventoryPouch3GC(InventoryType.Medicine, Legal.Pouch_Cologne_XD, 999, OFS_PouchCologne, 3), // Cologne
                new InventoryPouch3GC(InventoryType.BattleItems, Legal.Pouch_Disc_XD, 1, OFS_PouchDisc, 60),
            };
            return pouch.LoadAll(Data);
        }
        set => value.SaveAll(Data);
    }

    // Daycare Structure:
    // 0x00 -- Occupied
    // 0x01 -- Deposited Level
    // 0x02-0x03 -- unused?
    // 0x04-0x07 -- Initial EXP
    public override int GetDaycareSlotOffset(int loc, int slot) { return DaycareOffset + 8; }
    public override uint? GetDaycareEXP(int loc, int slot) { return null; }
    public override bool? IsDaycareOccupied(int loc, int slot) { return null; }
    public override void SetDaycareEXP(int loc, int slot, uint EXP) { /* todo */ }
    public override void SetDaycareOccupied(int loc, int slot, bool occupied) { /* todo */ }

    public override string GetString(ReadOnlySpan<byte> data) => StringConverter3GC.GetString(data);

    public override int SetString(Span<byte> destBuffer, ReadOnlySpan<char> value, int maxLength, StringConverterOption option)
    {
        return StringConverter3GC.SetString(destBuffer, value, maxLength, option);
    }
}
