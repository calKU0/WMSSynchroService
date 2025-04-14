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
    public class ProductProcessor
    {
        private readonly DatabaseRepository _database;
        private readonly RestApiClient _apiClient;
        private readonly ILogger _logger;
        private readonly int _fetchInterval;
        private readonly int _batchSizePerRequest;

        public ProductProcessor(DatabaseRepository database, RestApiClient apiClient, ILogger logger, int batchSizePerRequest)
        {
            _database = database;
            _apiClient = apiClient;
            _logger = logger;
            _fetchInterval = Convert.ToInt32(ConfigurationManager.AppSettings["Co ile minut pobierac towary"]);
            _batchSizePerRequest = batchSizePerRequest;
        }

        public async Task StartProcessingAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var products = await _database.GetProductsAsync();

                    if (products?.Count > 0)
                    {
                        _logger.Information($"Fetched {products.Count} products from database.");

                        var batches = await SplitIntoBatchesAsync(products, _batchSizePerRequest);
                        foreach (var batch in batches)
                        {
                            await ProcessProductBatchAsync(batch);
                        }
                    }
                    else
                    {
                        _logger.Information("No products found to process.");
                    }
                }
                catch (ProcessingException)
                {
                    // It is already logged so I dont need to log it again
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error while fetching or processing products.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(_fetchInterval), cancellationToken);
                }
                catch (TaskCanceledException) { }
            }
        }

        private async Task ProcessProductBatchAsync(List<Product> batch)
        {
            try
            {
                var result = await _apiClient.SendProductsAsync(batch);

                if (result == 1)
                {
                    _logger.Information($"Batch of {batch.Count} products processed and sent to API successfully.");
                }
                else
                {
                    _logger.Warning($"Failed to send batch of {batch.Count} products to API.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error processing batch of {batch.Count} products.");
            }
        }

        private async Task<List<List<Product>>> SplitIntoBatchesAsync(List<Product> products, int batchSize)
        {
            return await Task.Run(() =>
                products
                    .Select((product, index) => new { product, index })
                    .GroupBy(x => x.index / batchSize)
                    .Select(g => g.Select(x => x.product).ToList())
                    .ToList()
            );
        }
    }
}
