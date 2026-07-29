using Inbox2Project.Services;
using Xunit;

namespace Inbox2Project.Tests;

public sealed class FileNameBaseNameNormalizerTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 7, 29, 10, 30, 0, TimeSpan.Zero);
    private readonly PathSafetyService _pathSafetyService = new();

    [Theory]
    [InlineData("Marcelo__Sepulveda", "Marcelo_Sepulveda")]
    [InlineData("Marcelo___Sepulveda", "Marcelo_Sepulveda")]
    [InlineData("Marcelo _ _ Sepulveda", "Marcelo_Sepulveda")]
    [InlineData("Marcelo - - Sepulveda", "Marcelo-Sepulveda")]
    public void SanitizeName_CollapsesRepeatedSeparators(string input, string expected)
    {
        var result = _pathSafetyService.SanitizeName(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("20260729_Marcelo_Sepulveda", "Marcelo_Sepulveda")]
    [InlineData("Marcelo_Sepulveda_20260729", "Marcelo_Sepulveda")]
    [InlineData("2026-07-29 Marcelo Sepulveda", "Marcelo Sepulveda")]
    [InlineData("Marcelo Sepulveda 2026-07-29", "Marcelo Sepulveda")]
    [InlineData("Salah Monzoor_Li610_DBC_Generation_Proposal_Review.docx", "Salah Monzoor_Li610_DBC_Generation_Proposal_Review")]
    [InlineData("Proposal Review.pdf.txt", "Proposal Review")]
    public void NormalizeEditableBaseName_RemovesDuplicateReceivedDate(string input, string expected)
    {
        var result = FileNameBaseNameNormalizer.NormalizeEditableBaseName(
            input,
            "fallback",
            ReceivedAt,
            _pathSafetyService);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Marcelo__Software__Revisions", "Marcelo_Software_Revisions")]
    [InlineData("Marcelo_-_Software_Revisions", "Marcelo_-_Software_Revisions")]
    [InlineData("Marcelo _Software", "Marcelo_Software")]
    [InlineData("Q3 Review v2.0", "Q3 Review v2.0")]
    public void NormalizeEditableBaseName_PreservesSingleSeparators(string input, string expected)
    {
        var result = FileNameBaseNameNormalizer.NormalizeEditableBaseName(
            input,
            "fallback",
            ReceivedAt,
            _pathSafetyService);

        Assert.Equal(expected, result);
    }
}
