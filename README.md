# Multithreaded Telecom Call Processing Pipeline

**Name:** Akshit Khokher
**Group:** IT-2504
**Course:** FP 3222 — Introduction to Functional Programming

A small call data record (CDR) processor: an immutable `CallRecord`, a
pure tariff function, and two ways to total up a batch of calls — a
plain sequential loop and a two-thread parallel pipeline that is
race-free by construction (isolated output arrays, no locks).

## How to build / run / test

From the solution root (the folder with `TelecomCallProcessor.sln`):

```
dotnet build
dotnet run --project src/TelecomCallProcessor
dotnet test
```

`dotnet run` prints a small demo: it builds six sample calls, totals
them sequentially and in parallel, and prints both totals so you can
see they agree.

`dotnet test` runs all three groups of automated tests (tariff
correctness, invalid input / boundaries, and sequential–parallel
agreement across 100 runs on a 1,000-record dataset).

## Project structure

```
TelecomCallProcessor.sln
src/TelecomCallProcessor/
  CallRecord.cs           - immutable input model + validation
  CallPricing.cs          - pure tariff calculation (CalculateCost)
  CallProcessor.cs        - sequential baseline + two-thread parallel pipeline
  Program.cs               - the only file that touches Console (I/O boundary)
tests/TelecomCallProcessor.Tests/
  CallPricingTariffTests.cs          - Group A: tariff correctness
  CallRecordValidationTests.cs       - Group B: invalid input & boundaries
  SequentialParallelAgreementTests.cs- Group C: sequential vs parallel agreement
```

## Architecture / pure vs. impure components

| Component | Pure or impure | Why |
|---|---|---|
| `CallRecord` constructor | impure in a narrow sense (throws) | Validates and either returns a valid immutable value or throws. No I/O, no mutation after construction. |
| `CallPricing.CalculateCost` | **pure** | Same input always produces the same output. No `Console`, no fields, no shared state. Just one switch expression. |
| `CallProcessor.ProcessCallsSequential` | pure core, called from a plain loop | Only calls the pure `CalculateCost` in a loop and accumulates a local `decimal total`. |
| `CallProcessor.ProcessCallsParallel` | **impure** (by necessity) | Creates two `Thread` objects and writes into two output arrays. This is the only place in the project with any concurrency-related side effects. |
| `Program.Main` | **impure** (I/O boundary) | The only method in the whole solution that calls `Console.WriteLine`. Everything else is reachable and testable without a console. |

The rule followed throughout: **pricing logic never touches the
console or global state, and console I/O never does pricing math.**
`Program.cs` only calls into `CallProcessor`, which only calls into
`CallPricing` — the dependency only ever points one way.

## Task 1 note — why `readonly` doesn't mean "always valid"

`CallRecord` is declared with plain get-only properties and an
explicit constructor, not positional-record syntax, specifically so
validation can run *before* anything is assigned — positional syntax
alone would just assign whatever was passed in, without guarding it.

But `readonly` only promises that a `CallRecord`, once it exists,
cannot be mutated. It says nothing about whether it was ever
constructed validly in the first place. Because `CallRecord` is a
struct, `default(CallRecord)` is always a legal expression that skips
the constructor entirely and zero-fills every field — `RecordId` and
`DestinationCountry` end up `null`, `DurationMinutes` is `0.0`. That
value is perfectly "readonly" (immutable) and completely invalid at
the same time. That's why `CallPricing.CalculateCost` re-checks
validity itself (`IsNullOrWhiteSpace`, `NaN`, range) instead of
trusting that receiving a `CallRecord` means it's safe to price.

## Task 3 — explaining the race in `ProcessCalls`

```csharp
static int globalCallCounter = 0;
static void ProcessCalls(CallRecord[] records)
{
    foreach (var record in records)
    {
        _ = CallPricing.CalculateCost(in record);
        globalCallCounter++;
    }
}
```

