using POS.Shared.Application.Messaging;

namespace Identity.Application.Users.Commands.ResetPassword
{
    public sealed record ResetPasswordCommand(string UserId, string NewPassword) : ICommand;
}
