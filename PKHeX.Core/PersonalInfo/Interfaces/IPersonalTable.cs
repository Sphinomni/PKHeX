namespace PKHeX.Core;

public interface IPersonalTable
{
    /// <summary>
    /// Max Species ID (National Dex) that is stored in the table.
    /// </summary>
    int MaxSpeciesID { get; }

    /// <summary>
    /// Gets an index from the inner array.
    /// </summary>
    /// <remarks>Has built in length checks; returns empty (0) entry if out of range.</remarks>
    /// <param name="index">Index to retrieve</param>
    /// <returns>Requested index entry</returns>
    PersonalInfo this[int index] { get; }

    /// <summary>
    /// Alternate way of fetching <see cref="GetFormEntry"/>.
    /// </summary>
    PersonalInfo this[int species, int form] { get; }

    /// <summary>
    /// Gets the <see cref="PersonalInfo"/> entry index for a given <see cref="PKM.Species"/> and <see cref="PKM.Form"/>.
    /// </summary>
    /// <param name="species"><see cref="PKM.Species"/></param>
    /// <param name="form"><see cref="PKM.Form"/></param>
    /// <returns>Entry index for the input criteria</returns>
    int GetFormIndex(int species, int form);

    /// <summary>
    /// Gets the <see cref="PersonalInfo"/> entry for a given <see cref="PKM.Species"/> and <see cref="PKM.Form"/>.
    /// </summary>
    /// <param name="species"><see cref="PKM.Species"/></param>
    /// <param name="form"><see cref="PKM.Form"/></param>
    /// <returns>Entry for the input criteria</returns>
    PersonalInfo GetFormEntry(int species, int form);

    /// <summary>
    /// Checks if the <see cref="PKM.Species"/> is within the bounds of the table.
    /// </summary>
    /// <param name="species"><see cref="PKM.Species"/></param>
    /// <returns>True if present in game</returns>
    bool IsSpeciesInGame(int species);

    /// <summary>
    /// Checks if the <see cref="PKM.Species"/> and <see cref="PKM.Form"/> is within the bounds of the table.
    /// </summary>
    /// <param name="species"><see cref="PKM.Species"/></param>
    /// <param name="form"><see cref="PKM.Form"/></param>
    /// <returns>True if present in game</returns>
    bool IsPresentInGame(int species, int form);
}

/// <summary>
/// Generic interface for exposing specific <see cref="IPersonalInfo"/> retrieval methods.
/// </summary>
/// <typeparam name="T">Specific type of <see cref="IPersonalInfo"/> the table contains.</typeparam>
public interface IPersonalTable<out T> where T : IPersonalInfo
{
    T this[int index] { get; }
    T this[int species, int form] { get; }
    T GetFormEntry(int species, int form);
}
