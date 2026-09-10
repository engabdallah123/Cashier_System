using Identity.Application.Services;
using Identity.Domain.Users;
using Identity.Domain.Users.Entities;
using Microsoft.AspNetCore.Identity;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Identity.Application.Auth.Commands.Register
{
    internal sealed class RegisterCommandHandler : ICommandHandler<RegisterCommand, AuthResponse>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IJwtTokenService _jwtTokenService;

        public RegisterCommandHandler(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IJwtTokenService jwtTokenService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _jwtTokenService = jwtTokenService;
        }

        public async Task<Result<AuthResponse>> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            var existingUser = await _userManager.FindByNameAsync(request.UserName);
            if (existingUser is not null)
                return Result<AuthResponse>.Failure(UserErrors.DuplicateUserName);

            var finalEmail = !string.IsNullOrWhiteSpace(request.Email) 
                ? request.Email.Trim() 
                : $"{request.UserName.Trim().ToLowerInvariant()}@pos.local";

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var existingEmail = await _userManager.FindByEmailAsync(request.Email.Trim());
                if (existingEmail is not null)
                    return Result<AuthResponse>.Failure(UserErrors.DuplicateEmail);
            }

            var user = new ApplicationUser
            {
                FullName = request.FullName.Trim(),
                UserName = request.UserName.Trim(),
                Email = finalEmail,
                Phone = request.Phone?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
                return Result<AuthResponse>.Failure(UserErrors.RegistrationFailed);

            // تأكد من وجود الدور وإلا أنشئه
            if (!await _roleManager.RoleExistsAsync(request.Role))
                await _roleManager.CreateAsync(new IdentityRole(request.Role));

            var roleResult = await _userManager.AddToRoleAsync(user, request.Role);
            if (!roleResult.Succeeded)
                return Result<AuthResponse>.Failure(UserErrors.RoleAssignmentFailed);

            var roles = await _userManager.GetRolesAsync(user);
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
                request.Role));
        }
    }
}
