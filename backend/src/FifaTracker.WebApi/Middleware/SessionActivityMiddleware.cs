using FifaTracker.Domain.Extensions;
using FifaTracker.Domain.Interfaces;
using FifaTracker.WebApi.Attributes;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.WebApi.Middleware;

/// <summary>
/// Middleware that automatically updates TotalActiveTime for all active users
/// in a session after endpoints marked with [UpdateSessionActivity] execute successfully.
/// </summary>
public class SessionActivityMiddleware
{
    private readonly RequestDelegate _next;
    
    public SessionActivityMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    
    public async Task InvokeAsync(HttpContext context, IApplicationDbContext dbContext)
    {
        // Execute the endpoint first
        await _next(context);
        
        // After execution, check if we should update activity
        var endpoint = context.GetEndpoint();
        var attribute = endpoint?.Metadata.GetMetadata<UpdateSessionActivityAttribute>();
        
        // Skip if no attribute or request failed
        if (attribute == null || context.Response.StatusCode >= 400)
        {
            return;
        }
        
        // Extract session ID from route
        Guid sessionId;
        var paramValue = context.Request.RouteValues[attribute.SessionIdParameterName]?.ToString();
        
        if (!Guid.TryParse(paramValue, out var parsedId))
        {
            return;
        }
        
        // If the parameter is a match ID, look up the session from the match
        if (attribute.IsMatchIdParameter)
        {
            var match = await dbContext.Matches
                .FirstOrDefaultAsync(m => m.Id == parsedId);
            
            if (match == null)
            {
                return;
            }
            
            sessionId = match.SessionId;
        }
        else
        {
            sessionId = parsedId;
        }
        
        // Update activity for all active users in this session
        var now = DateTime.UtcNow;
        var sessionUsers = await dbContext.SessionUsers
            .Where(su => su.SessionId == sessionId && su.IsActiveInSession)
            .ToListAsync();
        
        foreach (var sessionUser in sessionUsers)
        {
            sessionUser.SnapshotActiveTime(now);
        }
        
        if (sessionUsers.Any())
        {
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }
}
