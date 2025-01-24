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
                        await ProcessClientsAsync(clients);
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

        private async Task ProcessClientsAsync(IEnumerable<Client> clients)
        {
            foreach (var client in clients)
            {
                try
                {
                    _logger.Information($"Processing client {client.ErpId}, with name: {client.Symbol}");

                    var result = await _apiClient.SendClientAsync(client);

                    if (result == 1)
                    {
                        _logger.Information($"Client {client.ErpId}, with Name: {client.Symbol} processed and sent to API successfully.");
                    }
                    else
                    {
                        _logger.Warning($"Failed to send client {client.ErpId}, with Name: {client.Symbol} to API.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error processing client {client.ErpId}, with Name: {client.Symbol}");
                }
            }
        }
    }
}
