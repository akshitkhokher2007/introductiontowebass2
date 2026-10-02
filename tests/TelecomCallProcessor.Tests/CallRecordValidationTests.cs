using System;
using TelecomCallProcessor;
using Xunit;

namespace TelecomCallProcessor.Tests;

/// <summary>
/// Group B - Invalid inputs and boundaries.
/// </summary>
public class CallRecordValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_RejectsBlankRecordId(string? blankId)
    {
        Assert.Throws<ArgumentException>(() => new CallRecord(blankId!, "KZ", 1.0, false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_RejectsBlankCountry(string? blankCountry)
    {
        Assert.Throws<ArgumentException>(() => new CallRecord("R1", blankCountry!, 1.0, false));
    }

    [Fact]
    public void Constructor_RejectsNegativeDuration()
    {
        Assert.Throws<ArgumentException>(() => new CallRecord("R1", "KZ", -0.01, false));
    }

    [Fact]
    public void Constructor_RejectsNaNDuration()
    {
        Assert.Throws<ArgumentException>(() => new CallRecord("R1", "KZ", double.NaN, false));
    }

    [Fact]
    public void Constructor_RejectsInfiniteDuration()
    {
        Assert.Throws<ArgumentException>(() => new CallRecord("R1", "KZ", double.PositiveInfinity, false));
        Assert.Throws<ArgumentException>(() => new CallRecord("R1", "KZ", double.NegativeInfinity, false));
    }

    [Fact]
    public void Constructor_RejectsDurationAboveTenThousandMinutes()
    {
        Assert.Throws<ArgumentException>(() => new CallRecord("R1", "KZ", 10_000.01, false));
    }

    [Fact]
    public void Constructor_AllowsExactlyTenThousandMinutes()
    {
        // 10,000 itself is allowed - the rule rejects "greater than" 10,000.
        var record = new CallRecord("R1", "KZ", 10_000.0, false);
        Assert.Equal(10_000.0, record.DurationMinutes);
    }

    [Fact]
    public void CalculateCost_RejectsDefaultCallRecord()
    {
        CallRecord defaultRecord = default;
        Assert.Throws<ArgumentException>(() => CallPricing.CalculateCost(in defaultRecord));
    }

    [Fact]
    public void CalculateCost_ZeroMinuteCall_IsValid()
    {
        var record = new CallRecord("R1", "KZ", 0.0, false);
        decimal cost = CallPricing.CalculateCost(in record);
        Assert.Equal(0.00m, cost);
    }

    // boundary: roaming + KZ, right around the 1.0 minute flat-fee cutoff
    [Fact]
    public void CalculateCost_JustUnderOneMinute_StillUsesFlatFee()
    {
        var record = new CallRecord("R1", "KZ", 0.999, true);
        Assert.Equal(50.00m, CallPricing.CalculateCost(in record));
    }

    [Fact]
    public void CalculateCost_AtExactlyOneMinute_LeavesFlatFeeArm()
    {
        // at 1.0 minutes it's no longer "< 1.0", so roaming+KZ falls
        // through to the 45.00/min fallback instead of the flat fee.
        var record = new CallRecord("R1", "KZ", 1.0, true);
        Assert.Equal(45.00m, CallPricing.CalculateCost(in record));
    }

    // boundary: roaming (non-KZ), right around the 10.0 minute premium cutoff
    [Fact]
    public void CalculateCost_JustUnderTenMinutes_StillUsesFallbackRate()
    {
        var record = new CallRecord("R1", "US", 9.999, true);
        decimal expected = Math.Round(45.00m * 9.999m, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(expected, CallPricing.CalculateCost(in record));
    }

    [Fact]
    public void CalculateCost_AtExactlyTenMinutes_UsesPremiumRoamingRate()
    {
        var record = new CallRecord("R1", "US", 10.0, true);
        Assert.Equal(1200.00m, CallPricing.CalculateCost(in record));
    }
}
