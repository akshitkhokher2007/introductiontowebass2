using TelecomCallProcessor;
using Xunit;

namespace TelecomCallProcessor.Tests;

/// <summary>
/// Group A - Tariff correctness. One row per example from the
/// assignment's pricing table.
/// </summary>
public class CallPricingTariffTests
{
    // InlineData can't take a decimal directly (it's not a valid
    // attribute-argument type in C#), so the expected value is passed
    // as a double and cast to decimal inside the test.
    [Theory]
    [InlineData("KZ", false, 4.0, 60.00)]   // non-roaming KZ -> 15.00/min
    [InlineData("KZ", true, 0.5, 50.00)]    // roaming KZ < 1 min -> flat 50
    [InlineData("US", true, 10.0, 1200.00)] // roaming >= 10 min -> 120.00/min
    [InlineData("DE", false, 3.0, 135.00)]  // non-roaming, non-KZ -> fallback 45.00/min
    [InlineData("XX", false, 2.0, 90.00)]   // non-roaming, unknown code -> fallback 45.00/min
    [InlineData("KZ", true, 1.0, 45.00)]    // roaming KZ, NOT < 1 min -> fallback 45.00/min
    public void CalculateCost_ReturnsExpectedTariff(string country, bool roaming, double minutes, double expected)
    {
        var record = new CallRecord("R-TEST", country, minutes, roaming);

        decimal cost = CallPricing.CalculateCost(in record);

        Assert.Equal((decimal)expected, cost);
    }

    [Fact]
    public void CalculateCost_ZeroMinuteRoamingKzCall_UsesFlatFee()
    {
        var record = new CallRecord("R1", "KZ", 0.0, true);
        Assert.Equal(50.00m, CallPricing.CalculateCost(in record));
    }

    [Fact]
    public void CalculateCost_ZeroMinuteNonRoamingCall_IsZero()
    {
        var kzRecord = new CallRecord("R1", "KZ", 0.0, false);
        Assert.Equal(0.00m, CallPricing.CalculateCost(in kzRecord));

        var otherRecord = new CallRecord("R2", "DE", 0.0, false);
        Assert.Equal(0.00m, CallPricing.CalculateCost(in otherRecord));
    }
}
