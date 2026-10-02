using System;

namespace TelecomCallProcessor;

/// <summary>
/// Immutable call data record (CDR). Deliberately NOT declared with
/// positional-record syntax, because positional syntax alone (the
/// "primary constructor") does not let us run validation before the
/// properties are set - it would just assign whatever was passed in.
/// Instead the properties are plain get-only auto-properties and the
/// only way to set them is through the explicit constructor below,
/// which validates every value first and throws on anything invalid.
///
/// IMPORTANT: "readonly" only means a CallRecord cannot be *mutated*
/// after it exists - it says nothing about whether it started out
/// valid. default(CallRecord) is a struct, and structs always have a
/// zero-initialized default value (RecordId = null, DestinationCountry
/// = null, DurationMinutes = 0.0, IsRoaming = false) that completely
/// bypasses this constructor. So "readonly" guarantees immutability,
/// not validity - CallPricing.CalculateCost cannot assume that just
/// because it received a CallRecord, that record is safe to price.
/// </summary>
public readonly record struct CallRecord
{
    public string RecordId { get; }
    public string DestinationCountry { get; }
    public double DurationMinutes { get; }
    public bool IsRoaming { get; }

    public CallRecord(string recordId, string destinationCountry, double durationMinutes, bool isRoaming)
    {
        if (string.IsNullOrWhiteSpace(recordId))
        {
            throw new ArgumentException("RecordId cannot be null, empty, or whitespace.", nameof(recordId));
        }

        if (string.IsNullOrWhiteSpace(destinationCountry))
        {
            throw new ArgumentException("DestinationCountry cannot be null, empty, or whitespace.", nameof(destinationCountry));
        }

        if (double.IsNaN(durationMinutes))
        {
            throw new ArgumentException("DurationMinutes cannot be NaN.", nameof(durationMinutes));
        }

        if (double.IsInfinity(durationMinutes))
        {
            throw new ArgumentException("DurationMinutes cannot be infinite.", nameof(durationMinutes));
        }

        if (durationMinutes < 0)
        {
            throw new ArgumentException("DurationMinutes cannot be negative.", nameof(durationMinutes));
        }

        if (durationMinutes > 10_000)
        {
            throw new ArgumentException("DurationMinutes cannot be greater than 10,000 minutes.", nameof(durationMinutes));
        }

        RecordId = recordId;
        DestinationCountry = destinationCountry;
        DurationMinutes = durationMinutes;
        IsRoaming = isRoaming;
    }

    /// <summary>
    /// True only for a record that went through the constructor above
    /// with valid values. default(CallRecord) returns false here,
    /// which is exactly what lets CallPricing defend against it.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(RecordId) &&
        !string.IsNullOrWhiteSpace(DestinationCountry) &&
        !double.IsNaN(DurationMinutes) &&
        !double.IsInfinity(DurationMinutes) &&
        DurationMinutes >= 0 &&
        DurationMinutes <= 10_000;
}
