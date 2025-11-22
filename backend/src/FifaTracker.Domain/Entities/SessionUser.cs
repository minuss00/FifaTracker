namespace FifaTracker.Domain.Entities;

// Junction table for Session and User (many-to-many)
public class SessionUser
{
    public Guid SessionId { get; set; }
    public Session Session { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime JoinedAt { get; set; }
    
    // Activity tracking
    public bool IsActiveInSession { get; set; } = true;
    public DateTime? PausedAt { get; set; }
    public DateTime? LastResumedAt { get; set; }
    public TimeSpan TotalActiveTime { get; set; } = TimeSpan.Zero;
}
