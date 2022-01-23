using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using SystemTrayApp.WPF;
using FileSharing.Models;
using FileSharing.Networking;

namespace FileSharing.ViewModels
{
    public class MainWindowViewModel : ObservableRecipient
    {
        private NotifyIconWrapper.NotifyRequestRecord? _notifyRequest;
        private bool _showInTaskbar;
        private WindowState _windowState;

        private readonly Client _client;
        private readonly FilesFromServers _availableFiles;
        private FileInfo _selectedAvailableFile;
        private readonly Downloads _downloads;
        private Download _selectedDownload;

        private readonly Server _server;

        public MainWindowViewModel()
        {
            LoadedCommand = new RelayCommand(Loaded);
            ClosingCommand = new RelayCommand<CancelEventArgs>(Closing);
            NotifyCommand = new RelayCommand(() => Notify("Hello world!"));
            NotifyIconOpenCommand = new RelayCommand(() => { WindowState = WindowState.Normal; });
            NotifyIconExitCommand = new RelayCommand(() => { Application.Current.Shutdown(); });

            ConnectToServerCommand = new RelayCommand(ConnectToServer);

            _client = new Client();
            _client.ServerUpdated += OnServerUpdated;
            _client.ServerRemoved += OnServerRemoved;
            _client.MessageReceived += OnClientMessageReceived;
            _availableFiles = new FilesFromServers();
            _availableFiles.FilesUpdated += AvailableFilesListUpdated;
            DownloadFileCommand = new RelayCommand<FileInfo>(DownloadFile);
            CancelDownloadCommand = new RelayCommand<Download>(CancelDownload);
            OpenFileInFolderCommand = new RelayCommand<Download>(OpenFileInFolder);
            _downloads = new Downloads();
            _downloads.DownloadsListUpdated += DownloadsListUpdated;

            _server = new Server(55000);
            _server.ClientUpdated += OnClientUpdated;
            _server.ClientRemoved += OnClientRemoved;
            _server.MessageReceived += OnServerMessageReceived;
        }

        #region System tray related stuff
        public ICommand LoadedCommand { get; }
        public ICommand ClosingCommand { get; }
        public ICommand NotifyCommand { get; }
        public ICommand NotifyIconOpenCommand { get; }
        public ICommand NotifyIconExitCommand { get; }

        public WindowState WindowState
        {
            get => _windowState;
            set
            {
                ShowInTaskbar = true;
                SetProperty(ref _windowState, value);
                ShowInTaskbar = value != WindowState.Minimized;
            }
        }

        public bool ShowInTaskbar
        {
            get => _showInTaskbar;
            set => SetProperty(ref _showInTaskbar, value);
        }

        public NotifyIconWrapper.NotifyRequestRecord? NotifyRequest
        {
            get => _notifyRequest;
            set => SetProperty(ref _notifyRequest, value);
        }

        private void Notify(string message)
        {
            NotifyRequest = new NotifyIconWrapper.NotifyRequestRecord
            {
                Title = "Notify",
                Text = message,
                Duration = 1000
            };
        }

        private void Loaded()
        {
            WindowState = WindowState.Normal;
        }

        private void Closing(CancelEventArgs? e)
        {
            if (e == null)
            {
                return;
            }
                
            e.Cancel = true;
            WindowState = WindowState.Minimized;
        }
        #endregion

        public ICommand ConnectToServerCommand { get; }
        public ICommand DownloadFileCommand { get; }
        public ICommand CancelDownloadCommand { get; }
        public ICommand OpenFileInFolderCommand { get; }
        public ObservableCollection<FileInfo> AvailableFiles => _availableFiles.AvailableFiles;
        public ObservableCollection<Download> Downloads => _downloads.DownloadsList;

        public FileInfo SelectedAvailableFile
        {
            get => _selectedAvailableFile;
            set => SetProperty(ref _selectedAvailableFile, value);
        }

        public Download SelectedDownload
        {
            get => _selectedDownload;
            set => SetProperty(ref _selectedDownload, value);
        }

        private void OnServerUpdated(object? sender, CryptoPeerEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnServerRemoved(object? sender, CryptoPeerEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnClientMessageReceived(object? sender, NetEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnClientUpdated(object? sender, CryptoPeerEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnClientRemoved(object? sender, CryptoPeerEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnServerMessageReceived(object? sender, NetEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void AvailableFilesListUpdated(object? sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void DownloadsListUpdated(object? sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void ConnectToServer()
        {
            //todo
        }

        private void DownloadFile(FileInfo? file)
        {
            if (file == null)
            {
                return;
            }

            Debug.WriteLine("(DownloadFileCommand)");

            var messageBoxQuestion = MessageBox.Show("Do you want to download this file: '" + file.Name + "'?",
                "Download Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (messageBoxQuestion != MessageBoxResult.Yes)
            {
                return;
            }

            var downloadFileInfo = new Download(fileInfo);
            if (_downloads.IsThereDownloadOfFileWithSameName(downloadFileInfo.Name, out string downloadID))
            {
                var messageBoxWarning = MessageBox.Show("File '" + fileInfo.Name + "' is already downloading! Do you want to restart download of this file?",
                    "Restart Download Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Exclamation);

                if (messageBoxWarning == MessageBoxResult.Yes)
                {
                    SendFileDenial(downloadID);
                    _downloads[downloadID].Cancel();
                    PrepareForFileReceiving(downloadFileInfo);
                    SendFileRequest(downloadFileInfo);
                }
            }
            else
            if (File.Exists(downloadFileInfo.Path))
            {
                var messageBoxWarning = MessageBox.Show("File '" + fileInfo.Name + "' already exists. Do you want to download this file again?",
                    "Download Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Exclamation);

                if (messageBoxWarning == MessageBoxResult.Yes)
                {
                    PrepareForFileReceiving(downloadFileInfo);
                    SendFileRequest(downloadFileInfo);
                }
            }
            else
            {
                PrepareForFileReceiving(downloadFileInfo);
                SendFileRequest(downloadFileInfo);
            }
        }

        private void CancelDownload(Download? download)
        {
            if (download == null)
            {
                return;
            }

            var messageBoxQuestion = MessageBox.Show("Do you want to cancel the download of this file: '" + download.Name + "'?",
                "Cancel Download Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (messageBoxQuestion == MessageBoxResult.Yes)
            {
                SendFileDenial(download.ID);
                _downloads[download.ID].Cancel();
            }
        }

        private void OpenFileInFolder(Download? download)
        {
            if (download == null)
            {
                return;
            }

            //todo
        }
    }
}
