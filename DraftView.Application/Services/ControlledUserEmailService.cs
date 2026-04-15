using DraftView.Application.Contracts;
using DraftView.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace DraftView.Application.Services;

public sealed class ControlledUserEmailService(
    IUserEmailAccessService userEmailAccessService,
    IUserEmailProtectionService userEmailProtectionService,
    ILogger<ControlledUserEmailService> logger) : IControlledUserEmailService
{
    public async Task<string> GetEmailAsync(UserEmailAccessRequest request, CancellationToken ct = default)
    {
        var accessResult = await userEmailAccessService.EvaluateAccessAsync(request, ct);
        LogAuditRecord(request, accessResult);

        if (!accessResult.IsAllowed)
            throw new InvalidOperationException(accessResult.Reason ?? "Email access denied.");

        return await userEmailProtectionService.GetEmailAsync(request.TargetUserId, ct);
    }

    private void LogAuditRecord(UserEmailAccessRequest request, UserEmailAccessResult accessResult)
    {
        if (accessResult.IsAllowed)
        {
            logger.LogInformation(
                "Controlled email access allowed for requester {RequestingUserId} targeting {TargetUserId} with role {RequestingUserRole} for {Purpose}. Reason: {Reason}",
                request.RequestingUserId,
                request.TargetUserId,
                request.RequestingUserRole,
                request.Purpose,
                accessResult.Reason);
            return;
        }

        logger.LogWarning(
            "Controlled email access denied for requester {RequestingUserId} targeting {TargetUserId} with role {RequestingUserRole} for {Purpose}. Reason: {Reason}",
            request.RequestingUserId,
            request.TargetUserId,
            request.RequestingUserRole,
            request.Purpose,
            accessResult.Reason);
    }
}
