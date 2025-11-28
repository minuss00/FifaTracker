namespace FifaTracker.Domain.Entities;

public class Match
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Session Session { get; set; } = null!;

    public bool IsGenerated { get; set; } // true if auto-generated, false if custom
    public bool IsCompleted { get; set; }
    public int? Team1Score { get; set; }
    public int? Team2Score { get; set; }
    public DateTime? PlayedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    
    // Priority for pending matches (not persisted to database, calculated on-demand)
    // Custom matches always have Double.MaxValue priority to appear first
    public double Priority { get; set; }

    // Navigation properties
    public ICollection<MatchTeam> MatchTeams { get; set; } = new List<MatchTeam>();
}
