﻿using System;
using System.Configuration;
using System.ServiceProcess;
using System.Threading.Tasks;
using Serilog;
using PinquarkWMSSynchro.Infrastructure;
using PinquarkWMSSynchro.Processing;
using System.Threading;

namespace PinquarkWMSSynchro
{
    public partial class Service : ServiceBase
    {
        private readonly string _sqlConnectionString = ConfigurationManager.ConnectionStrings["GaskaConnectionString"].ConnectionString;
        private readonly int _retainLogDays = Convert.ToInt32(ConfigurationManager.AppSettings["Po ilu dniach usuwac logi"]);
        private readonly int _batchSizePerRequest = Convert.ToInt32(ConfigurationManager.AppSettings["Ile obiektów wysyłac per request"]);
        private static ILogger _logger;

        private readonly DatabaseRepository _database;
        private readonly XlApiService _xlApiService;
        private readonly RestApiClient _restApiClient;
        private readonly DocumentProcessor _documentProcessor;
        private readonly ProductProcessor _productProcessor;
        private readonly ClientProcessor _clientProcessor;
        private readonly FeedbackProcessor _feedbackProcessor;
        private readonly LogCleanupProcessor _logCleanupProcessor;
        private CancellationTokenSource _cancellationTokenSource;

        public Service()
        {
            InitializeComponent();
            _logger = SerilogConfig.ConfigureLogger(_retainLogDays);

            try
            {
                _xlApiService = new XlApiService();
                _database = new DatabaseRepository(_sqlConnectionString, _xlApiService, _logger);
                _restApiClient = new RestApiClient(_database, _logger);
                _documentProcessor = new DocumentProcessor(_database, _restApiClient, _logger, _batchSizePerRequest);
                _productProcessor = new ProductProcessor(_database, _restApiClient, _logger, _batchSizePerRequest);
                _clientProcessor = new ClientProcessor(_database, _restApiClient, _logger, _batchSizePerRequest);
                _feedbackProcessor = new FeedbackProcessor(_restApiClient, _logger);
                _logCleanupProcessor = new LogCleanupProcessor(_retainLogDays, _logger);

                _logger.Information("DatabaseRepository, ApiClient and Processors initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "Failed to initialize DatabaseRepository or processors");
                throw;
            }
            _logger.Information("Service initialized.");
        }

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);
            _logger.Information("Service Starting");

            _cancellationTokenSource = new CancellationTokenSource();

            int loginResult = _xlApiService.Login();
            if (loginResult != 0)
            {
                _logger.Error($"Can't login to XLApi. ErrorCode: {loginResult}");
                Stop();
                return;
            }
            _logger.Information("Logged in to XLAPI");

            _ = Task.Run(() => _clientProcessor.StartProcessingAsync(_cancellationTokenSource.Token));
            _logger.Information("Client processing started.");

            _ = Task.Run(() => _productProcessor.StartProcessingAsync(_cancellationTokenSource.Token));
            _logger.Information("Product processing started.");

            _ = Task.Run(() => _documentProcessor.StartProcessingAsync(_cancellationTokenSource.Token));
            _logger.Information("Document processing started.");

            _ = Task.Run(() => _feedbackProcessor.StartProcessingAsync(_cancellationTokenSource.Token));
            _logger.Information("Feedback processing started.");

            _ = Task.Run(() => _logCleanupProcessor.StartProcessingAsync(_cancellationTokenSource.Token));
            _logger.Information("Log cleanup process started.");


        }

        protected override void OnStop()
        {
            base.OnStop();
            _cancellationTokenSource.Cancel();

            int logoutResult = _xlApiService.Logout();
            if (logoutResult != 0)
            {
                _logger.Error($"Can't logout from XLApi. ErrorCode: {logoutResult}");
            }
            _logger.Information("Service Stopped");
        }
    }
}
