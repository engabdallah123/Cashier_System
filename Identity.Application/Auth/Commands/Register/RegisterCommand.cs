using POS.Shared.Application.Messaging;

namespace Identity.Application.Auth.Commands.Register
{
    public sealed record RegisterCommand(
        string FullName,
        string UserName,
        string Password,
        string Role,
        string? Email = null,
        string? Phone = null) : ICommand<AuthResponse>;
}
