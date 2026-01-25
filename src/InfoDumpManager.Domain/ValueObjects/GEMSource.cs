using System;
using System.Text.RegularExpressions;

namespace InfoDumpManager.Domain.ValueObjects;

public sealed record GEMSource
{
    private static readonly Regex UrlRegex = new(
        "^(https?://).+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string Url { get; }

    private GEMSource(string url) => Url = url;

    public static GEMSource Create(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be empty", nameof(url));
        }

        if (!UrlRegex.IsMatch(url) || !Uri.IsWellFormedUriString(url, UriKind.Absolute))
        {
            throw new ArgumentException("Invalid URL format", nameof(url));
        }

        return new GEMSource(url.Trim());
    }
}
