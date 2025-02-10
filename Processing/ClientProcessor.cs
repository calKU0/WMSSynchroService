using PinquarkWMSSynchro.Infrastructure;
using PinquarkWMSSynchro.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
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

        public ClientProcessor(DatabaseRepository databaseRepository, RestApiClient apiClient, ILogger logger)
        {
            _database = databaseRepository;
            _apiClient = apiClient;
            _logger = logger;
            _fetchInterval = Convert.ToInt32(ConfigurationManager.AppSettings["Co ile minut pobierac kontrahentow"]);
        }

        public async Task StartProcessingAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var clients = await _database.GetClientsAsync();

                    if (clients != null)
                    {
                        _logger.Information($"Fetched {clients.Count} clients from database.");
                        _logger.Information($"Processing {clients.Count} clients");

                        var result = await _apiClient.SendClientAsync(clients);

                        if (result == 1)
                        {
                            _logger.Information($"Clients processed and sent to API successfully.");
                        }
                        else
                        {
                            _logger.Warning($"Failed to send clients to API.");
                        }
                    }
                    else
                    {
                        _logger.Information("No clients found to process.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error while fetching clients from database.");
                }

                await Task.Delay(TimeSpan.FromMinutes(_fetchInterval), cancellationToken);
            }
        }
    }
}
