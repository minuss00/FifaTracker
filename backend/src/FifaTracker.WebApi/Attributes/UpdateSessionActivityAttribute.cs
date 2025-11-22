namespace FifaTracker.WebApi.Attributes;

/// <summary>
/// Marks an endpoint that should automatically update TotalActiveTime
/// for all active users in the session after execution.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class UpdateSessionActivityAttribute : Attribute
{
    /// <summary>
    /// The name of the route parameter containing the session ID or match ID.
    /// Default is "id".
    /// </summary>
    public string SessionIdParameterName { get; set; } = "id";
    
    /// <summary>
    /// If true, the parameter is a match ID and the session ID will be looked up from the match.
    /// Default is false.
    /// </summary>
    public bool IsMatchIdParameter { get; set; } = false;
}
