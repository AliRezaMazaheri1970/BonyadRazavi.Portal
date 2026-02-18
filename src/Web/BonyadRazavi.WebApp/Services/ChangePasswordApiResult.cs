namespace BonyadRazavi.WebApp.Services;

public sealed record ChangePasswordApiResult(
    bool IsSuccess,
    string Message,
    int? StatusCode,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> FieldErrors)
{
    public static ChangePasswordApiResult Succeeded(string message) =>
        new(true, message, null, new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase));

    public static ChangePasswordApiResult Failed(
        string message,
        int? statusCode = null,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? fieldErrors = null) =>
        new(
            false,
            message,
            statusCode,
            fieldErrors ?? new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase));
}
