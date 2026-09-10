using Identity.Domain.Users;
using Identity.Domain.Users.Entities;
using Microsoft.AspNetCore.Identity;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Identity.Application.Users.Commands.ResetPassword
{
    internal sealed class ResetPasswordCommandHandler : ICommandHandler<ResetPasswordCommand>
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ResetPasswordCommandHandler(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user is null)
                return Result.Failure(UserErrors.NotFound(request.UserId));

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 4)
                return Result.Failure(new Error("User.InvalidPassword", "كلمة المرور يجب أن لا تقل عن 4 خانات."));

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
                return Result.Failure(new Error("User.PasswordResetFailed", $"فشل تعيين كلمة المرور: {errors}"));
            }

            return Result.Success();
        }
    }
}
