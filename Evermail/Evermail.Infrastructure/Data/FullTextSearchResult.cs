namespace Evermail.Infrastructure.Data;

/// <summary>
/// Represents the KEY/RANK payload returned by SQL Server CONTAINSTABLE.
/// This is configured as a keyless entity in <see cref="EvermailDbContext"/>.
/// </summary>
public class FullTextSearchResult
{
    public Guid EmailId { get; set; }
    public int Rank { get; set; }
}

