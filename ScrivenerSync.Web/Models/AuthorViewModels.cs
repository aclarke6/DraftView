using ScrivenerSync.Domain.Entities;

namespace ScrivenerSync.Web.Models;

public class DashboardViewModel
{
    public ScrivenerProject? ActiveProject { get; set; }
    public IReadOnlyList<ScrivenerProject> AllProjects { get; set; } = new List<ScrivenerProject>();
    public IReadOnlyList<Section> PublishedSections { get; set; } = new List<Section>();
    public IReadOnlyList<EmailDeliveryLog> EmailFailures { get; set; } = new List<EmailDeliveryLog>();
    public int ActiveReaderCount { get; set; }
}

public class SectionViewModel
{
    public Section Section { get; set; } = default!;
    public IReadOnlyList<Comment> Comments { get; set; } = new List<Comment>();
    public int ReadCount { get; set; }
}

public class InviteReaderViewModel
{
    public string Email { get; set; } = string.Empty;
    public bool NeverExpires { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
}
