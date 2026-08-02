using System.Reflection;
using System.Text.Json;
using Inbox2Project.Services;
using Xunit;

namespace Inbox2Project.Tests;

public sealed class OpenAiBillingServiceTests
{
    [Theory]
    [InlineData("0.18", "0.18")]
    [InlineData("\"0.18\"", "0.18")]
    public void ReadUsdSpend_AcceptsNumericAndStringAmounts(string jsonValue, string expected)
    {
        using var document = JsonDocument.Parse(
            $$"""
            {
              "data": [
                {
                  "results": [
                    {
                      "amount": {
                        "currency": "usd",
                        "value": {{jsonValue}}
                      }
                    }
                  ]
                }
              ]
            }
            """);

        var method = typeof(OpenAiBillingService).GetMethod(
            "ReadUsdSpend",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var result = Assert.IsType<decimal>(method.Invoke(null, [document.RootElement]));
        Assert.Equal(decimal.Parse(expected), result);
    }
}
