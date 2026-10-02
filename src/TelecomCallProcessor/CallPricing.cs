using System;

namespace TelecomCallProcessor;

/// <summary>
/// Pure pricing logic. Nothing in this class touches the console,
/// touches a global/static variable, or mutates anything - every
/// method just takes values in and returns a value out, so the same
/// input always produces the same output (a pure function).
/// </summary>
public static class CallPricing
{
    public static decimal CalculateCost(in CallRecord record)
    {
        // One switch expression chooses the tariff. The first three
        // arms are defensive validation (identity/country, NaN, and
        // out-of-range duration are each called out separately so the
        // NaN case is explicit, like the assignment asks for) and
        // only after all of those pass does any arm convert
        // DurationMinutes from double to decimal.
        decimal rawCost = record switch
        {
            _ when string.IsNullOrWhiteSpace(record.RecordId) || string.IsNullOrWhiteSpace(record.DestinationCountry) =>
                throw new ArgumentException(
                    "CallRecord is invalid: RecordId or DestinationCountry is missing. " +
                    "(This also catches default(CallRecord), which has both set to null.)"),

            // defensive NaN check, kept separate from the range check below
            // on purpose, since NaN compares false against every ordinary
            // comparison (NaN < 0 is false, NaN > 10000 is false) and would
            // silently slip through a plain range check otherwise.
            _ when double.IsNaN(record.DurationMinutes) =>
                throw new ArgumentException("CallRecord is invalid: DurationMinutes is NaN."),

            _ when double.IsInfinity(record.DurationMinutes) || record.DurationMinutes < 0 || record.DurationMinutes > 10_000 =>
                throw new ArgumentException("CallRecord is invalid: DurationMinutes must be between 0 and 10,000 and finite."),

            // from here on DurationMinutes is guaranteed finite and in [0, 10000]

            { IsRoaming: true, DestinationCountry: "KZ" } when record.DurationMinutes < 1.0 =>
                50.00m, // flat fee, no per-minute conversion needed

            { IsRoaming: false, DestinationCountry: "KZ" } =>
                15.00m * (decimal)record.DurationMinutes,

            { IsRoaming: true } when record.DurationMinutes >= 10.0 =>
                120.00m * (decimal)record.DurationMinutes,

            // fallback: every other valid combination
            _ =>
                45.00m * (decimal)record.DurationMinutes,
        };

        return Math.Round(rawCost, 2, MidpointRounding.AwayFromZero);
    }
}
