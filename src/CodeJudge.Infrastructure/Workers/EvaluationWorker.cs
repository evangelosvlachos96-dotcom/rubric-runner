using CodeJudge.Application.Abstractions;
using CodeJudge.Application.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeJudge.Infrastructure.Workers;

/// <summary>
/// Background evaluation loop (System Design §6.4, Database Design §8): claim a batch, evaluate each
/// submission, mark it Completed or Error, isolate failures per item, and poll when the queue is empty.
/// </summary>
public sealed class EvaluationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EvaluationSettings _settings;
    private readonly ILogger<EvaluationWorker> _logger;

    private readonly string _workerId =
        $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public EvaluationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<EvaluationSettings> settings,
        ILogger<EvaluationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
                if (processed == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_settings.PollingIntervalSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Evaluation poll failed; retrying after the polling interval.");
                await SafeDelayAsync(stoppingToken);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var claimer = scope.ServiceProvider.GetRequiredService<ISubmissionClaimer>();
        var evaluationService = scope.ServiceProvider.GetRequiredService<EvaluationService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var batch = await claimer.ClaimBatchAsync(
            _settings.BatchSize,
            _workerId,
            TimeSpan.FromSeconds(_settings.LockDurationSeconds),
            stoppingToken);

        foreach (var submission in batch)
        {
            using var _ = _logger.BeginScope("SubmissionId:{SubmissionId} WorkerId:{WorkerId}", submission.Id, _workerId);
            try
            {
                if (submission.AttemptCount > _settings.MaxAttempts)
                {
                    submission.Fail("Max attempts exceeded.");
                }
                else
                {
                    var results = await evaluationService.EvaluateAsync(submission, stoppingToken);
                    submission.Complete(results);
                }

                await unitOfWork.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Evaluation failed for submission {SubmissionId}.", submission.Id);
                await TryFailAsync(submission, unitOfWork, ex.Message, stoppingToken);
            }
        }

        return batch.Count;
    }

    private async Task TryFailAsync(
        Domain.Entities.Submission submission,
        IUnitOfWork unitOfWork,
        string message,
        CancellationToken stoppingToken)
    {
        try
        {
            submission.Fail(message);
            await unitOfWork.SaveChangesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not mark submission {SubmissionId} as failed.", submission.Id);
        }
    }

    private async Task SafeDelayAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_settings.PollingIntervalSeconds), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}
