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
using FileSharing.Networking.Utils;
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
                Debug.WriteLine("(OnServerAdded) Adding files list of server " + server.EndPoint);

                _availableFiles.AddServer(server);
                OnPropertyChanged(nameof(Servers));
            }
        }

        private void OnServerConnected(object? sender, EncryptedPeerEventArgs e)
        {
            var server = _client.GetServerByID(e.PeerID);
            if (server != null)
            {
                Debug.WriteLine("(OnServerConnected) Requesting files list from server " + server.EndPoint);

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
                MessageBox.Show("Couldn't add file to share: " + e.Path, 
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
                case NetMessageType.ResendFileSegment:
                    ReceiveFileSegment(server, reader);
                    break;

                case NetMessageType.CancelDownload:
                    ReceiveDownloadCancellation(reader);
                    break;

                default:
                case NetMessageType.None:
                    Debug.WriteLine("(Client_ProcessIncomingMessage) Unknown type");
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

                case NetMessageType.FileSegment:
                    SendFileSegmentToClient(client, reader);
                    break;

                case NetMessageType.ResendFileSegment:
                    ResendFileSegmentToClient(client, reader);
                    break;

                case NetMessageType.FileSegmentAck:
                    ReceiveAckFromClient(client, reader);
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
            if (addressDialog.AskServerAddressAndPort(_defaultServerAddress, out IPEndPoint? address) &&
                address != null)
            {
                _client.ConnectToServer(address);
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

            if (_downloads.HasDownloadWithSamePath(newDownload.FilePath, out string downloadID))
            {
                var confirmDownloadRestart = MessageBox.Show($"File '{file.Name}' is already downloading! Do you want to restart download of this file?",
                    "Restart Download Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Exclamation);

                if (confirmDownloadRestart == MessageBoxResult.Yes)
                {
                    SendFileDenial(server, downloadID);
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

            if (download.IsDownloaded)
            {
                MessageBox.Show($"File '{download.Name}' is already downloaded.",
                    "Cancel download notification",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (download.IsCancelled)
            {
                MessageBox.Show($"Download of file '{download.Name}' is already cancelled!",
                    "Cancel download notification",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            var confirmDownloadCancellation = MessageBox.Show(
                $"Do you want to cancel the download of this file: '{download.Name}'?",
                "Cancel Download Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmDownloadCancellation == MessageBoxResult.Yes)
            {
                if (_client.IsConnectedToServer(download.Server.Id, out EncryptedPeer? server) &&
                    server != null)
                {
                    SendFileDenial(server, download.ID);
                }
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

                if (sharedFile.IsHashCalculated)
                {
                    foreach (var upload in _uploads.GetAllUploadsOfFile(sharedFile.Hash))
                    {
                        upload.Cancel();
                        upload.Destination.SendUploadDenial(upload.ID);
                    }
                }
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

            if (_sharedFiles.HasFileAvailable(fileHash) &&
                !_uploads.Has(uploadID))
            {
                var desiredFile = _sharedFiles.GetByHash(fileHash);
                if (desiredFile == null)
                {
                    Debug.WriteLine($"(StartSendingFileToClient) File {fileHash} was not found");

                    return;
                }

                var upload = new Upload(uploadID,
                    desiredFile.Name,
                    desiredFile.Size,
                    desiredFile.Hash,
                    destination,
                    desiredFile.NumberOfSegments);

                _uploads.Add(upload);

                Debug.WriteLine($"(StartSendingFileToClient) Upload {uploadID} of file {desiredFile.Name} has started. " +
                    $"Segments count: {desiredFile.NumberOfSegments}");

                SendFileSegmentToClient(destination, upload.ID, 0);
            }
        }

        private void SendFileSegmentToClient(EncryptedPeer destination, NetDataReader reader)
        {
            if (!reader.TryGetString(out string uploadID) ||
                !reader.TryGetLong(out long numOfSegment))
            {
                return;
            }

            SendFileSegmentToClient(destination, uploadID, numOfSegment);
        }

        private void SendFileSegmentToClient(EncryptedPeer destination, string uploadID, long numberOfSegment)
        {
            var upload = _uploads.Get(uploadID);

            if (upload != null &&
                upload.IsActive &&
                _sharedFiles.HasFileAvailable(upload.FileHash))
            {
                var file = _sharedFiles.GetByHash(upload.FileHash);
                if (file == null)
                {
                    Debug.WriteLine($"(SendFileSegmentToClient) File {upload.FileHash} was not found");

                    return;
                }

                var segment = file.TryReadSegment(numberOfSegment);
                if (segment.Length > 0)
                {
                    var message = new NetDataWriter();
                    message.Put((byte)NetMessageType.FileSegment);
                    message.Put(uploadID);
                    message.Put(numberOfSegment);
                    message.Put(CRC32.Compute(segment));
                    message.Put(segment.Length);
                    message.Put(segment);

                    destination.SendEncrypted(message, 1);
                }
            }
        }

        private void ResendFileSegmentToClient(EncryptedPeer destination, NetDataReader reader)
        {
            if (!reader.TryGetString(out string uploadID) ||
                !reader.TryGetString(out string fileHash) ||
                !reader.TryGetLong(out long numberOfSegment))
            {
                return;
            }

            var upload = _uploads.Get(uploadID);

            if (upload != null &&
                upload.IsActive &&
                _sharedFiles.HasFileAvailable(fileHash))
            {
                var file = _sharedFiles.GetByHash(fileHash);
                if (file == null)
                {
                    return;
                }

                var segment = file.TryReadSegment(numberOfSegment);
                if (segment.Length > 0)
                {
                    var message = new NetDataWriter();
                    message.Put((byte)NetMessageType.ResendFileSegment);
                    message.Put(uploadID);
                    message.Put(numberOfSegment);
                    var crc = CRC32.Compute(segment);
                    message.Put(crc);
                    message.Put(segment.Length);
                    message.Put(segment);

                    destination.SendEncrypted(message, 1);
                    upload.AddResendedSegment();
                }
            }
        }

        private void SendFileDenial(EncryptedPeer server, string downloadID)
        {
            var download = _downloads.Get(downloadID);
            if (download == null)
            {
                return;
            }

            download.Cancel();

            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.CancelDownload);
            message.Put(downloadID);

            server.SendEncrypted(message, 0);
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
                    $"Can't download file {download.Name}",
                    1500,
                    System.Windows.Forms.ToolTipIcon.Error);
            }
        }

        private void ReceiveFilesList(EncryptedPeer server, NetDataReader reader)
        {
            if (!reader.TryGetUInt(out uint receivedCrc) ||
                !reader.TryGetString(out string jsonFilesList))
            {
                return;
            }

            if (receivedCrc != CRC32.Compute(jsonFilesList))
            {
                server.SendFilesListRequest();

                Debug.WriteLine("(ReceiveFilesList_Warning) CRC32's of received files list don't match!");
                return;
            }

            var filesList = JsonConvert.DeserializeObject<List<SharedFileInfo>>(jsonFilesList);
            if (filesList != null)
            {
                var filesFromServer = _availableFiles.Get(server.Id);
                if (filesFromServer == null)
                {
                    return;
                }

                filesFromServer.UpdateWith(filesList);
            }
        }

        private void ReceiveFileSegment(EncryptedPeer server, NetDataReader reader)
        {
            Debug.WriteLine("(ReceiveFileSegment) Start");

            if (!reader.TryGetString(out string downloadID) ||
                !reader.TryGetLong(out long numOfSegment) ||
                !reader.TryGetUInt(out uint receivedCrc) ||
                !reader.TryGetBytesWithLength(out byte[] segment))
            {
                Debug.WriteLine("(ReceiveFileSegment) Can't retrieve the data");

                return;
            }

            var download = _downloads.Get(downloadID);
            if (download == null)
            {
                Debug.WriteLine($"(ReceiveFileSegment) No such download: {downloadID}");

                return;
            }

            switch (download.TryWrite(receivedCrc, numOfSegment, segment))
            {
                default:
                case DownloadingFileWriteStatus.DoNothing:
                    Debug.WriteLine("(ReceiveFileSegment_Result) Default or DoNothing");
                    break;

                case DownloadingFileWriteStatus.Success:
                    Debug.WriteLine("(ReceiveFileSegment_Result) Success");

                    server.SendFileSegmentAck(downloadID, numOfSegment);
                    break;

                case DownloadingFileWriteStatus.Failure:
                    Debug.WriteLine("(ReceiveFileSegment_Result) Failure");

                    server.RequestFileSegment(downloadID, download.Hash, numOfSegment);
                    break;
            }
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

        private void ReceiveAckFromClient(EncryptedPeer client, NetDataReader reader)
        {
            Debug.WriteLine($"(ReceiveAckFromClient) Start");

            if (!reader.TryGetString(out string uploadID) ||
                !reader.TryGetLong(out long numOfSegment))
            {
                Debug.WriteLine($"(ReceiveAckFromClient) Can't get the data");

                return;
            }

            var upload = _uploads.Get(uploadID);
            if (upload == null)
            {
                Debug.WriteLine($"(ReceiveAckFromClient) No such upload: {uploadID}");

                return;
            }

            switch (upload.AddAck(numOfSegment))
            {
                default:
                case UploadingFileAckStatus.DoNothing:
                    Debug.WriteLine($"(ReceiveAckFromClient_Result) Default or DoNothing");
                    break;

                case UploadingFileAckStatus.Success:
                    var freeSegmentNumber = upload.NumberOfAckedSegments;

                    Debug.WriteLine($"(ReceiveAckFromClient_Result) Success, sending new segment: {freeSegmentNumber}");

                    if (freeSegmentNumber != -1)
                    {
                        SendFileSegmentToClient(client, uploadID, freeSegmentNumber);
                    }
                    break;
            }
        }


        private void CancelUpload(NetDataReader reader)
        {
            if (!reader.TryGetString(out string uploadID))
            {
                return;
            }

            var upload = _uploads.Get(uploadID);
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
            _server.StartListening(_defaultPort);

            /*var isFreePortChosen = false;
            var defaultPort = _defaultPort;
            var port = 0;
            var portNumberDialog = new InputBoxWindow();
            do
            {
                if (portNumberDialog.AskPort(defaultPort, out int portNumber))
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

            _server.StartListening(port);*/
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
}