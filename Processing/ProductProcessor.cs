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
using System.Xml.Linq;

namespace PinquarkWMSSynchro.Processing
{
    public class ProductProcessor
    {
        private readonly DatabaseRepository _database;
        private readonly RestApiClient _apiClient;
        private readonly ILogger _logger;
        private readonly int _fetchInterval;

        public ProductProcessor(DatabaseRepository database, RestApiClient apiClient, ILogger logger)
        {
            _database = database;
            _apiClient = apiClient;
            _logger = logger;
            _fetchInterval = Convert.ToInt32(ConfigurationManager.AppSettings["Co ile minut pobierac towary"]);
        }
        public async Task StartProcessingAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var products = await _database.GetProductsAsync();

                    if (products != null && products?.Count > 0)
                    {
                        _logger.Information($"Fetched {products.Count} products from database.");
                        var result = await _apiClient.SendProductAsync(products);

                        if (result == 1)
                        {
                            _logger.Information($"Products processed and sent to API successfully.");
                        }
                        else
                        {
                            _logger.Warning($"Failed to send {products.Count} products to API.");
                        }
                    }
                    else
                    {
                        _logger.Information("No products found to process.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error while fetching products from database.");
                }

                await Task.Delay(TimeSpan.FromMinutes(_fetchInterval), cancellationToken);
            }
        }
    }
}
