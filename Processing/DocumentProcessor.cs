using PinquarkWMSSynchro.Infrastructure;
using PinquarkWMSSynchro.Models;
using PinquarkWMSSynchro;
using Serilog;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Configuration;

namespace PinquarkWMSSynchro.Processing
{
    public class DocumentProcessor
    {
        private readonly DatabaseRepository _database;
        private readonly RestApiClient _apiClient;
        private readonly ILogger _logger;
        private readonly int _fetchInterval;

        public DocumentProcessor(DatabaseRepository databaseRepository, RestApiClient apiClient, ILogger logger)
        {
            _database = databaseRepository;
            _apiClient = apiClient;
            _logger = logger;
            _fetchInterval = Convert.ToInt32(ConfigurationManager.AppSettings["Co ile minut pobierac dokumenty"]);
        }

        public async Task StartProcessingAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var documents = await _database.GetDocumentsAsync();

                    if (documents != null)
                    {
                        _logger.Information($"Fetched {documents.Count} documents from database.");
                        await ProcessDocumentsAsync(documents);
                    }
                    else
                    {
                        _logger.Information("No documents found to process.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error while fetching documents from database.");
                }

                await Task.Delay(TimeSpan.FromMinutes(_fetchInterval), cancellationToken);
            }
        }

        private async Task ProcessDocumentsAsync(IEnumerable<Document> documents)
        {
            foreach (var document in documents)
            {
                try
                {
                    _logger.Information($"Processing document {document.ErpCode} ({document.ErpId})");

                    var result = await _apiClient.SendDocumentAsync(document);

                    if (result == 1)
                    {
                        _logger.Information($"Document {document.ErpCode} ({document.ErpId}) processed and sent to API successfully.");
                    }
                    else
                    {
                        _logger.Warning($"Failed to send document {document.ErpCode} ({document.ErpId}) to API.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error processing document {document.ErpCode} ({document.ErpId})");
                }
            }
        }
    }
}