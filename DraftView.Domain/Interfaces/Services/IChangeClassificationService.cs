using DraftView.Domain.Diff;
using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Classifies the nature of changes between two versions of prose content.
/// Uses a diff-based heuristic to assign Polish, Revision, or Rewrite.
/// The classification is advisory — it does not block or alter publishing.
/// Source-agnostic: makes no distinction between sync and import content.
/// </summary>
public interface IChangeClassificationService
{
    /// <summary>
    /// Classifies changes based on a paragraph-level diff result.
    /// Returns null when no diff exists (no previous version).
    /// </summary>
    ChangeClassification? Classify(IReadOnlyList<ParagraphDiffResult> diffParagraphs);
}
