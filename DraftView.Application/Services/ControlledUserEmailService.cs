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
        var auditTimestampUtc = DateTimeOffset.UtcNow;
        var accessOutcome = accessResult.IsAllowed ? "Allowed" : "Denied";

        if (accessResult.IsAllowed)
        {
            logger.LogInformation(
                "Controlled email access {AccessOutcome} for requester {RequestingUserId} targeting {TargetUserId} with role {RequestingUserRole} for {Purpose} at {AuditTimestampUtc}. Reason: {Reason}",
                accessOutcome,
                request.RequestingUserId,
                request.TargetUserId,
                request.RequestingUserRole,
                request.Purpose,
                auditTimestampUtc,
                accessResult.Reason);
            return;
        }

        logger.LogWarning(
            "Controlled email access {AccessOutcome} for requester {RequestingUserId} targeting {TargetUserId} with role {RequestingUserRole} for {Purpose} at {AuditTimestampUtc}. Reason: {Reason}",
            accessOutcome,
            request.RequestingUserId,
            request.TargetUserId,
            request.RequestingUserRole,
            request.Purpose,
            auditTimestampUtc,
            accessResult.Reason);
    }
}
