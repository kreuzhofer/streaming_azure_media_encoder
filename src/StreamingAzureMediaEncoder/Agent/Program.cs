using System.IO;
using AzureChunkingMediaEncoder;

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
        const string ServiceName = "AzureFFMPEGEncoder";

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

        public static string StorageAccount { get { return Get("StorageAccount"); } }
        public static string EncodingThreads { get { return Get("EncodingThreads"); } }
        public static string TempFolder { get { return Get("TempFolder"); } }

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
                x.Service<FFMPEGService>(instance => instance
                        .ConstructUsing(() => new FFMPEGService(StorageAccount, EncodingThreads, TempFolder))
                        .WhenStarted(s => s.Start())
                        .WhenStopped(s => s.Stop())
                    );
                x.SetDisplayName("Azure FFMPEG Encoder");
                x.SetServiceName(ServiceName);
                x.SetDescription("Azure FFMPEG Streaming Blob Encoding.");
                x.StartAutomatically();
                x.RunAsLocalService();
                x.UseNLog();
            });
        }
    }

    public class FFMPEGService
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        public FFMPEGService(string storageAccount, string encodingThreads, string tempFolder)
        {
            this.StorageAccount = storageAccount;
            this.EncodingThreads = int.Parse(encodingThreads);
            this.TempFolder = tempFolder;
        }

        public string TempFolder { get; set; }

        public string StorageAccount { get; set; }

        public int EncodingThreads { get; set; }

        public void Start()
        {
            logger.Debug($"Starting worker...");
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
            try
            {
                // check for tempfolder to exist
                if (!Directory.Exists(TempFolder))
                {
                    Directory.CreateDirectory(TempFolder);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                throw;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                logger.Debug($"Starting workers...");
                var workersList = new List<Task>();
                for (int i = 0; i < EncodingThreads; i++)
                {
                    var task = Task.Factory.StartNew(() => { new DownloadAndEncodingTask().Start(StorageAccount, TempFolder, cancellationToken); }, cancellationToken);
                    workersList.Add(task);
                }
                Task.WaitAll(workersList.ToArray());
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