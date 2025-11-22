using FifaTracker.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FifaTracker.Application.Users.Queries.GetInactiveUsers;

public class GetInactiveUsersQueryHandler : IRequestHandler<GetInactiveUsersQuery, List<InactiveUserDto>>
{
    private readonly IApplicationDbContext _context;

    public GetInactiveUsersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<InactiveUserDto>> Handle(GetInactiveUsersQuery request, CancellationToken cancellationToken)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(u => !u.IsActive)
            .OrderByDescending(u => u.DeactivatedAt)
            .Select(u => new InactiveUserDto(u.Id, u.Name, u.CreatedAt, u.DeactivatedAt))
            .ToListAsync(cancellationToken);
    }
}
