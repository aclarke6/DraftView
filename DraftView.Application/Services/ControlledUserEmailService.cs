using DraftView.Application.Contracts;
using DraftView.Application.Interfaces;

namespace DraftView.Application.Services;

public sealed class ControlledUserEmailService(
    IUserEmailAccessService userEmailAccessService,
    IUserEmailProtectionService userEmailProtectionService) : IControlledUserEmailService
{
    public async Task<string> GetEmailAsync(UserEmailAccessRequest request, CancellationToken ct = default)
    {
        var accessResult = await userEmailAccessService.EvaluateAccessAsync(request, ct);
        if (!accessResult.IsAllowed)
            throw new InvalidOperationException(accessResult.Reason ?? "Email access denied.");

        return await userEmailProtectionService.GetEmailAsync(request.TargetUserId, ct);
    }
}
