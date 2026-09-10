using Identity.Domain.Users.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Identity.Application.Auth.Queries.GetInitialSetupStatus
{
    internal sealed class GetInitialSetupStatusQueryHandler : IQueryHandler<GetInitialSetupStatusQuery, bool>
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public GetInitialSetupStatusQueryHandler(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<Result<bool>> Handle(GetInitialSetupStatusQuery request, CancellationToken cancellationToken)
        {
            // Returns true if NO admin user exists (initial setup required)
            var hasAnyUsers = await _userManager.Users.AnyAsync(cancellationToken);
            if (!hasAnyUsers)
            {
                return Result<bool>.Success(true);
            }

            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var adminExists = adminUsers.Any();
            return Result<bool>.Success(!adminExists);
        }
    }
}
