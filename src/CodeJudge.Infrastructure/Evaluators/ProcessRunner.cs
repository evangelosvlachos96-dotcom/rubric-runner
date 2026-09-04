using System.Diagnostics;

namespace CodeJudge.Infrastructure.Evaluators;

/// <summary>Outcome of running an external process.</summary>
public sealed record ProcessRunResult(int ExitCode, string Stdout, string Stderr, bool TimedOut, int DurationMs);

/// <summary>
/// Starts a process, optionally feeds stdin, captures stdout/stderr, and enforces a hard timeout by
/// killing the whole process tree (System Design §4.2). Never lets untrusted code hang the worker.
/// </summary>
public sealed class ProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string? stdin,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo =
            {
                FileName = fileName,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        var stopwatch = Stopwatch.StartNew();
        process.Start();

        // Read both streams concurrently to avoid pipe-buffer deadlocks.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        if (stdin is not null)
        {
            await process.StandardInput.WriteAsync(stdin.AsMemory(), cancellationToken);
        }

        process.StandardInput.Close();

        var timedOut = false;
        using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeoutCts.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                timedOut = true;
                TryKill(process);
                await process.WaitForExitAsync(cancellationToken);
            }
        }

        stopwatch.Stop();

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new ProcessRunResult(
            timedOut ? -1 : process.ExitCode,
            stdout,
            stderr,
            timedOut,
            (int)stopwatch.ElapsedMilliseconds);
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Process already exited between the timeout and the kill.
        }
    }
}
