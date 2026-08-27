using Identity.Application.Auth;
using POS.Shared.Application.Messaging;

namespace Identity.Application.Auth.Commands.SetupInitialAdmin
{
    public sealed record SetupInitialAdminCommand(
        string FullName,
        string UserName,
        string? Email,
        string? Phone,
        string Password,
        string? StoreName = null
    ) : ICommand<AuthResponse>;
}
