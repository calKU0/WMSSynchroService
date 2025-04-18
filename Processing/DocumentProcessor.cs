﻿using PinquarkWMSSynchro.Infrastructure;
using PinquarkWMSSynchro.Models;
using PinquarkWMSSynchro;
using Serilog;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Configuration;
using System.Linq;

namespace PinquarkWMSSynchro.Processing
{
    public class DocumentProcessor
    {
        private readonly DatabaseRepository _database;
        private readonly RestApiClient _apiClient;
        private readonly ILogger _logger;
        private readonly int _fetchInterval;
        private readonly int _batchSizePerRequest;

        public DocumentProcessor(DatabaseRepository database, RestApiClient apiClient, ILogger logger, int batchSizePerRequest)
        {
            _database = database;
            _apiClient = apiClient;
            _logger = logger;
            _fetchInterval = Convert.ToInt32(ConfigurationManager.AppSettings["Co ile minut pobierac dokumenty"]);
            _batchSizePerRequest = batchSizePerRequest;
        }

        public async Task StartProcessingAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var documents = await _database.GetDocumentsAsync();

                    if (documents != null && documents?.Count > 0)
                    {
                        _logger.Information($"Fetched {documents.Count} documents from database.");

                        var tasks = documents.Select(ProcessDocumentAsync);
                        await Task.WhenAll(tasks);
                    }
                    else
                    {
                        _logger.Information("No documents found to process.");
                    }
                }
                catch (ProcessingException)
                {
                    // It is already logged so I dont need to log it again
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error while fetching or processing documents.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(_fetchInterval), cancellationToken);
                }
                catch (TaskCanceledException) { }
            }
        }

        private async Task ProcessDocumentAsync(Document document)
        {
            try
            {
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