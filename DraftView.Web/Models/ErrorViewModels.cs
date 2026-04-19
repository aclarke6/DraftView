namespace DraftView.Web.Models;

public class ErrorPageViewModel
{
    public string Heading { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int StatusCode { get; set; } = 500;
    public string ErrorReference { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    public string SourceArea { get; set; } = string.Empty;
    public string? SystemName
    {
        get; set;
    }
    public string? ExceptionType
    {
        get; set;
    }
    public string? ExceptionMessage
    {
        get; set;
    }
    public string? StackTrace
    {
        get; set;
    }
    public string? InnerException
    {
        get; set;
    }
    public bool ShowTechnicalDetails
    {
        get; set;
    }
}