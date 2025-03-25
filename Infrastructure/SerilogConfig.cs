using Serilog;
using Serilog.Core;
using System;

namespace PinquarkWMSSynchro.Infrastructure
{
    public static class SerilogConfig
    {
        public static Logger ConfigureLogger(int retainLogDays)
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            return new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    $@"{path}\log-.txt",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: retainLogDays
                )
                .CreateLogger();
        }
    }
}
