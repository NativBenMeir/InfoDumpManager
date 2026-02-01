using System;
using FluentAssertions;
using InfoDumpManager.Infrastructure.Services;
using Xunit;

namespace InfoDumpManager.Tests.Unit;

public sealed class WebScrapingUtilitiesTests
{
    [Theory]
    [InlineData("https://example.com", "https://example.com/")]
    [InlineData("http://example.com/path?query=1#fragment", "http://example.com/path?query=1")]
    public void NormalizeUrl_WhenValid_ReturnsNormalizedUrl(string input, string expected)
    {
        var normalized = WebScrapingUtilities.NormalizeUrl(input);

        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("notaurl")]
    [InlineData("ftp://example.com")]
    public void NormalizeUrl_WhenInvalid_Throws(string input)
    {
        Action act = () => WebScrapingUtilities.NormalizeUrl(input);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SanitizeHtml_RemovesScripts()
    {
        const string html = "<div>Safe</div><script>alert('xss');</script>";

        var sanitized = WebScrapingUtilities.SanitizeHtml(html);

        sanitized.Should().Contain("Safe");
        sanitized.Should().NotContain("<script");
    }

    [Fact]
    public void SanitizeHtml_RemovesEventHandlers()
    {
        const string html = "<div onclick=\"alert('xss')\">Click me</div>";

        var sanitized = WebScrapingUtilities.SanitizeHtml(html);

        sanitized.Should().Contain("Click me");
        sanitized.Should().NotContain("onclick");
    }

    [Fact]
    public void SanitizeHtml_RemovesMultipleNestedScriptTags()
    {
        const string html = "<div><script>alert(1)</script><p>Safe</p><script>alert(2)</script></div>";

        var sanitized = WebScrapingUtilities.SanitizeHtml(html);

        sanitized.Should().Contain("Safe");
        sanitized.Should().NotContain("<script");
        sanitized.Should().NotContain("alert");
    }

    [Fact]
    public void SanitizeHtml_RemovesOnloadAttribute()
    {
        const string html = "<body onload=\"alert('xss')\"><div>Content</div></body>";

        var sanitized = WebScrapingUtilities.SanitizeHtml(html);

        sanitized.Should().Contain("Content");
        sanitized.Should().NotContain("onload");
    }

    [Fact]
    public void SanitizeHtml_PreservesLegitimateContent()
    {
        const string html = @"<html>
            <head><title>Test Page</title></head>
            <body>
                <h1>Heading</h1>
                <p>Paragraph with <strong>bold</strong> and <em>italic</em></p>
                <ul><li>List item 1</li><li>List item 2</li></ul>
            </body>
        </html>";

        var sanitized = WebScrapingUtilities.SanitizeHtml(html);

        sanitized.Should().Contain("Heading");
        sanitized.Should().Contain("Paragraph");
        sanitized.Should().Contain("bold");
        sanitized.Should().Contain("italic");
        sanitized.Should().Contain("List item");
    }

    [Fact]
    public void SanitizeHtml_RemovesScriptContent_ComplexNesting()
    {
        const string html = "<div><script>var x = 1;</script><p>Content</p><script>alert('xss')</script></div>";

        var sanitized = WebScrapingUtilities.SanitizeHtml(html);

        sanitized.Should().Contain("Content");
        sanitized.Should().NotContain("alert");
        sanitized.Should().NotContain("var x");
    }

    [Fact]
    public void SanitizeHtml_PreservesBasicStructure()
    {
        const string html = "<h1>Title</h1><p>Paragraph</p><ul><li>Item</li></ul>";

        var sanitized = WebScrapingUtilities.SanitizeHtml(html);

        sanitized.Should().Contain("Title");
        sanitized.Should().Contain("Paragraph");
        sanitized.Should().Contain("Item");
    }
}
