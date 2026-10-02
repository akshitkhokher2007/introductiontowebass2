using System;
using System.Collections.Generic;
using System.Threading;

namespace TelecomCallProcessor;

/// <summary>
/// Sequential baseline and two-thread parallel processing.
///
/// The ONLY impure/mutating ideas in this whole project live in
/// ProcessCallsParallel: creating threads and writing into the two
/// output arrays. CallPricing.CalculateCost itself stays pure - the
/// threads just call it many times and store where the results go.
/// </summary>
public static class CallProcessor
{
    /// <summary>
    /// Correctness baseline. Straightforward loop, one thread (the
    /// caller's own thread), no concurrency at all.
    /// </summary>
    public static decimal ProcessCallsSequential(CallRecord[] records)
    {
        if (records is null)
        {
            throw new ArgumentNullException(nameof(records));
        }

        decimal total = 0m;

        foreach (CallRecord record in records)
        {
            total += CallPricing.CalculateCost(in record);
        }

        return total;
    }

    /// <summary>
    /// Splits the array into two equal halves and processes each half
    /// on its own dedicated Thread, writing into its own dedicated
    /// output array - so there is no shared mutable state between the
    /// two threads and nothing to synchronize.
    /// </summary>
    public static decimal ProcessCallsParallel(CallRecord[] records)
    {
        if (records is null)
        {
            throw new ArgumentNullException(nameof(records));
        }

        if (records.Length == 0)
        {
            return 0m;
        }

        if (records.Length % 2 != 0)
        {
            throw new ArgumentException("records must have an even length so it can be split into two equal halves.", nameof(records));
        }

        int mid = records.Length / 2;

        // array range syntax - these make brand new arrays, they do not
        // alias the original "records" array, so nothing a worker does
        // to its half can ever touch "records" itself.
        CallRecord[] firstHalf = records[..mid];
        CallRecord[] secondHalf = records[mid..];

        // two independent output arrays, allocated before either thread
        // starts, one per worker. Each worker only ever writes to its
        // own array, so there is no shared mutable output to race on.
        decimal[] firstResults = new decimal[firstHalf.Length];
        decimal[] secondResults = new decimal[secondHalf.Length];

        // each worker catches its own exception instead of letting it
        // escape on a background thread (which would crash the process
        // instead of being reported back to the caller).
        Exception? firstError = null;
        Exception? secondError = null;

        var firstWorker = new Thread(() =>
        {
            try
            {
                for (int i = 0; i < firstHalf.Length; i++)
                {
                    firstResults[i] = CallPricing.CalculateCost(in firstHalf[i]);
                }
            }
            catch (Exception ex)
            {
                firstError = ex;
            }
        });

        var secondWorker = new Thread(() =>
        {
            try
            {
                for (int i = 0; i < secondHalf.Length; i++)
                {
                    secondResults[i] = CallPricing.CalculateCost(in secondHalf[i]);
                }
            }
            catch (Exception ex)
            {
                secondError = ex;
            }
        });

        firstWorker.Start();
        secondWorker.Start();

        // Join blocks the calling thread until each worker has fully
        // finished (success or failure) - so nothing below this line
        // can run while a worker is still writing into its array.
        firstWorker.Join();
        secondWorker.Join();

        if (firstError is not null || secondError is not null)
        {
            var innerExceptions = new List<Exception>();
            if (firstError is not null) innerExceptions.Add(firstError);
            if (secondError is not null) innerExceptions.Add(secondError);

            // report the failure on the calling thread instead of ever
            // treating whatever partial results exist as a real answer.
            throw new AggregateException(
                "One or both worker threads failed while processing call records.",
                innerExceptions);
        }

        decimal total = 0m;

        foreach (decimal cost in firstResults)
        {
            total += cost;
        }

        foreach (decimal cost in secondResults)
        {
            total += cost;
        }

        return total;
    }
}
