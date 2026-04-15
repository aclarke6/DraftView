using DraftView.Application.Contracts;
using DraftView.Application.Interfaces;
using DraftView.Domain.Enumerations;

namespace DraftView.Application.Services;

public sealed class UserEmailAccessService : IUserEmailAccessService
{
    public Task<UserEmailAccessResult> EvaluateAccessAsync(
        UserEmailAccessRequest request,
        CancellationToken ct = default)
    {
        if (request.RequestingUserId == request.TargetUserId)
            return Task.FromResult(new UserEmailAccessResult(true));

        return Task.FromResult(new UserEmailAccessResult(false, "Email access denied."));
    }
}
