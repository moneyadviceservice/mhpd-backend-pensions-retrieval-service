namespace PensionsRetrievalFunction.Models;

public class PeiRequest
{
    public string? Rpt { get; set; }

    public string? Iss { get; set; }

    public string? PeisId { get; set; }

    public string? UserSessionId { get; set; }

    public string CorrelationId { get; set; } = string.Empty;
}
