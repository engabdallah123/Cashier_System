using POS.Shared.Application.Messaging;

namespace Identity.Application.Auth.Queries.GetInitialSetupStatus
{
    public sealed record GetInitialSetupStatusQuery : IQuery<bool>;
}
