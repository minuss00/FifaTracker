namespace FifaTracker.Domain.Entities;

public class Session
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public SessionStatus Status { get; set; }
    public MatchType MatchType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastModifiedAt { get; set; }

    // Navigation properties
    public ICollection<SessionUser> SessionUsers { get; set; } = new List<SessionUser>();
    public ICollection<Match> Matches { get; set; } = new List<Match>();
}

public enum SessionStatus
{
    Active,
    Completed
}

public enum MatchType
{
    OneVsOne,
    TwoVsTwo
}
