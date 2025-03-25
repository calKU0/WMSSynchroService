using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace PinquarkWMSSynchro.Processing
{
    public class LogCleanupProcessor
    {
        private readonly int _retainLogDays;
        private readonly ILogger _logger;

        public LogCleanupProcessor(int retainLogDays, ILogger logger)
        {
            _retainLogDays = retainLogDays;
            _logger = logger;
        }

        public async Task StartProcessingAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await DeleteOldJsonFilesAsync();
                    await Task.Delay(TimeSpan.FromDays(1), cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.Information("Log cleanup process stopped.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during log cleanup process.");
            }
        }

        private async Task DeleteOldJsonFilesAsync()
        {
            try
            {
                string jsonLogsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "json");

                if (Directory.Exists(jsonLogsPath))
                {
                    string[] files = Directory.GetFiles(jsonLogsPath, "*.json", SearchOption.AllDirectories);

                    var deleteTasks = files
                        .Where(file => File.GetLastWriteTime(file) < DateTime.Now.AddDays(-_retainLogDays))
                        .Select(async file =>
                        {
                            try
                            {
                                await Task.Run(() => File.Delete(file));
                            }
                            catch (Exception ex)
                            {
                                _logger.Error(ex, $"Error deleting file {file}");
                            }
                        });

                    await Task.WhenAll(deleteTasks);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during JSON log file cleanup.");
            }
        }
    }
}
