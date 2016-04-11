using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
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
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            dynamic profile = JsonConvert.DeserializeObject(profileRawData);
            var count = ((JArray)profile.renditions).Count;


            var uploadTask = ChunkingFileUploaderUtils.UploadAsync(jobId, textBlockVideoFile.Text, connectionString, textBlockProfilefile.Text);
            ProgressTracker.Log = Log;
            var progressTask = ProgressTracker.TrackProgress(jobId, connectionString, count);

            await uploadTask;
            await progressTask;

            buttonStartUpload.IsEnabled = true;
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
