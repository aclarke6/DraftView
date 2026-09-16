using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using System.Net;

namespace DraftView.Application.Services;

/// <summary>
/// Sends personal notes from the author to a specific reader and maintains a sent log.
/// Emails are delivered from the DraftView address with Reply-To set to the author's address.
/// </summary>
public class ReaderMessageService(
    IUserRepository userRepo,
    IReaderMessageRepository messageRepo,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork) : IReaderMessageService
{
    /// <summary>
    /// Sends a note to the recipient and persists a sent-log record.
    /// Throws if either user is not found or the acting user is not the Author.
    /// </summary>
    public async Task SendAsync(Guid authorId, Guid recipientId, string subject, string body, CancellationToken ct = default)
    {
        var author = await userRepo.GetByIdAsync(authorId, ct)
            ?? throw new EntityNotFoundException(nameof(User), authorId);

        if (author.Role != Role.Author)
            throw new UnauthorisedOperationException("Only the Author may send notes to readers.");

        var recipient = await userRepo.GetByIdAsync(recipientId, ct)
            ?? throw new EntityNotFoundException(nameof(User), recipientId);

        var message = ReaderMessage.Create(authorId, recipientId, subject, body, DateTime.UtcNow);
        await messageRepo.AddAsync(message, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var encodedBody = WebUtility.HtmlEncode(body.Trim());
        var htmlBody = $"<p>{encodedBody}</p><p>&#8212; {WebUtility.HtmlEncode(author.DisplayName)}</p>";

        await emailSender.SendAsync(
            recipient.Email,
            recipient.DisplayName,
            subject.Trim(),
            htmlBody,
            ct,
            replyToEmail: author.Email);
    }

    /// <summary>
    /// Returns all messages sent by the author, ordered newest first.
    /// </summary>
    public Task<IReadOnlyList<ReaderMessage>> GetSentByAuthorAsync(Guid authorId, CancellationToken ct = default) =>
        messageRepo.GetByAuthorIdAsync(authorId, ct);
}
