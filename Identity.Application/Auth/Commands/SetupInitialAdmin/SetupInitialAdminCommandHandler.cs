using Identity.Application.Auth;
using Identity.Application.Services;
using Identity.Domain.Users;
using Identity.Domain.Users.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Identity.Application.Auth.Commands.SetupInitialAdmin
{
    internal sealed class SetupInitialAdminCommandHandler : ICommandHandler<SetupInitialAdminCommand, AuthResponse>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IJwtTokenService _jwtTokenService;

        public SetupInitialAdminCommandHandler(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IJwtTokenService jwtTokenService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _jwtTokenService = jwtTokenService;
        }

        public async Task<Result<AuthResponse>> Handle(SetupInitialAdminCommand request, CancellationToken cancellationToken)
        {
            // Guard: Initial admin setup can only run once when no admin exists
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            if (adminUsers.Any())
            {
                return Result<AuthResponse>.Failure(new Error("Auth.SetupAlreadyCompleted", "تم تهيئة حساب المدير مسبقاً ولا يمكن إنشاء مدير جديد من هذه الصفحة."));
            }

            // Ensure Admin role exists
            if (!await _roleManager.RoleExistsAsync("Admin"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            var email = string.IsNullOrWhiteSpace(request.Email) 
                ? $"{request.UserName.Trim().ToLower()}@pos.local" 
                : request.Email.Trim();

            var user = new ApplicationUser
            {
                FullName = request.FullName.Trim(),
                UserName = request.UserName.Trim(),
                Email = email,
                Phone = request.Phone?.Trim(),
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                return Result<AuthResponse>.Failure(new Error("Auth.CreationFailed", $"فشل إنشاء حساب المدير: {errors}"));
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "Admin");
            if (!roleResult.Succeeded)
            {
                return Result<AuthResponse>.Failure(UserErrors.RoleAssignmentFailed);
            }

            var roles = new List<string> { "Admin" };
            var accessToken = _jwtTokenService.GenerateAccessToken(user, roles);
            var refreshToken = _jwtTokenService.GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            await _userManager.UpdateAsync(user);

            return Result<AuthResponse>.Success(new AuthResponse(
                accessToken,
                refreshToken,
                DateTime.UtcNow.AddHours(1),
                user.Id,
                user.FullName,
                "Admin"
            ));
        }
    }
}
