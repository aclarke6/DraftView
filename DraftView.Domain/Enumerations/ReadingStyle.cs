namespace DraftView.Domain.Enumerations;

/// <summary>
/// Controls the minimum change classification a reader sees when revisiting a chapter.
/// Maps to the reader's self-described reading purpose.
/// </summary>
public enum ReadingStyle
{
    BetaReader    = 0,  // Show everything, including typos (Trivial and above)
    StoryReader   = 1,  // Minor edits and above (Polish and above) — default
    AlphaReader   = 2,  // Meaningful revisions and above (Revision and above)
    StructureOnly = 3   // Major rewrites only (Rewrite)
}
