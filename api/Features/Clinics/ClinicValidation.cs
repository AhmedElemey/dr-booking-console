using System.Text.RegularExpressions;

using Console.Api.Common;
using Console.Api.Features.Catalog;

namespace Console.Api.Features.Clinics;

public static partial class ClinicValidation
{
    public const int MaxLogoBytes = 100_000;

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorRegex();

    [GeneratedRegex(@"^\+?[0-9][0-9 \-]{5,19}$")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"^([a-z0-9]([a-z0-9\-]*[a-z0-9])?\.)+[a-z]{2,}$")]
    private static partial Regex DomainRegex();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$")]
    private static partial Regex AppIdRegex();

    // The Android id doubles as the payment-return URL scheme (<id>://payment-return), and a URI scheme
    // cannot contain "_" (RFC 3986), so unlike iosBundleId it must be lowercase letters and digits only.
    [GeneratedRegex(@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$")]
    private static partial Regex AndroidAppIdRegex();

    [GeneratedRegex(@"<script|on[a-z]+\s*=|javascript:|<foreignObject|<iframe", RegexOptions.IgnoreCase)]
    private static partial Regex UnsafeSvgRegex();

    public static string DefaultAppId(string slug)
    {
        var compact = slug.Replace("-", string.Empty, StringComparison.Ordinal);
        return $"com.{(char.IsDigit(compact[0]) ? "c" : string.Empty)}{compact}.app";
    }

    public static bool IsValidSlug(string? slug) => slug is { Length: > 0 and <= 40 } && SlugRegex().IsMatch(slug);

    // Validates a full create request, applies defaults, and returns the normalised copy. Throws 400/409.
    public static ClinicRequest Normalize(ClinicRequest r)
    {
        var errors = new Dictionary<string, string[]>();
        var slug = (r.Slug ?? string.Empty).Trim();
        if (!IsValidSlug(slug))
        {
            errors["slug"] = ["Use lowercase letters, digits and single hyphens, up to 40 characters."];
        }

        var normalized = r with
        {
            Slug = slug,
            NameAr = Required(errors, "nameAr", r.NameAr, 100),
            NameEn = Required(errors, "nameEn", r.NameEn, 100),
            TaglineAr = Optional(errors, "taglineAr", r.TaglineAr, 200),
            TaglineEn = Optional(errors, "taglineEn", r.TaglineEn, 200),
            PrimaryColor = Color(errors, "primaryColor", r.PrimaryColor),
            AccentColor = Color(errors, "accentColor", r.AccentColor),
            SupportPhone = Phone(errors, r.SupportPhone),
            Domain = Domain(errors, r.Domain),
            AndroidApplicationId = AppId(errors, "androidApplicationId", r.AndroidApplicationId, slug, AndroidAppIdRegex(), AndroidAppIdMessage),
            IosBundleId = AppId(errors, "iosBundleId", r.IosBundleId, slug, AppIdRegex(), IosAppIdMessage),
            LogoSvg = Logo(errors, r.LogoSvg),
        };

        if (errors.Count > 0)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, errors: errors);
        }

        return normalized;
    }

    public static ClinicPatch Normalize(ClinicPatch p)
    {
        var errors = new Dictionary<string, string[]>();
        var normalized = p with
        {
            NameAr = p.NameAr is null ? null : Required(errors, "nameAr", p.NameAr, 100),
            NameEn = p.NameEn is null ? null : Required(errors, "nameEn", p.NameEn, 100),
            TaglineAr = p.TaglineAr is null ? null : Optional(errors, "taglineAr", p.TaglineAr, 200),
            TaglineEn = p.TaglineEn is null ? null : Optional(errors, "taglineEn", p.TaglineEn, 200),
            PrimaryColor = p.PrimaryColor is null ? null : Color(errors, "primaryColor", p.PrimaryColor),
            AccentColor = p.AccentColor is null ? null : Color(errors, "accentColor", p.AccentColor),
            SupportPhone = p.SupportPhone is null ? null : Phone(errors, p.SupportPhone),
            Domain = p.Domain is null ? null : Domain(errors, p.Domain),
            AndroidApplicationId = p.AndroidApplicationId is null ? null : AppId(errors, "androidApplicationId", p.AndroidApplicationId, null, AndroidAppIdRegex(), AndroidAppIdMessage),
            IosBundleId = p.IosBundleId is null ? null : AppId(errors, "iosBundleId", p.IosBundleId, null, AppIdRegex(), IosAppIdMessage),
            LogoSvg = p.LogoSvg is null ? null : Logo(errors, p.LogoSvg) ?? string.Empty,
        };

        if (errors.Count > 0)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, errors: errors);
        }

        return normalized;
    }

    // Feature switches: unknown keys are a 400, enabling a coming-soon feature is a 409.
    public static Dictionary<string, bool> ResolveFeatures(Dictionary<string, bool>? requested)
    {
        var result = FeatureCatalog.Available.ToDictionary(f => f.Key, f => f.DefaultEnabled);
        if (requested is null)
        {
            return result;
        }

        var unknown = requested.Keys.Where(k => FeatureCatalog.Find(k) is null).ToList();
        if (unknown.Count > 0)
        {
            throw ApiException.Validation("features", $"Unknown feature: {string.Join(", ", unknown)}.");
        }

        foreach (var (key, enabled) in requested)
        {
            var def = FeatureCatalog.Find(key)!;
            if (!def.Available)
            {
                if (enabled)
                {
                    throw new ApiException(ErrorCodes.FeatureNotAvailable, extensions: new Dictionary<string, object?> { ["feature"] = key });
                }

                continue;
            }

            result[key] = enabled;
        }

        return result;
    }

    private static string Required(Dictionary<string, string[]> errors, string field, string? value, int max)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0)
        {
            errors[field] = ["This field is required."];
        }
        else if (v.Length > max)
        {
            errors[field] = [$"At most {max} characters."];
        }

        return v;
    }

    private static string Optional(Dictionary<string, string[]> errors, string field, string? value, int max)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length > max)
        {
            errors[field] = [$"At most {max} characters."];
        }

        return v;
    }

    private static string Color(Dictionary<string, string[]> errors, string field, string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (!ColorRegex().IsMatch(v))
        {
            errors[field] = ["Use a hex colour like #0F766E."];
        }

        return v.ToUpperInvariant();
    }

    private static string Phone(Dictionary<string, string[]> errors, string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (!PhoneRegex().IsMatch(v))
        {
            errors["supportPhone"] = ["Enter a phone number like +201000000000."];
        }

        return v;
    }

    private static string Domain(Dictionary<string, string[]> errors, string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (v.Length > 0 && !DomainRegex().IsMatch(v))
        {
            errors["domain"] = ["Enter a domain like smilecare.example (no https://)."];
        }

        return v;
    }

    private const string IosAppIdMessage = "Use a reverse-domain id like com.smilecare.app.";

    private const string AndroidAppIdMessage = "Use lowercase letters and digits in a reverse-domain id like com.smilecare.app. Underscores are not allowed: the id is also the payment-return URL scheme.";

    private static string AppId(Dictionary<string, string[]> errors, string field, string? value, string? slug, Regex pattern, string message)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0 && slug is { Length: > 0 } && IsValidSlug(slug))
        {
            return DefaultAppId(slug);
        }

        if (!pattern.IsMatch(v) || v.Length > 100)
        {
            errors[field] = [message];
        }

        return v;
    }

    private static string? Logo(Dictionary<string, string[]> errors, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var v = value.Trim();
        if (System.Text.Encoding.UTF8.GetByteCount(v) > MaxLogoBytes)
        {
            errors["logoSvg"] = ["The logo must be smaller than 100 KB."];
        }
        else if (!Regex.IsMatch(v, @"^(<\?xml[^>]*\?>\s*)?(<!--.*?-->\s*)*<svg[\s>]", RegexOptions.Singleline | RegexOptions.IgnoreCase) || UnsafeSvgRegex().IsMatch(v))
        {
            errors["logoSvg"] = ["Upload a plain SVG file without scripts."];
        }

        return v;
    }
}
