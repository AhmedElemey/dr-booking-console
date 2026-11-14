namespace Console.Api.Common;

public sealed class ApiException(string code, string? detail = null, IDictionary<string, string[]>? errors = null, IDictionary<string, object?>? extensions = null)
    : Exception(detail ?? code)
{
    public string Code { get; } = code;

    public string? Detail { get; } = detail;

    public IDictionary<string, string[]>? Errors { get; } = errors;

    public IDictionary<string, object?>? Extensions { get; } = extensions;

    public static ApiException NotFound(string code = ErrorCodes.NotFound) => new(code);

    public static ApiException Validation(string field, string message) =>
        new(ErrorCodes.ValidationFailed, errors: new Dictionary<string, string[]> { [field] = [message] });
}

public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string ClinicNotFound = "CLINIC_NOT_FOUND";
    public const string JobNotFound = "JOB_NOT_FOUND";
    public const string FeatureNotFound = "FEATURE_NOT_FOUND";
    public const string FeatureNotAvailable = "FEATURE_NOT_AVAILABLE";
    public const string SlugTaken = "SLUG_TAKEN";
    public const string EmailTaken = "EMAIL_TAKEN";
    public const string LastAdmin = "LAST_ADMIN";
    public const string JobInProgress = "JOB_IN_PROGRESS";
    public const string ClinicNotActive = "CLINIC_NOT_ACTIVE";
    public const string InternalError = "INTERNAL_ERROR";

    private static readonly Dictionary<string, (int Status, string Title, string Detail)> Catalog = new()
    {
        [ValidationFailed] = (400, "Validation failed", "One or more fields are invalid."),
        [Unauthorized] = (401, "Unauthorized", "Authentication is required or the token has expired."),
        [InvalidCredentials] = (401, "Invalid credentials", "The email or password is incorrect."),
        [Forbidden] = (403, "Forbidden", "You are not allowed to perform this action."),
        [NotFound] = (404, "Not found", "The requested resource was not found."),
        [ClinicNotFound] = (404, "Clinic not found", "The clinic does not exist."),
        [JobNotFound] = (404, "Job not found", "The provisioning job does not exist."),
        [FeatureNotFound] = (404, "Feature not found", "The feature key is not in the catalog."),
        [FeatureNotAvailable] = (409, "Feature not available", "This feature is coming soon and cannot be enabled yet."),
        [SlugTaken] = (409, "Slug already used", "Another clinic already uses this slug."),
        [EmailTaken] = (409, "Email already used", "A super admin with this email already exists."),
        [LastAdmin] = (409, "Last super admin", "You cannot deactivate yourself or the last active super admin."),
        [JobInProgress] = (409, "Job in progress", "A provisioning job is already queued or running for this clinic."),
        [ClinicNotActive] = (409, "Clinic not active", "The clinic must be provisioned and active for this action."),
        [InternalError] = (500, "Internal error", "Something went wrong."),
    };

    public static int StatusOf(string code) => Catalog[code].Status;

    public static string TitleOf(string code) => Catalog[code].Title;

    public static string DetailOf(string code) => Catalog[code].Detail;

    public static string FromStatus(int status) => status switch
    {
        401 => Unauthorized,
        403 => Forbidden,
        404 => NotFound,
        >= 500 => InternalError,
        _ => ValidationFailed,
    };
}
