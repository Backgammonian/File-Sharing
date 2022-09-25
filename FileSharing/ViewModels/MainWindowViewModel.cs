using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.IO;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Win32;
using LiteNetLib.Utils;
using SystemTrayApp.WPF;
using Newtonsoft.Json;
using FileSharing.Models;
using FileSharing.Networking;
using InputBox;
using Behaviours;
using Extensions;
using Helpers;

namespace FileSharing.ViewModels
{
    public sealed partial class MainWindowViewModel : ObservableObject
    {
        private const int _defaultPort = 55000;
        private static readonly IPEndPoint _defaultServerAddress = new IPEndPoint(LocalAddressResolver.GetLocalAddress(), _defaultPort);

        private readonly Client _client;
        private readonly FilesFromServers _availableFiles;
        private readonly Downloads _downloads;
        private readonly Server _server;
        private readonly SharedFiles _sharedFiles;
        private readonly Uploads _uploads;

        public MainWindowViewModel()
        {
            InitializeSystemTrayCommands();

            ConnectToServerCommand = new RelayCommand(ConnectToServer);
            DownloadFileCommand = new RelayCommand<SharedFileInfo>(DownloadFile);
            CancelDownloadCommand = new RelayCommand<Download>(CancelDownload);
            OpenFileInFolderCommand = new RelayCommand<Download>(OpenFileInFolder);
            DisconnectFromServerCommand = new RelayCommand<EncryptedPeer>(DisconnectFromServer);
            RemoveSharedFileCommand = new RelayCommand<SharedFile>(RemoveSharedFile);
            AddFileCommand = new RelayCommand(AddFile);
            GetFileToShareCommand = new RelayCommand<FilesDroppedEventArgs?>(GetFileToShare);
            ShowLocalPortCommand = new RelayCommand(ShowLocalPort);

            _client = new Client();
            _client.ServerAdded += OnServerAdded;
            _client.ServerConnected += OnServerConnected;
            _client.ServerRemoved += OnServerRemoved;
            _client.MessageReceived += OnClientMessageReceived;

            _availableFiles = new FilesFromServers();
            _availableFiles.FilesUpdated += OnAvailableFilesListUpdated;

            _downloads = new Downloads();
            _downloads.DownloadsListUpdated += OnDownloadsListUpdated;
            _downloads.DownloadFinished += OnDownloadFinished;

            _server = new Server();
            _server.ClientAdded += OnClientAdded;
            _server.ClientRemoved += OnClientRemoved;
            _server.MessageReceived += OnServerMessageReceived;

            _sharedFiles = new SharedFiles();
            _sharedFiles.SharedFileAdded += OnSharedFileAdded;
            _sharedFiles.SharedFileHashCalculated += OnSharedFileHashCalculated;
            _sharedFiles.SharedFileError += OnSharedFileError;
            _sharedFiles.SharedFileRemoved += OnSharedFileRemoved;

            _uploads = new Uploads();
            _uploads.UploadAdded += OnUploadAdded;
            _uploads.UploadRemoved += OnUploadRemoved;
        }

        #region View-model bindings
        public ICommand ConnectToServerCommand { get; }
        public ICommand DownloadFileCommand { get; }
        public ICommand CancelDownloadCommand { get; }
        public ICommand OpenFileInFolderCommand { get; }
        public ICommand DisconnectFromServerCommand { get; }
        public ICommand RemoveSharedFileCommand { get; }
        public ICommand AddFileCommand { get; }
        public ICommand ShowLocalPortCommand { get; }
        public ICommand GetFileToShareCommand { get; }
        public ObservableCollection<SharedFileInfo> AvailableFiles => new ObservableCollection<SharedFileInfo>(_availableFiles.List);
        public ObservableCollection<Download> Downloads => new ObservableCollection<Download>(_downloads.DownloadsList);
        public ObservableCollection<EncryptedPeer> Servers => new ObservableCollection<EncryptedPeer>(_client.Servers);
        public ObservableCollection<SharedFile> SharedFiles => new ObservableCollection<SharedFile>(_sharedFiles.SharedFilesList);
        public ObservableCollection<Upload> Uploads => new ObservableCollection<Upload>(_uploads.UploadsList);
        public ObservableCollection<EncryptedPeer> Clients => new ObservableCollection<EncryptedPeer>(_server.Clients);
        #endregion