`globalCallCounter++` looks like one operation but is really three:
**read** the current value, **add** 1, **write** the new value back.
If two threads both call `ProcessCalls` on this same counter, this
interleaving can happen:

1. Thread A reads `globalCallCounter` → gets `41`.
2. Thread B reads `globalCallCounter` → also gets `41` (A hasn't written yet).
3. Thread A computes `41 + 1 = 42` and writes `42`.
4. Thread B computes `41 + 1 = 42` (using its stale read) and writes `42`.

Two increments happened, but the counter only went up by one — a
**lost update**. The final count under-reports how many calls were
actually processed, and which update "wins" depends on thread timing,
so the bug doesn't show up every run. A single-threaded sequential
loop never has this problem, because there's only ever one reader/writer
at a time — the race only exists once a second thread can interleave
with the first.

This is exactly why `ProcessCallsParallel` doesn't use any shared
counter or accumulator at all — see the next section.

## Task 4 — how the parallel pipeline avoids that race

`ProcessCallsParallel` sidesteps the whole class of bug above instead
of patching it with `lock`/`Interlocked`:

1. The input array is split into two **copies** with array range
   syntax, `records[..mid]` and `records[mid..]` — not shared views,
   so neither worker can see or touch the other's half or the
   original array.
2. **Two independent `decimal[]` output arrays** are allocated before
   either thread starts. Each worker writes only into its own array,
   at its own indices — there is no index or array that both threads
   ever write to, so there's nothing to race on.
3. Exactly **two `Thread` objects** are created and started with
   `Thread.Start()`. Each worker wraps its loop in a `try/catch` and
   stores any exception in its own local variable (`firstError` /
   `secondError`) instead of letting it crash the thread silently.
4. The calling thread calls `Join()` on **both** threads before
   touching either output array — `Join()` blocks until that thread
   has completely finished, so by the time execution reaches the
   aggregation step, both arrays are fully written and no worker is
   still running.
5. If either worker recorded an exception, it's wrapped in an
   `AggregateException` and thrown on the calling thread — the method
   returns *either* a fully-aggregated correct total *or* throws,
   never a partial sum from only one half.
6. The final result is just `sum(firstResults) + sum(secondResults)`,
   computed entirely on the calling thread after both workers are done.

No `lock`, `Interlocked`, `Task`, or PLINQ anywhere — isolation
(separate data, separate output) replaces synchronization.

## Task 5 — sequential baseline & a note on performance

`ProcessCallsSequential` calls the exact same `CallPricing.CalculateCost`
as the parallel version, just from a single ordinary loop with no
threads at all. It exists as the correctness baseline that the tests
in Group C compare the parallel result against.

**On performance:** this project does *not* claim that
`ProcessCallsParallel` is faster than `ProcessCallsSequential`. For
small arrays, the overhead of creating two OS threads can easily cost
more time than it saves — raw `Thread` objects are a relatively heavy
way to get parallelism. The point of this assignment is correctness
and race-freedom, not raw speed; any real speed-up would only tend to
show up on much larger inputs, and even then dedicating only two fixed
threads regardless of core count is not a scalable design (see
Limitations below).

## Test results

All three groups pass under `dotnet test`:

- **Group A — Tariff correctness:** every example from the
  assignment's pricing table, plus explicit zero-minute checks for
  both the roaming-KZ flat fee and the non-roaming case.
- **Group B — Invalid input & boundaries:** blank `RecordId`/country,
  negative/NaN/infinite/over-10,000 duration, `default(CallRecord)`
  rejected by `CalculateCost`, the exact `10,000` boundary allowed,
  and two tariff boundaries — `0.999.../1.0` (roaming KZ flat-fee
  cutoff) and `9.999.../10.0` (premium roaming cutoff).
- **Group C — Sequential/parallel agreement:** a fixed 1,000-record
  dataset (cycling through 5 countries, three roaming patterns, and
  minute counts `0..19` so every tariff arm is hit many times) is
  totaled sequentially once, then the parallel pipeline is run **100
  times** on the same input and every run's total is asserted equal
  to the sequential total. The input array is also asserted unchanged
  after all of that. Empty-array and odd-length-array edge cases are
  tested for both the sequential and parallel methods.

As noted in the assignment, 100 passing runs is evidence the pipeline
behaves correctly on this machine and this workload — it is not a
formal proof that no interleaving could ever cause a problem.

## Limitations

- Always exactly **two** threads, regardless of how many CPU cores
  are available or how large the input is — this is intentional per
  the assignment, but it isn't how you'd write this for a real,
  arbitrarily-sized workload (you'd want a thread/task pool sized to
  the hardware instead of a hardcoded split in two).
- `ProcessCallsParallel` requires an **even-length** array so it can
  split into two *equal* halves — an odd-length array is rejected
  rather than handled with an uneven split.
- `Remote`/unknown country codes beyond what's demonstrated here all
  fall into the same 45.00/min fallback rate — there's no third
  "unknown code" tariff tier, by design (matches the assignment's
  table, which only defines 4 explicit tiers plus one fallback).

## Live-change readiness

The instructor may ask for a live change such as swapping the
fallback rate from `45.00` to `55.00`. That's a one-line edit in
`CallPricing.CalculateCost` (the last `_ => 45.00m * ...` arm) plus
updating the affected expected values in `CallPricingTariffTests`
(the DE, XX, and KZ-roaming-1-min rows) and the boundary test in
`CallRecordValidationTests` that uses the fallback rate near the
9.999/10.0 boundary.

## Oral defense — quick prep notes

**Pure functions / immutability**
- `CalculateCost` is pure: no `Console`, no fields, no mutation —
  given the same `CallRecord`, it always returns the same decimal.
- `readonly record struct` guarantees the *value* can't be mutated
  after construction and gives structural equality for free. It does
  **not** guarantee every instance went through the constructor —
  `default(CallRecord)` proves that, which is why `CalculateCost`
  still validates defensively instead of trusting the type alone.

**Pattern matching / validation**
- Walk through `0.999` vs `1.0` minutes for a roaming KZ call: at
  `0.999` the `when record.DurationMinutes < 1.0` guard is true, so
  the flat `50.00` arm matches; at exactly `1.0` that guard is false,
  so it falls through to the `45.00`/min fallback arm instead.
- The NaN arm is separate from the general range-check arm because
  `NaN` compares `false` to *every* ordinary comparison, including
  `NaN < 0` and `NaN > 10000` — a plain range check would silently
  let `NaN` slip through, so it needs its own explicit
  `double.IsNaN(...)` guard ahead of the range check.
- Duration is only cast `double → decimal` in arms reached *after*
  all the validation arms — by that point it's already confirmed
  finite and in range, so the conversion can't silently produce a
  nonsensical decimal from a NaN/infinite double.

**Threads / testing**
- The two output arrays are safe because each thread only ever
  indexes into *its own* array — there is no shared index or shared
  array either thread writes to, so there's no race to synchronize
  away in the first place.
- `Join()` guarantees the calling thread blocks until that worker
  thread has fully finished running (including its `try/catch`) —
  so by the time both `Join()` calls return, both output arrays are
  completely and safely written, with no thread still in flight.
- Sequential and parallel totals are compared directly in Group C's
  tests (100 parallel runs, each asserted equal to one sequential
  run) as the correctness check.

## AI assistance disclosure

This project (code, tests, and README) was generated with AI
assistance (Claude) based on the assignment brief, then reviewed
against every requirement in the brief by hand. I did not have access
to a .NET compiler in the environment where this was generated, so it
has **not been compiled or executed** before being handed to you — you
must run `dotnet build` and `dotnet test` yourself and fix anything
that doesn't compile before the defense. I am expected to explain any
part of this code during the oral defense, so I've read through it
and the notes above before submitting.
