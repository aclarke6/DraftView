using DraftView.Domain.Entities;

namespace DraftView.Web.Models;

public class SupportDashboardViewModel
{
    public string SystemStatus { get; init; } = "Operational";
    public int ActiveAuthors { get; init; }
    public int ActiveReaders { get; init; }
    public SystemStateMessage? ActiveMessage { get; init; }
    public IReadOnlyList<SystemStateMessage> MessageHistory { get; init; } = [];
}
