using Identity.Domain.Users;
using Identity.Domain.Users.Entities;
using Microsoft.AspNetCore.Identity;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Identity.Application.Users.Commands.DeleteUser
{
    internal sealed class DeleteUserCommandHandler : ICommandHandler<DeleteUserCommand>
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public DeleteUserCommandHandler(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<Result> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(request.Id);
            if (user is null)
                return Result.Failure(UserErrors.NotFound(request.Id));

            // Prevent deleting the main Admin if it's the last admin
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin") || roles.Contains("Administrator"))
            {
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                var administrators = await _userManager.GetUsersInRoleAsync("Administrator");
                if (admins.Count + administrators.Count <= 1)
                {
                    return Result.Failure(new Error("User.CannotDeleteLastAdmin", "لا يمكن حذف حساب المسؤول الأخير في النظام."));
                }
            }

            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                var errorMsg = string.Join(", ", deleteResult.Errors.Select(e => e.Description));
                return Result.Failure(new Error("User.DeleteFailed", $"فشل حذف المستخدم: {errorMsg}"));
            }

            return Result.Success();
        }
    }
}