        #region Client & Server event handlers
        private void OnServerAdded(object? sender, EncryptedPeerEventArgs e)
        {
            var server = _client.GetServerByID(e.PeerID);
            if (server != null)
            {
                _availableFiles.AddServer(server);
                OnPropertyChanged(nameof(Servers));
            }
        }

        private void OnServerConnected(object? sender, EncryptedPeerEventArgs e)
        {
            var server = _client.GetServerByID(e.PeerID);
            if (server != null)
            {
                server.SendFilesListRequest();
            }
        }

        private void OnServerRemoved(object? sender, EncryptedPeerEventArgs e)
        {
            _downloads.CancelAllDownloadsFromServer(e.PeerID);
            _availableFiles.RemoveServer(e.PeerID);
            OnPropertyChanged(nameof(Servers));
        }

        private void OnAvailableFilesListUpdated(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(AvailableFiles));
        }

        private void OnDownloadsListUpdated(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(Downloads));
        }

        private void OnDownloadFinished(object? sender, DownloadFinishedEventArgs e)
        {
            Notify("Download is finished",
                $"Download of file {e.DownloadedFileName} has finished!",
                1500,
                System.Windows.Forms.ToolTipIcon.Info);
        }

        private void OnClientAdded(object? sender, EncryptedPeerEventArgs e)
        {
            OnPropertyChanged(nameof(Clients));
        }

        private void OnClientRemoved(object? sender, EncryptedPeerEventArgs e)
        {
            _uploads.CancelAllUploadsOfPeer(e.PeerID);
            OnPropertyChanged(nameof(Clients));
        }

        private void OnSharedFileAdded(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(SharedFiles));
        }

        private void OnSharedFileHashCalculated(object? sender, EventArgs e)
        {
            _server.SendFilesListToAllClients(_sharedFiles.GetAvailableFiles());
        }

        private void OnSharedFileError(object? sender, SharedFileEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show($"Couldn't add file to share: {e.Path}", 
                    "File sharing error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }));
        }

        private void OnSharedFileRemoved(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(SharedFiles));
            _server.SendFilesListToAllClients(_sharedFiles.GetAvailableFiles());
        }

        private void OnUploadAdded(object? sender, UploadEventArgs e)
        {
            OnPropertyChanged(nameof(Uploads));
        }

        private void OnUploadRemoved(object? sender, UploadEventArgs e)
        {
            OnPropertyChanged(nameof(Uploads));
        }

        private void OnClientMessageReceived(object? sender, NetEventArgs e)
        {
            var reader = e.Message;
            var server = e.CryptoPeer;

            if (!reader.TryGetByte(out byte typeByte) ||
                !typeByte.TryParseType(out NetMessageType type))
            {
                return;
            }

            Debug.WriteLine($"(Client_ProcessIncomingMessage) Source: {server.EndPoint}");
            Debug.WriteLine($"(Client_ProcessIncomingMessage) Type: {type}");

            switch (type)
            {
                case NetMessageType.FilesList:
                    ReceiveFilesList(server, reader);
                    break;

                case NetMessageType.FileSegment:
                    ReceiveFileSegment(reader);
                    break;

                case NetMessageType.CancelDownload:
                    ReceiveDownloadCancellation(reader);
                    break;
                     
                default:
                case NetMessageType.None:
                    break;
            }
        }

        private void OnServerMessageReceived(object? sender, NetEventArgs e)
        {
            var reader = e.Message;
            var client = e.CryptoPeer;

            if (!reader.TryGetByte(out byte typeByte) ||
                !typeByte.TryParseType(out NetMessageType type))
            {
                return;
            }

            Debug.WriteLine($"(Server_ProcessIncomingMessage) Source: {client.EndPoint}");
            Debug.WriteLine($"(Server_ProcessIncomingMessage) Type: {type}");

            switch (type)
            {
                case NetMessageType.FilesListRequest:
                    client.SendFilesList(_sharedFiles.GetAvailableFiles());
                    break;

                case NetMessageType.FileRequest:
                    StartSendingFileToClient(client, reader);
                    break;

                case NetMessageType.FileSegmentAck:
                    ReceiveAckFromClient(reader);
                    break;

                case NetMessageType.CancelDownload:
                    CancelUpload(reader);
                    break;

                default:
                case NetMessageType.None:
                    Debug.WriteLine("(Server_ProcessIncomingMessage) Unknown type");
                    break;
            }
        }
        #endregion

        #region Commands implementations
        private void ConnectToServer()
        {
            var addressDialog = new InputBoxWindow();
            var serverAddress = addressDialog.AskServerAddressAndPort(_defaultServerAddress);

            if (serverAddress != null)
            {
                _client.ConnectToServer(serverAddress);
            }
            else
            {
                MessageBox.Show("Server address is not valid! Try enter correct IP address and port (example: 10.0.8.100:55000)",
                    "Server address error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void DownloadFile(SharedFileInfo? file)
        {
            if (file == null)
            {
                return;
            }

            if (file.Server == null)
            {
                MessageBox.Show($"File {file.Name} is unreachable: unknown file server",
                    "Download error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            if (!_client.IsConnectedToServer(file.Server.Id, out EncryptedPeer? server) ||
                server == null)
            {
                MessageBox.Show($"File '{file.Name}' is unreachable: no connection to file server",
                    "Download error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            var fileName = Path.GetFileNameWithoutExtension(file.Name);
            var fileExtension = Path.GetExtension(file.Name);

            var saveFileDialog = new SaveFileDialog()
            {
                FileName = fileName,
                DefaultExt = fileExtension,
                ValidateNames = true,
                Filter = fileExtension.GetAppropriateFileFilter()
            };

            var dialogResult = saveFileDialog.ShowDialog();
            if (!dialogResult.HasValue ||
                !dialogResult.Value)
            {
                return;
            }

            Debug.WriteLine($"(DownloadFile) Path: {saveFileDialog.FileName}");

            var newDownload = new Download(file, server, saveFileDialog.FileName);

            var duplicateDownload = _downloads.HasDownloadWithSamePath(newDownload.FilePath);
            if (duplicateDownload != null)
            {
                var confirmDownloadRestart = MessageBox.Show($"File '{file.Name}' is already downloading! Do you want to restart download of this file?",
                    "Restart Download Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Exclamation);

                if (confirmDownloadRestart == MessageBoxResult.Yes)
                {
                    duplicateDownload.Cancel();
                    PrepareForFileReceiving(server, newDownload);
                }

                return;
            }

            if (File.Exists(newDownload.FilePath))
            {
                var confirmDownloadRepeat = MessageBox.Show($"File '{file.Name}' already exists. Do you want to download this file again?",
                    "Download Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Exclamation);

                if (confirmDownloadRepeat == MessageBoxResult.Yes)
                {
                    PrepareForFileReceiving(server, newDownload);
                }

                return;
            }

            PrepareForFileReceiving(server, newDownload);
        }

        private void CancelDownload(Download? download)
        {
            if (download == null)
            {
                return;
            }

            var confirmDownloadCancellation = MessageBox.Show($"Do you want to cancel the download of the file '{download.Name}'?",
                "Cancel Download Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmDownloadCancellation == MessageBoxResult.Yes)
            {
                download.Cancel();
            }
        }

        private void OpenFileInFolder(Download? download)
        {
            if (download == null)
            {
                return;
            }

            if (!File.Exists(download.FilePath))
            {
                MessageBox.Show($"File '{download.Name}' was removed or deleted.",
                    "File not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            var argument = $"/select, \"{download.FilePath}\"";
            Process.Start("explorer.exe", argument);
        }

        private void DisconnectFromServer(EncryptedPeer? server)
        {
            if (server == null)
            {
                return;
            }

            var confirmDisconnect = MessageBox.Show(
                $"Do you want to disconnect from server {server.EndPoint}?",
                "Disconnect Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmDisconnect == MessageBoxResult.Yes)
            {
                _client.DisconnectFromServer(server);
                _availableFiles.RemoveServer(server.Id);
            }
        }

        private void RemoveSharedFile(SharedFile? sharedFile)
        {
            if (sharedFile == null)
            {
                return;
            }

            var messageBoxResult = MessageBox.Show(
                $"Do you want to stop sharing this file: '{sharedFile.Name}'? Users' current downloads will be stopped.",
                "Delete Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (messageBoxResult == MessageBoxResult.Yes)
            {
                _sharedFiles.RemoveFile(sharedFile.Index);
            }
        }

        private void AddFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "All files(*.*)|*.*",
                Title = "Select file to share"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _sharedFiles.AddFile(openFileDialog.FileName);
            }
        }

        private void GetFileToShare(FilesDroppedEventArgs? args)
        {
            if (args == null ||
                args.FilesPath.Length == 0)
            {
                return;
            }

            foreach (var file in args.FilesPath)
            {
                if (File.Exists(file))
                {
                    Debug.WriteLine("(OnFilesDropped) Dragging file: " + file);

                    _sharedFiles.AddFile(file);
                }
            }
        }

        private void ShowLocalPort()
        {
            var port = _server.LocalPort;
            var text = port == 0 ? "Local File server is not working yet." : "Local file server port: " + port;
            MessageBox.Show(text, 
                "Local port info", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
        }
        #endregion

        #region Methods for information exchange between clients and servers
        private void StartSendingFileToClient(EncryptedPeer destination, NetDataReader reader)
        {
            if (!reader.TryGetString(out string fileHash) ||
                !reader.TryGetString(out string uploadID))
            {
                return;
            }

            var desiredFile = _sharedFiles.GetByHash(fileHash);
            if (desiredFile == null)
            {
                return;
            }

            var upload = new Upload(uploadID, desiredFile, destination);
            if (_uploads.Has(upload.ID))
            {
                upload.Cancel();
            }

            _uploads.Add(upload);
            upload.StartUpload();

            Debug.WriteLine($"(StartSendingFileToClient) Upload {uploadID} of file {desiredFile.Name} has started. " +
                $"Segments count: {desiredFile.NumberOfSegments}");
        }

        private void PrepareForFileReceiving(EncryptedPeer server, Download download)
        {
            if (_downloads.TryAddDownload(download))
            {
                server.SendFileRequest(download);

                Notify("New download",
                    $"File {download.Name} is now downloading!",
                    1500,
                    System.Windows.Forms.ToolTipIcon.Info);
            }
            else
            {
                Notify("Download error",
                    $"Can't start the download of file {download.Name}",
                    1500,
                    System.Windows.Forms.ToolTipIcon.Error);
            }
        }

        private void ReceiveFilesList(EncryptedPeer server, NetDataReader reader)
        {
            if (!reader.TryGetString(out string jsonFilesList))
            {
                return;
            }

            var filesList = JsonConvert.DeserializeObject<List<SharedFileInfo>>(jsonFilesList);
            if (filesList == null)
            {
                return;
            }
            
            var filesFromServer = _availableFiles.GetServerByID(server.Id);
            if (filesFromServer == null)
            {
                return;
            }

            filesFromServer.UpdateWith(filesList);
        }

        private void ReceiveFileSegment(NetDataReader reader)
        {
            if (!reader.TryGetString(out string downloadID) ||
                !reader.TryGetBytesWithLength(out byte[] segment))
            {
                return;
            }

            var download = _downloads.Get(downloadID);
            if (download == null)
            {
                return;
            }

            download.Write(segment);
        }

        private void ReceiveDownloadCancellation(NetDataReader reader)
        {
            if (!reader.TryGetString(out string downloadID))
            {
                return;
            }

            var download = _downloads.Get(downloadID);
            if (download == null)
            {
                return;
            }

            download.Cancel();
        }

        private void ReceiveAckFromClient(NetDataReader reader)
        {
            if (!reader.TryGetString(out string uploadID))
            {
                return;
            }

            var upload = _uploads.GetUploadByID(uploadID);
            if (upload == null)
            {
                return;
            }

            if (upload.AddAck())
            {
                upload.SendNextFileSegment();
            }
        }


        private void CancelUpload(NetDataReader reader)
        {
            if (!reader.TryGetString(out string uploadID))
            {
                return;
            }

            var upload = _uploads.GetUploadByID(uploadID);
            if (upload != null)
            {
                upload.Cancel();
            }
        }
        #endregion

        #region Methods for enabling and disabling application
        private void StartApp()
        {
            _client.StartListening();
            //_server.StartListening(_defaultPort);

            var isFreePortChosen = false;
            var defaultPort = _defaultPort;
            var port = 0;
            var portNumberDialog = new InputBoxWindow();
            do
            {
                var portNumber = portNumberDialog.AskPort(defaultPort);
                if (portNumber != -1)
                {
                    if (!portNumber.IsPortOccupied())
                    {
                        isFreePortChosen = true;
                        port = portNumber;
                    }
                    else
                    {
                        MessageBox.Show($"Port {portNumber} is already occupied! Try another port", 
                            "Local server port error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    //app will take default port number if it's not occupied
                    //otherwise it won't start at all

                    if (!defaultPort.IsPortOccupied())
                    {
                        isFreePortChosen = true;
                        port = defaultPort;
                    }
                    else
                    {
                        MessageBox.Show($"Default port {defaultPort} is already occupied, appliction will be shut down!",
                            "Occupied port error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        Application.Current.Shutdown();
                    }
                }
            }
            while (!isFreePortChosen);

            _server.StartListening(port);
        }

        private void ShutdownApp()
        {
            var question = MessageBox.Show("Do you want to close FileSharing™©? Current downloads and uploads will be stopped!",
                "Shutdown Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (question != MessageBoxResult.Yes)
            {
                return;
            }

            _client.DisconnectAll();
            _downloads.ShutdownAllDownloads();

            _server.DisconnectAll();
            _sharedFiles.CloseAllFileStreams();

            Application.Current.Shutdown();
        }
        #endregion
    }

    #region Notifications and system tray
    public partial class MainWindowViewModel
    {
        private NotifyIconWrapper.NotifyRequestRecord? _notifyRequest;
        private bool _showInTaskbar;
        private WindowState _windowState;

        public ICommand? LoadedCommand { get; private set; }
        public ICommand? ClosingCommand { get; private set; }
        public ICommand? NotifyIconOpenCommand { get; private set; }
        public ICommand? NotifyIconExitCommand { get; private set; }

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

        private void Notify(string title, string message, int durationMs, System.Windows.Forms.ToolTipIcon icon)
        {
            NotifyRequest = new NotifyIconWrapper.NotifyRequestRecord
            {
                Title = title,
                Text = message,
                Duration = durationMs,
                Icon = icon,
            };
        }

        private void Loaded()
        {
            WindowState = WindowState.Normal;

            StartApp();
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

        private void InitializeSystemTrayCommands()
        {
            LoadedCommand = new RelayCommand(Loaded);
            ClosingCommand = new RelayCommand<CancelEventArgs>(Closing);
            NotifyIconOpenCommand = new RelayCommand(() => { WindowState = WindowState.Normal; });
            NotifyIconExitCommand = new RelayCommand(ShutdownApp);
        }
    }
    #endregion
}