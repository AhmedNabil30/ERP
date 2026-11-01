namespace Kaff.Domain.Rulings;

/// <summary>What a second executed-quantity entry by the same engineer, on the same line and day, does.</summary>
public enum QuantityEntryMode
{
    /// <summary>The entry is a period delta, so it adds to the earlier one. spec.md section 8, rule 3.</summary>
    Adds = 1,

    /// <summary>The entry replaces the earlier one; the latest timestamp wins. spec.md section 8, rule 5.</summary>
    LatestWins = 2,
}

/// <summary>
/// Does a quantity count as a "field" for the latest-timestamp-wins rule? spec.md section 8 says two
/// things that collide on exactly this case: quantities are period <b>deltas</b> (a second entry adds), and
/// for the same engineer, same day, same field the <b>latest timestamp wins</b> (a second entry replaces).
/// KAFF-600 question 1.
/// </summary>
/// <remarks>
/// Running assumption, not a ruling: a quantity against a BOQ line adds, because it is a delta. "Latest
/// wins" governs the single-valued fields (check-in, check-out, weather, a note, the stoppage reason),
/// which are never summed. Changing <see cref="Default"/> to <see cref="QuantityEntryMode.LatestWins"/> is
/// the whole change: the handler asks <see cref="Effective"/> before and after each entry and moves the BOQ
/// line by the difference.
/// </remarks>
public sealed record DailyLogQuantityEntry(QuantityEntryMode Mode) : IRuling
{
    public static readonly DailyLogQuantityEntry Default = new(QuantityEntryMode.Adds);

    public RulingNote Note => new(
        "DailyLogQuantityEntry",
        "KAFF-600 question 1: is a quantity a \"field\" for rule 5, or does a second entry on the same line mean "
        + "he did more work? spec.md section 8 rule 3 (deltas, so it adds) collides with rule 5 (latest timestamp "
        + "wins, so it replaces) on exactly this case.",
        "What a second quantity entry by the same engineer on the same day and BOQ line does to the executed quantity.",
        $"Mode {Mode}: a quantity against a BOQ line adds, because it is a period delta (rule 3). Latest-timestamp-wins "
        + "(rule 5) governs the single-valued fields only: check-in, check-out, weather, note, stoppage reason. ASSUMED.",
        "Set Default to LatestWins. Per engineer, day and line only the quantity with the latest OccurredAt then "
        + "counts, and the BOQ line moves by the difference. Entries already stored are unchanged; they are only re-read.",
        RulingStatus.Assumed);

    /// <summary>
    /// The quantity the entries of one log on one line count for. Adds: their sum. Latest wins: the one with
    /// the latest <c>OccurredAt</c> (ties go to the larger entry id, so the answer never depends on arrival order).
    /// </summary>
    public decimal Effective(IEnumerable<(DateTimeOffset OccurredAt, Guid EntryId, decimal Quantity)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return Mode switch
        {
            QuantityEntryMode.Adds => entries.Sum(entry => entry.Quantity),
            QuantityEntryMode.LatestWins => entries
                .OrderByDescending(entry => entry.OccurredAt)
                .ThenByDescending(entry => entry.EntryId)
                .Select(entry => entry.Quantity)
                .FirstOrDefault(),
            _ => throw new InvalidOperationException($"Unknown quantity entry mode {Mode}."),
        };
    }
}
