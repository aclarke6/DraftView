using System.Text;
using DraftView.Application.Services;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Services;
using Moq;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for RtfImportProvider covering supported metadata and conversion outcomes.
/// Excludes the underlying RTF conversion engine itself.
/// </summary>
public class RtfImportProviderTests
{
    private readonly Mock<IRtfConverter> rtfConverter = new();

    private RtfImportProvider CreateSut() => new(rtfConverter.Object);

    /// <summary>Supported extension should be .rtf.</summary>
    [Fact]
    public void SupportedExtension_IsRtf()
    {
        var sut = CreateSut();

        Assert.Equal(".rtf", sut.SupportedExtension);
    }

    /// <summary>Provider name should identify RTF.</summary>
    [Fact]
    public void ProviderName_IsRtf()
    {
        var sut = CreateSut();

        Assert.Equal("RTF", sut.ProviderName);
    }

    /// <summary>Valid RTF streams should be converted to HTML.</summary>
    [Fact]
    public async Task ConvertToHtmlAsync_WithValidRtfStream_ReturnsHtml()
    {
        rtfConverter
            .Setup(c => c.ConvertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RtfConversionResult { Html = "<p>Hello</p>", Hash = "hash" });

        var sut = CreateSut();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\\rtf1 Hello}"));

        var result = await sut.ConvertToHtmlAsync(stream);

        Assert.Equal("<p>Hello</p>", result);
    }

    /// <summary>Null converter results should be mapped to UnsupportedFileTypeException.</summary>
    [Fact]
    public async Task ConvertToHtmlAsync_WhenConverterReturnsNull_ThrowsUnsupportedFileTypeException()
    {
        rtfConverter
            .Setup(c => c.ConvertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RtfConversionResult?)null);

        var sut = CreateSut();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\\rtf1 Hello}"));

        var ex = await Assert.ThrowsAsync<UnsupportedFileTypeException>(
            () => sut.ConvertToHtmlAsync(stream));

        Assert.Equal(".rtf", ex.Extension);
    }
}
