using Identity.Application.Services;
using Identity.Domain.Users;
using Identity.Domain.Users.Entities;
using Microsoft.AspNetCore.Identity;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Identity.Application.Auth.Commands.Login
{
    internal sealed class LoginCommandHandler : ICommandHandler<LoginCommand, AuthResponse>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IJwtTokenService _jwtTokenService;

        public LoginCommandHandler(
            UserManager<ApplicationUser> userManager,
            IJwtTokenService jwtTokenService)
        {
            _userManager = userManager;
            _jwtTokenService = jwtTokenService;
        }

        public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByNameAsync(request.UserName);
            if (user is null)
                return Result<AuthResponse>.Failure(UserErrors.InvalidCredentials);

            if (!user.IsActive)
                return Result<AuthResponse>.Failure(UserErrors.UserInactive);

            var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!isPasswordValid)
                return Result<AuthResponse>.Failure(UserErrors.InvalidCredentials);

            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _jwtTokenService.GenerateAccessToken(user, roles);
            var refreshToken = _jwtTokenService.GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            await _userManager.UpdateAsync(user);

            return Result<AuthResponse>.Success(new AuthResponse(
                accessToken,
                refreshToken,
                DateTime.UtcNow.AddDays(7),
                user.Id,
                user.FullName,
                roles.FirstOrDefault() ?? "Cashier"));
        }
    }
}
