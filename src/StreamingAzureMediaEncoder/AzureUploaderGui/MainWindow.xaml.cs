using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using AzureChunkingMediaFileUploader;
using Microsoft.Practices.EnterpriseLibrary.WindowsAzure.TransientFaultHandling.AzureStorage;
using Microsoft.Practices.TransientFaultHandling;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared;

namespace AzureUploaderGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void buttonChooseVideo_Click(object sender, RoutedEventArgs e)
        {
            var picker = new OpenFileDialog();
            picker.RestoreDirectory = true;
            var fileResult = picker.ShowDialog();
            if (fileResult != null && fileResult.Value)
            {
                textBlockVideoFile.Text = picker.FileName;
            }
            Validate();
        }

        private void buttonChooseProfile_Click(object sender, RoutedEventArgs e)
        {
            var picker = new OpenFileDialog();
            picker.RestoreDirectory = true;
            var fileResult = picker.ShowDialog();
            if (fileResult != null && fileResult.Value)
            {
                textBlockProfilefile.Text = picker.FileName;
            }
            Validate();
        }

        private void Validate()
        {
            bool error = false;
            if (!String.IsNullOrEmpty(textBlockVideoFile.Text))
            {
                if (!File.Exists(textBlockVideoFile.Text))
                {
                    error = true;
                }
            }
            if (!String.IsNullOrEmpty(textBlockProfilefile.Text))
            {
                if (!File.Exists(textBlockProfilefile.Text))
                {
                    error = true;
                }
            }
            if (string.IsNullOrEmpty(textBlockProfilefile.Text) || string.IsNullOrEmpty(textBlockVideoFile.Text))
            {
                error = true;
            }
            buttonStartUpload.IsEnabled = !error;
        }

        private async void buttonStartUpload_Click(object sender, RoutedEventArgs e)
        {
            buttonStartUpload.IsEnabled = false;
            encodingProgress.Value = 0;
            uploadProgress.Value = 0;
            listBoxLog.Items.Clear();
            var jobId = Guid.NewGuid().ToString();
            var connectionString = ConfigurationManager.ConnectionStrings["AzureStorageConnection"].ConnectionString;
            ChunkingFileUploaderUtils.Log = Log;
            ChunkingFileUploaderUtils.Progress = i =>
            {
                Dispatcher.Invoke(() =>
                {
                    uploadProgress.Value = i;
                });
            };

            // load profile and validate
            var profileRawData = File.ReadAllText(textBlockProfilefile.Text);
            var profile = JsonConvert.DeserializeObject<ProfileDefinition>(profileRawData);
            var count = profile.renditions.Count;

            var uploadTask = ChunkingFileUploaderUtils.UploadAsync(jobId, textBlockVideoFile.Text, connectionString, textBlockProfilefile.Text);

            ProgressTracker.Log = Log;
            ProgressTracker.Progress = p =>
            {
                Dispatcher.Invoke(() =>
                {
                    encodingProgress.Value = p;
                });
            };
            var progressTask = ProgressTracker.TrackProgress(jobId, connectionString, count);

            await uploadTask;
            await progressTask;

            Log("sending notification...");
            await DoCallback(progressTask, jobId);
            Log("notification sent");

            buttonStartUpload.IsEnabled = true;
        }

        private async Task DoCallback(Task<bool> progressTask, string jobId)
        {
            try
            {
                var retryStrategy = new Incremental(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
                var retryPolicy = new RetryPolicy<HttpErrorStrategy>(retryStrategy);
                await retryPolicy.ExecuteAction(async () =>
                {
                // send callback when done
                var callbackTemplate = @"
{{
  ""trigger"": ""job"",
  ""subject"": ""{0}"",
  ""facts"": {{
      ""result"":""{1}"",
      ""id"":""{2}""
      }}
}}";
                    var status = progressTask.Result ? "done" : "failed";
                    var result = progressTask.Result ? "all tasks done" : "at least one task failed";

                    var callBackBody = String.Format(callbackTemplate, status, result, jobId);
                    var callBackEndpoint = ConfigurationManager.AppSettings["JobCallbackEndpoint"];

                    var client = new HttpClient();
                    var content = new StringContent(callBackBody, Encoding.UTF8, "application/json");
                    await client.PostAsync(new Uri(callBackEndpoint), content);
                });
            }
            catch
            {
                // ignore
            }
        }

        private void Log(string s)
        {
            Dispatcher.Invoke(() =>
            {
                listBoxLog.Items.Add(s);
                listBoxLog.ScrollIntoView(s);
            });
        }

    }
}
