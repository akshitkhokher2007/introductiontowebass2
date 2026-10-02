using System;

namespace TelecomCallProcessor;

/// <summary>
/// This class is the I/O boundary - it is the only place in the whole
/// project that calls Console.WriteLine. It builds some sample data,
/// hands it to the pure/thread-based processing methods, and prints
/// what comes back. No pricing or threading logic lives here.
/// </summary>
public static class Program
{
    public static void Main()
    {
        CallRecord[] records = BuildSampleRecords();

        decimal sequentialTotal = CallProcessor.ProcessCallsSequential(records);
        decimal parallelTotal = CallProcessor.ProcessCallsParallel(records);

        Console.WriteLine("=== Telecom Call Processing Demo ===");
        Console.WriteLine($"Records processed    : {records.Length}");
        Console.WriteLine($"Sequential total (KZT): {sequentialTotal:F2}");
        Console.WriteLine($"Parallel total (KZT)  : {parallelTotal:F2}");
        Console.WriteLine(sequentialTotal == parallelTotal
            ? "Sequential and parallel totals agree."
            : "WARNING: totals do not agree - something is wrong.");
    }

    private static CallRecord[] BuildSampleRecords() => new[]
    {
        new CallRecord("R001", "KZ", 4.0, false),  // non-roaming KZ    -> 15.00/min  = 60.00
        new CallRecord("R002", "KZ", 0.5, true),   // roaming KZ < 1min -> flat       = 50.00
        new CallRecord("R003", "US", 10.0, true),  // roaming >= 10min  -> 120.00/min = 1200.00
        new CallRecord("R004", "DE", 3.0, false),  // non-roaming non-KZ -> fallback  = 135.00
        new CallRecord("R005", "XX", 2.0, false),  // non-roaming non-KZ -> fallback  = 90.00
        new CallRecord("R006", "KZ", 1.0, true),   // roaming KZ, not < 1min -> fallback = 45.00
    };
}
