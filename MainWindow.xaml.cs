using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Threading;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.IO.Compression;

namespace Steam_Authenticator_Updater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Action _cancelWork;
        private int maxEntries = 400;

        public MainWindow()
        {
            InitializeComponent();
            btnUpdate.IsEnabled = true;
            btnCancel.Content = "Exit";
            if (!Directory.Exists(Directory.GetCurrentDirectory() + @"\maFiles")) // Check if we're in the root directory. (maFiles is common among all SA versions)
                InsideRootDir = false;
            else if (Directory.Exists(Directory.GetCurrentDirectory() + @"\maFiles"))
                InsideRootDir = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (File.Exists(UpdateFilePath))
                {
                    File.Delete(UpdateFilePath); // cleaning up
                }
            }
            catch (Exception) { }
            try
            {
                if (!InsideRootDir)
                {
                    File.WriteAllBytes(Directory.GetCurrentDirectory() + @"\..\updated", new byte[444]);
                    System.Diagnostics.Process.Start(Directory.GetCurrentDirectory() + @"\..\Steam Authenticator.exe");
                }
                else if (InsideRootDir)
                {
                    File.WriteAllBytes(Directory.GetCurrentDirectory() + @"\updated", new byte[444]);
                    System.Diagnostics.Process.Start(Directory.GetCurrentDirectory() + @"\Steam Authenticator.exe");
                }
                else if (CustomPath)
                    System.Diagnostics.Process.Start(ExtractToPath + @"\Steam Desktop Authenticator.exe"); // failsafe? probably wont work
            }
            catch (Exception)
            {
            }

        }

        private string ExtractToPath;
        private string UpdateFilePath;
        private bool CustomPath;
        private bool InsideRootDir;
        private ZipArchive Archive;
        private bool Updating;

        private bool ChangeUpdatePaths()
        {
            MessageBox.Show("Select the location of your Steam Authenticator install.");
            var folderPicker = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Multiselect = false,
                Title = "Select where you store Steam Authenticator",
                InitialDirectory = Directory.GetCurrentDirectory()
            };
            if (folderPicker.ShowDialog() == CommonFileDialogResult.Ok)
                ExtractToPath = folderPicker.FileName.ToString() + "\\";
            else
                return false;
            MessageBox.Show("Please select the location of the update file");
            var filePicker = new CommonOpenFileDialog
            {
                Title = "Select the update file.",
                Multiselect = false,
                InitialDirectory = Directory.GetCurrentDirectory()
            };
            if (filePicker.ShowDialog() == CommonFileDialogResult.Ok)
                UpdateFilePath = filePicker.FileName;
            CustomPath = true;
            return true;
        }

        private void BtnUpdate_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Change the update path.
            ChangeUpdatePaths();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (Updating)
            {
                _cancelWork?.Invoke();
            }
            else
                Close();            
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            Updating = true;
            btnUpdate.IsEnabled = false;
            btnCancel.Content = "Cancel";

            try
            {
                if (!Directory.Exists(Directory.GetCurrentDirectory() + @"\maFiles")) // Check if we're in the root directory. (maFiles is common among all SA versions)
                    InsideRootDir = false;
                else if (Directory.Exists(Directory.GetCurrentDirectory() + @"\maFiles"))
                    InsideRootDir = true;

                if (File.Exists(Directory.GetCurrentDirectory() + @"\update.zip"))
                {
                    UpdateFilePath = Directory.GetCurrentDirectory() + @"\update.zip";
                }
                else if (File.Exists(Directory.GetCurrentDirectory() + @"\..\update.zip"))
                {
                    UpdateFilePath = Directory.GetCurrentDirectory() + @"\..\update.zip";
                }
                else if (CustomPath && File.Exists(UpdateFilePath))
                {}// It's already saved.
                else
                {
                    if (MessageBox.Show("The update file was not found. Would you like to find it?", "Error while extracting", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                    {
                        if (ChangeUpdatePaths() == false)
                            return;
                        else
                        if (!File.Exists(UpdateFilePath))
                        {
                            MessageBox.Show("That file doesn't exist. Try again");
                            btnUpdate.IsEnabled = true;
                            btnCancel.Content = "Exit";
                            Updating = false;
                            return;
                        }
                    }
                    else
                    {
                        btnUpdate.IsEnabled = true;
                        btnCancel.Content = "Exit";
                        Updating = false;
                        return;
                    }
                }

                Archive = ZipFile.OpenRead(UpdateFilePath);
                maxEntries = Archive.Entries.Count - 1;

            }
            catch (Exception err)
            {
                MessageBox.Show("Error finding the update file / path." + Environment.NewLine + "Error: " + err.Message, "Error searching.", MessageBoxButton.OK, MessageBoxImage.Error);
                btnUpdate.IsEnabled = true;
                btnCancel.Content = "Exit";
                Updating = false;
                return;
            }

            try
            {
                var cancellationTokenSource = new CancellationTokenSource();

                var progressReport = new Progress<string>((a) => UpdateProgress(a, false));
                var progressReportBar = new Progress<string>((a) => UpdateProgress(a, true));

                _cancelWork = () =>
                {
                    btnCancel.IsEnabled = false;
                    cancellationTokenSource.Cancel();
                };

                var token = cancellationTokenSource.Token;

                await Task.Run(() => Update(token, progressReport, progressReportBar, Archive), token);
                
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            //Quit
            btnUpdate.IsEnabled = false;
            btnCancel.IsEnabled = false;
            _cancelWork = null;
            Close();
        }

        private void UpdateProgress(string action, bool updateBars = false)
        {
            txtOutput.AppendText(action);
            txtOutput.ScrollToEnd();

            if (updateBars)
            {
                pbStatus.IsIndeterminate = false;
                pbStatus.Visibility = Visibility.Visible;
                statusCurrent.Content = pbStatus.Value + 1;
                pbStatus.Value = pbStatus.Value + 1;
                pbStatus.Maximum = maxEntries;
                statusMax.Content = maxEntries;

                pbTaskbar.ProgressValue = 1 - ((maxEntries - pbStatus.Value) / maxEntries); // 1-((max-current)/max) | left > to dec > reverse
                pbTaskbar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
            }            
        }

        private void Update(CancellationToken token, IProgress<string> progressReport, IProgress<string> progressReportBar, ZipArchive archive)
        {
            string path = "";
            if (!InsideRootDir)
                path = Directory.GetCurrentDirectory() + @"\..\";
            else if (CustomPath)
                path = ExtractToPath;
            else
                path = Directory.GetCurrentDirectory() + @"\";
            List<string> UnpackedFile = new List<string>();

            //MessageBox.Show("Path; " + path + Environment.NewLine + "In update dir; " + inUpdateFolder + Environment.NewLine + "Custom path?" + customPath);

            for (int i = 0; i < archive.Entries.Count - 1; i++)
            {
                try
                {
                    ZipArchiveEntry entry = archive.Entries[i];
                    progressReport.Report("Unpacking " + entry.FullName + " - ");

                    if (!entry.FullName.StartsWith(entry.Name))
                    {
                        string dir = entry.FullName.Remove(entry.FullName.Length - entry.Name.Length, entry.Name.Length);
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(path + dir);
                    }

                    if (entry.FullName == AppDomain.CurrentDomain.FriendlyName || entry.FullName.ToLower().Contains("mafiles"))
                        progressReportBar.Report("failed (Either blocked or the updater itself)." + Environment.NewLine);
                    else if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                    {
                        if (!Directory.Exists(entry.FullName))
                            Directory.CreateDirectory(path + entry.FullName);
                    }
                    else
                    {
                        if (File.Exists(path + entry.FullName))
                            File.Delete(path + entry.FullName);
                        entry.ExtractToFile(path + entry.FullName);
                        UnpackedFile.Add(entry.FullName);
                    }
                    progressReportBar.Report("completed." + Environment.NewLine);
                }
                catch (Exception ex) { MessageBox.Show("Error updating: " + ex.Message + Environment.NewLine + "Updated files: " + String.Join(Environment.NewLine, UnpackedFile) 
                    + Environment.NewLine + "", "Error while extracting", MessageBoxButton.OK, MessageBoxImage.Error); }
                try { token.ThrowIfCancellationRequested(); } catch { return; }
            }
        }
    }
}
