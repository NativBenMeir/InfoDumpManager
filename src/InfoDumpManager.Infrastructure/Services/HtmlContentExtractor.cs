using System;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace InfoDumpManager.Infrastructure.Services;

public interface IHtmlContentExtractor
{
    string ExtractMainText(string htmlContent);
}

public sealed class AngleSharpHtmlContentExtractor : IHtmlContentExtractor
{
    private static readonly string[] PrimarySelectors =
    {
        "article",
        "main",
        "[role='main']",
        "#content",
        ".content",
        ".article",
        ".post"
    };

    private static readonly string[] RemoveSelectors =
    {
        "script",
        "style",
        "noscript",
        "nav",
        "header",
        "footer",
        "aside",
        "form",
        "iframe"
    };

    public string ExtractMainText(string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
        {
            throw new ArgumentException("HTML content must be provided.", nameof(htmlContent));
        }

        var parser = new HtmlParser();
        var document = parser.ParseDocument(htmlContent);

        foreach (var selector in RemoveSelectors)
        {
            var nodes = document.QuerySelectorAll(selector);
            foreach (var node in nodes)
            {
                node.Remove();
            }
        }

        IElement? root = null;
        foreach (var selector in PrimarySelectors)
        {
            root = document.QuerySelector(selector);
            if (root is not null)
            {
                break;
            }
        }

        root ??= document.Body;

        var text = NormalizeWhitespace(root?.TextContent ?? string.Empty);

        if (text.Length < 200 && root != document.Body && document.Body is not null)
        {
            text = NormalizeWhitespace(document.Body.TextContent ?? string.Empty);
        }

        return text;
    }

    private static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = Regex.Replace(text, "[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\r?\n\s*", "\n");
        return normalized.Trim();
    }
}
