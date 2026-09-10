using POS.Shared.Application.Messaging;

namespace Identity.Application.Users.Commands.DeleteUser
{
    public sealed record DeleteUserCommand(string Id) : ICommand;
}
