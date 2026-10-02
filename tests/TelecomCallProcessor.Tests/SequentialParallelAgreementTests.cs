using System;
using TelecomCallProcessor;
using Xunit;

namespace TelecomCallProcessor.Tests;

/// <summary>
/// Group C - Sequential/parallel agreement, plus the empty and
/// odd-length edge cases for the parallel pipeline.
/// </summary>
public class SequentialParallelAgreementTests
{
    /// <summary>
    /// Deterministic 1,000-record dataset that deliberately cycles
    /// through several countries, roaming values and minute counts so
    /// it exercises every tariff arm (flat fee, KZ per-minute, premium
    /// roaming, and the fallback rate) many times over.
    /// </summary>
    private static CallRecord[] BuildLargeDataset(int count)
    {
        string[] countries = { "KZ", "US", "DE", "FR", "XX" };
        var records = new CallRecord[count];

        for (int i = 0; i < count; i++)
        {
            string country = countries[i % countries.Length];
            bool roaming = i % 3 == 0;
            double minutes = i % 20; // sweeps 0..19, covering the 1.0 and 10.0 boundaries
            records[i] = new CallRecord($"R{i:D5}", country, minutes, roaming);
        }

        return records;
    }

    [Fact]
    public void SequentialAndParallel_AgreeOnLargeDataset_Across100Runs()
    {
        CallRecord[] records = BuildLargeDataset(1000);
        CallRecord[] snapshot = (CallRecord[])records.Clone();

        decimal sequentialTotal = CallProcessor.ProcessCallsSequential(records);

        for (int run = 0; run < 100; run++)
        {
            decimal parallelTotal = CallProcessor.ProcessCallsParallel(records);
            Assert.Equal(sequentialTotal, parallelTotal);
        }

        // the parallel pipeline only ever reads from "records" (it
        // slices copies with [..mid]/[mid..]), so the original array
        // must be exactly what it was before any of this ran.
        Assert.Equal(snapshot.Length, records.Length);
        for (int i = 0; i < snapshot.Length; i++)
        {
            Assert.Equal(snapshot[i], records[i]);
        }
    }

    [Fact]
    public void ProcessCallsParallel_EmptyArray_ReturnsZero()
    {
        Assert.Equal(0m, CallProcessor.ProcessCallsParallel(Array.Empty<CallRecord>()));
    }

    [Fact]
    public void ProcessCallsSequential_EmptyArray_ReturnsZero()
    {
        Assert.Equal(0m, CallProcessor.ProcessCallsSequential(Array.Empty<CallRecord>()));
    }

    [Fact]
    public void ProcessCallsParallel_OddLengthArray_Throws()
    {
        CallRecord[] records = { new CallRecord("R1", "KZ", 1.0, false) };
        Assert.Throws<ArgumentException>(() => CallProcessor.ProcessCallsParallel(records));
    }

    [Fact]
    public void ProcessCallsParallel_NullArray_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CallProcessor.ProcessCallsParallel(null!));
    }

    [Fact]
    public void ProcessCallsSequential_NullArray_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CallProcessor.ProcessCallsSequential(null!));
    }
}
