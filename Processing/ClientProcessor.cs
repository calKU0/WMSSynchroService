using PinquarkWMSSynchro.Infrastructure;
using PinquarkWMSSynchro.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PinquarkWMSSynchro.Processing
{
    public class ClientProcessor
    {
        private readonly DatabaseRepository _database;
        private readonly RestApiClient _apiClient;
        private readonly ILogger _logger;
        private readonly int _fetchInterval;
        private readonly int _batchSizePerRequest;

        public ClientProcessor(DatabaseRepository database, RestApiClient apiClient, ILogger logger, int batchSizePerRequest)
        {
            _database = database;
            _apiClient = apiClient;
            _logger = logger;
            _fetchInterval = Convert.ToInt32(ConfigurationManager.AppSettings["Co ile minut pobierac kontrahentow"]);
            _batchSizePerRequest = batchSizePerRequest;
        }

        public async Task StartProcessingAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var clients = await _database.GetClientsAsync();

                    if (clients?.Count > 0)
                    {
                        _logger.Information($"Fetched {clients.Count} clients from database.");

                        var batches = await SplitIntoBatchesAsync(clients, _batchSizePerRequest);
                        foreach (var batch in batches)
                        {
                            await ProcessClientBatchAsync(batch);
                        }
                    }
                    else
                    {
                        _logger.Information("No clients found to process.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error while fetching or processing clients.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(_fetchInterval), cancellationToken);
                }
                catch (TaskCanceledException) { }
            }
        }

        private async Task ProcessClientBatchAsync(List<Client> batch)
        {
            try
            {
                var result = await _apiClient.SendClientsAsync(batch);

                if (result == 1)
                {
                    _logger.Information($"Batch of {batch.Count} clients processed and sent to API successfully.");
                }
                else
                {
                    _logger.Warning($"Failed to send batch of {batch.Count} clients to API.");
                }
            }
            catch (ProcessingException)
            {
                // It is already logged so I dont need to log it again
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error processing batch of {batch.Count} clients.");
            }
        }

        private async Task<List<List<Client>>> SplitIntoBatchesAsync(List<Client> clients, int batchSize)
        {
            return await Task.Run(() =>
                clients
                    .Select((client, index) => new { client, index })
                    .GroupBy(x => x.index / batchSize)
                    .Select(g => g.Select(x => x.client).ToList())
                    .ToList()
            );
        }
    }
}