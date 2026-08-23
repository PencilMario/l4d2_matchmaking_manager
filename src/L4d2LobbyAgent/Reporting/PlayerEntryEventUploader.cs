using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using L4d2Matchmaking.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace L4d2LobbyAgent.Reporting;

public sealed class PlayerEntryEventUploader(
    PlayerEntryEventQueue queue,
    HttpClient httpClient,
    PlayerEntryReportingOptions options,
    ILogger<PlayerEntryEventUploader> logger) : BackgroundService
{
    private const string EventPath = "/v1/internal/player-entry-events";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<PlayerEntryEvent> batch;
            try
            {
                batch = await queue.ReadBatchAsync(options.BatchSize, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            if (batch.Count == 0)
                return;

            await UploadBatchAsync(batch, stoppingToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> UploadBatchAsync(
        IReadOnlyList<PlayerEntryEvent> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return true;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(options.RequestTimeout);
                using var request = new HttpRequestMessage(HttpMethod.Post, EventPath)
                {
                    Content = JsonContent.Create(new PlayerEntryEventBatchRequest(batch)),
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
                using var response = await httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return true;
                if ((int)response.StatusCode < (int)HttpStatusCode.InternalServerError)
                    return false;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (OperationCanceledException)
            {
                // The per-request timeout is retryable; shutdown cancellation is handled above.
            }
            catch (HttpRequestException)
            {
                // A single immediate retry is part of the reporting failure boundary.
            }

            if (attempt == 0)
                continue;
        }

        logger.LogWarning("player_entry_event_batch_discarded");
        return false;
    }
}
