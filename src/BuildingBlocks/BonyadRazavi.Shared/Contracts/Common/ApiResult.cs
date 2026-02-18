namespace BonyadRazavi.Shared.Contracts.Common;

public sealed class ApiResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<string> Errors { get; init; } = [];
    public string? CorrelationId { get; init; }

    public static ApiResult Ok(string message = "") =>
        new()
        {
            Success = true,
            Message = message
        };

    public static ApiResult Fail(
        string message,
        IEnumerable<string>? errors = null,
        string? correlationId = null) =>
        new()
        {
            Success = false,
            Message = message,
            Errors = errors?
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? [],
            CorrelationId = correlationId
        };
}
