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
    public class FeedbackProcessor
    {
        private readonly RestApiClient _apiClient;
        private readonly ILogger _logger;
        private readonly int _fetchInterval;

        public FeedbackProcessor(RestApiClient apiClient, ILogger logger)
        {
            _apiClient = apiClient;
            _logger = logger;
            _fetchInterval = Convert.ToInt32(ConfigurationManager.AppSettings["Co ile minut pobierac logi"]);
        }

        public async Task StartProcessingAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _apiClient.GetFeedbackAsync(true);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error processing feedback");
                }

                await Task.Delay(TimeSpan.FromMinutes(_fetchInterval), cancellationToken);
            }
        }
    }
}
