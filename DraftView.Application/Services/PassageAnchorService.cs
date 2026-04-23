using System.Net;
using System.Text.RegularExpressions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.ValueObjects;

namespace DraftView.Application.Services;

/// <summary>
/// Orchestrates passage anchor creation and retrieval for authorized callers.
/// </summary>
public sealed class PassageAnchorService(
    IPassageAnchorRepository anchorRepo,
    ISectionRepository sectionRepo,
    ISectionVersionRepository sectionVersionRepo,
    IReaderAccessRepository readerAccessRepo,
    IUserRepository userRepo,
    IAuthorizationFacade authFacade,
    IUnitOfWork unitOfWork) : IPassageAnchorService
{
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);

    /// <summary>
    /// Creates a new passage anchor from a reader-visible selection after enforcing section access.
    /// </summary>
    public Task<PassageAnchorDto> CreateAsync(
        CreatePassageAnchorRequest request,
        Guid currentUserId,
        CancellationToken ct = default) =>
        CreateInternalAsync(request, currentUserId, ct);

    /// <summary>
    /// Validates a reader-visible selection without persisting a passage anchor.
    /// </summary>
    public Task ValidateSelectionAsync(
        CreatePassageAnchorRequest request,
        Guid currentUserId,
        CancellationToken ct = default) =>
        ValidateInternalAsync(request, currentUserId, ct);

    /// <summary>
    /// Retrieves a passage anchor for an authorized caller and returns its current status metadata.
    /// </summary>
    public async Task<PassageAnchorDto> GetByIdAsync(
        Guid anchorId,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var anchor = await anchorRepo.GetByIdAsync(anchorId, ct)
            ?? throw new EntityNotFoundException(nameof(PassageAnchor), anchorId);
        var section = await sectionRepo.GetByIdAsync(anchor.SectionId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), anchor.SectionId);

        await EnsureAuthorizedAsync(section, currentUserId, ct);

        return Map(anchor);
    }

    /// <summary>
    /// Executes create orchestration after the public wrapper delegates to the async implementation.
    /// </summary>
    private async Task<PassageAnchorDto> CreateInternalAsync(
        CreatePassageAnchorRequest request,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var section = await sectionRepo.GetByIdAsync(request.SectionId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), request.SectionId);

        var source = await ValidateSelectionInternalAsync(section, request, currentUserId, ct);

        var snapshot = PassageAnchorSnapshot.Create(
            request.SelectedText,
            request.NormalizedSelectedText,
            request.SelectedTextHash,
            request.PrefixContext,
            request.SuffixContext,
            request.StartOffset,
            request.EndOffset,
            request.CanonicalContentHash,
            request.HtmlSelectorHint);
        var anchor = PassageAnchor.Create(
            section.Id,
            source.OriginalSectionVersionId,
            request.Purpose,
            currentUserId,
            snapshot);

        await anchorRepo.AddAsync(anchor, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Map(anchor);
    }

    /// <summary>
    /// Executes validation without persisting a passage anchor.
    /// </summary>
    private async Task ValidateInternalAsync(
        CreatePassageAnchorRequest request,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var section = await sectionRepo.GetByIdAsync(request.SectionId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), request.SectionId);

        await ValidateSelectionInternalAsync(section, request, currentUserId, ct);
    }

    /// <summary>
    /// Resolves the reader-visible content source and validates the supplied selection against it.
    /// </summary>
    private async Task<ReaderVisibleSource> ValidateSelectionInternalAsync(
        Section section,
        CreatePassageAnchorRequest request,
        Guid currentUserId,
        CancellationToken ct)
    {
        await EnsureAuthorizedAsync(section, currentUserId, ct);

        var latestVersion = await sectionVersionRepo.GetLatestAsync(section.Id, ct);
        var source = ResolveReaderVisibleSource(section, latestVersion, request.OriginalSectionVersionId);

        ValidateSelection(request, source.ReaderVisibleText);
        return source;
    }

    /// <summary>
    /// Enforces that the current caller can access anchor data for the section.
    /// </summary>
    private async Task EnsureAuthorizedAsync(Section section, Guid currentUserId, CancellationToken ct)
    {
        _ = await userRepo.GetByIdAsync(currentUserId, ct)
            ?? throw new EntityNotFoundException(nameof(User), currentUserId);

        if (authFacade.IsAuthor() || authFacade.IsSystemSupport())
            return;

        if (!authFacade.IsBetaReader())
            throw new UnauthorisedOperationException("Only authorized users may access passage anchors.");

        if (!section.IsPublished)
            throw new UnauthorisedOperationException("Beta readers may only anchor published sections.");

        var access = await readerAccessRepo.GetByReaderAndProjectAsync(currentUserId, section.ProjectId, ct);
        if (access is null || !access.IsActive)
            throw new UnauthorisedOperationException("Beta reader does not have access to this project.");
    }

    /// <summary>
    /// Resolves the exact reader-visible content source, preferring the latest section version when present.
    /// </summary>
    private static ReaderVisibleSource ResolveReaderVisibleSource(
        Section section,
        SectionVersion? latestVersion,
        Guid? requestedVersionId)
    {
        if (latestVersion is not null)
        {
            if (requestedVersionId != latestVersion.Id)
                throw new InvariantViolationException(
                    "I-ANCHOR-VERSION",
                    "Anchors must be created against the current reader-visible section version.");

            return new ReaderVisibleSource(latestVersion.Id, Canonicalize(latestVersion.HtmlContent));
        }

        if (requestedVersionId.HasValue)
            throw new InvariantViolationException(
                "I-ANCHOR-VERSION",
                "A section version cannot be specified when no reader-visible version exists.");

        if (string.IsNullOrWhiteSpace(section.HtmlContent))
            throw new InvariantViolationException(
                "I-ANCHOR-CONTENT",
                "Cannot create an anchor for a section with no reader-visible content.");

        return new ReaderVisibleSource(null, Canonicalize(section.HtmlContent));
    }

    /// <summary>
    /// Verifies that the requested selection and surrounding context match the reader-visible content.
    /// </summary>
    private static void ValidateSelection(CreatePassageAnchorRequest request, string readerVisibleText)
    {
        if (request.StartOffset < 0 ||
            request.EndOffset <= request.StartOffset ||
            request.EndOffset > readerVisibleText.Length)
            throw new InvariantViolationException(
                "I-ANCHOR-SELECTION",
                "Passage anchor offsets do not resolve within the reader-visible content.");

        var selectedText = readerVisibleText[request.StartOffset..request.EndOffset];
        if (!string.Equals(selectedText, request.NormalizedSelectedText, StringComparison.Ordinal))
            throw new InvariantViolationException(
                "I-ANCHOR-SELECTION",
                "Passage anchor selection does not match the reader-visible content.");

        var normalizedSelectedText = Canonicalize(request.SelectedText);
        if (!string.Equals(normalizedSelectedText, request.NormalizedSelectedText, StringComparison.Ordinal))
            throw new InvariantViolationException(
                "I-ANCHOR-SELECTION",
                "Passage anchor selected text does not match its normalized form.");

        var prefixStart = Math.Max(0, request.StartOffset - request.PrefixContext.Length);
        var prefix = readerVisibleText[prefixStart..request.StartOffset];
        if (!string.Equals(prefix, request.PrefixContext, StringComparison.Ordinal))
            throw new InvariantViolationException(
                "I-ANCHOR-SELECTION",
                "Passage anchor prefix context does not match the reader-visible content.");

        var suffixEnd = Math.Min(readerVisibleText.Length, request.EndOffset + request.SuffixContext.Length);
        var suffix = readerVisibleText[request.EndOffset..suffixEnd];
        if (!string.Equals(suffix, request.SuffixContext, StringComparison.Ordinal))
            throw new InvariantViolationException(
                "I-ANCHOR-SELECTION",
                "Passage anchor suffix context does not match the reader-visible content.");
    }

    /// <summary>
    /// Converts HTML or selected text into the canonical plain-text form used for offset validation.
    /// </summary>
    private static string Canonicalize(string text)
    {
        var withoutTags = HtmlTagRegex.Replace(text, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return WhitespaceRegex.Replace(decoded, " ").Trim();
    }

    /// <summary>
    /// Maps a passage anchor aggregate to its service DTO shape.
    /// </summary>
    private static PassageAnchorDto Map(PassageAnchor anchor)
    {
        return new PassageAnchorDto(
            anchor.Id,
            anchor.SectionId,
            anchor.OriginalSectionVersionId,
            anchor.Purpose,
            anchor.CreatedByUserId,
            anchor.CreatedAt,
            anchor.Status,
            anchor.UpdatedAt,
            new PassageAnchorSnapshotDto(
                anchor.OriginalSnapshot.SelectedText,
                anchor.OriginalSnapshot.NormalizedSelectedText,
                anchor.OriginalSnapshot.SelectedTextHash,
                anchor.OriginalSnapshot.PrefixContext,
                anchor.OriginalSnapshot.SuffixContext,
                anchor.OriginalSnapshot.StartOffset,
                anchor.OriginalSnapshot.EndOffset,
                anchor.OriginalSnapshot.CanonicalContentHash,
                anchor.OriginalSnapshot.HtmlSelectorHint),
            anchor.CurrentMatch is null
                ? null
                : new PassageAnchorMatchDto(
                    anchor.CurrentMatch.TargetSectionVersionId,
                    anchor.CurrentMatch.StartOffset,
                    anchor.CurrentMatch.EndOffset,
                    anchor.CurrentMatch.MatchedText,
                    anchor.CurrentMatch.ConfidenceScore,
                    anchor.CurrentMatch.MatchMethod,
                    anchor.CurrentMatch.ResolvedAt,
                    anchor.CurrentMatch.ResolvedByUserId,
                    anchor.CurrentMatch.Reason));
    }

    private sealed record ReaderVisibleSource(Guid? OriginalSectionVersionId, string ReaderVisibleText);
}
