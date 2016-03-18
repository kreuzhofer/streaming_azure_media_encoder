namespace Agent
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Win32;
    using NLog;
    using NLog.Config;
    using NLog.Targets;
    using Topshelf;

    class Program
    {
        const string ServiceName = "AzureFilesReplicator";

        private static string Get(string key)
        {
            string val = null;

            RegistryKey hklm64 = RegistryKey.OpenBaseKey(
                hKey: RegistryHive.LocalMachine, 
                view: RegistryView.Registry64);
            if (hklm64 != null)
            {
                RegistryKey azureSettings = hklm64.OpenSubKey(
                    name: string.Join("\\", "SOFTWARE", "Azure", "LocalService"), 
                    writable: false);
                if (azureSettings != null)
                {
                    val = azureSettings.GetValue(key) as string;
                    if (val != null)
                    {
                        return val;
                    }
                }
            }

            val = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrEmpty(val as string))
            {
                throw new KeyNotFoundException($"Could not retrieve setting {key}, Was not found in Environment and Registry!");
            }
            return val;
        }

        public static string AzureConnectionString { get { return Get("StorageAccount"); } }

        static LoggingConfiguration CreateLoggingConfiguration()
        {
            var log = new LoggingConfiguration();
            var layout = @"${date:format=HH\:mm\:ss} ${logger} ${message}";

            var targetConsole = new ColoredConsoleTarget { Layout = layout };
            log.AddTarget("console", targetConsole);
            log.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, targetConsole));

            var targetLogfile = new FileTarget
            {
                FileName = "${basedir}/" + ServiceName + "-${machinename}-{#####}.log",
                Layout = layout,
                ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
                ArchiveEvery = FileArchivePeriod.Minute
            };
            log.AddTarget("logfile", targetLogfile);
            log.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, targetLogfile));

            var targetTrace = new TraceTarget();
            log.AddTarget("trace", targetTrace);
            log.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, targetTrace));

            //var azureTarget = new AzureAppendBlobTarget
            //{
            //    ConnectionString = AzureConnectionString,
            //    Layout = layout,
            //    Name = "azure",
            //    BlobName = ServiceName + "-${machinename}.log",
            //    Container = $"logs-{Environment.MachineName.ToLower()}"
            //};
            //log.AddTarget("azure", azureTarget);
            //log.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, azureTarget));

            return log;
        }

        static void Main(string[] args)
        {
            // MirrorCSProgram.MainAsync(args).Wait();

            LogManager.Configuration = CreateLoggingConfiguration();

            HostFactory.Run(x =>
            {
                x.Service<CopyService>(instance => instance
                        .ConstructUsing(() => new CopyService("hallo"))
                        .WhenStarted(s => s.Start())
                        .WhenStopped(s => s.Stop())
                    );
                x.SetDisplayName("Azure Files Replicator");
                x.SetServiceName(ServiceName);
                x.SetDescription("Replicate data from Azure Files to Azure Blob Storage.");
                x.StartAutomatically();
                x.RunAsLocalService();
                x.UseNLog();
            });
        }
    }

    public class CopyService
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        public string ConnectionString { get; private set; }

        public CopyService(string connectionString)
        {
            this.ConnectionString = connectionString;
        }

        public void Start()
        {
            logger.Debug($"Starting");
            var myThread = new Thread(new ThreadStart(Run)) { IsBackground = true };
            myThread.Start();
        }

        private CancellationTokenSource cts;
        public void Run()
        {
            cts = new CancellationTokenSource();
            RunAsync(cts.Token).Wait(cts.Token);
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                logger.Debug($"Running");
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            logger.Debug($"Stopped");
        }

        public void Stop()
        {
            logger.Debug($"Stopping");
            this.cts.Cancel();
            logger.Debug($"Cancellation sent");
        }
    }
}