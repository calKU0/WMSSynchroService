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

                    if (products != null)
                    {
                        _logger.Information($"Fetched {products.Count} products from database.");
                        await ProcessProductsAsync(products);
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
        private async Task ProcessProductsAsync(IEnumerable<Product> products)
        {
            foreach (var product in products)
            {
                try
                {
                    _logger.Information($"Processing product {product.Symbol} ({product.ErpId})");

                    var result = await _apiClient.SendProductAsync(product);

                    if (result == 1)
                    {
                        _logger.Information($"Product {product.Symbol} ({product.ErpId}) processed and sent to API successfully.");
                    }
                    else
                    {
                        _logger.Warning($"Failed to send product {product.Symbol} ({product.ErpId}) to API.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error processing product {product.Symbol} ({product.ErpId})");
                }
            }
        }
    }
}
