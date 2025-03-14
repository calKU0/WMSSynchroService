﻿using PinquarkWMSSynchro.Infrastructure;
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

                    if (clients?.Count > 0)
                    {
                        _logger.Information($"Fetched {clients.Count} clients from database.");

                        var tasks = clients.Select(ProccessClientAsync);
                        await Task.WhenAll(tasks);
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

        private async Task ProccessClientAsync(Client client)
        {
            try
            {
                var result = await _apiClient.SendClientAsync(client);
                if (result == 1)
                {
                    _logger.Information($"Client {client.Symbol} ({client.ErpId}) processed and send to API successfully.");
                }
                else
                {
                    _logger.Warning($"Failed to send client {client.Name} ({client.ErpId}) to API.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error processing client {client.Symbol} ({client.ErpId}).");
            }
        }
    }
}
