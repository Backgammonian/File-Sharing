using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
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

namespace FileSharing.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private static readonly IPEndPoint _defaultServerAddress = new IPEndPoint(IPAddress.Parse("192.168.0.14"), 55000);

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
            AddFileCommand = new AsyncRelayCommand(AddFile);
            GetFileToShareCommand = new AsyncRelayCommand<FilesDroppedEventArgs?>(GetFileToShare);
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
            _downloads.MissingSegmentsRequested += OnMissingFileSegmentsRequested;
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

        private async Task OnMissingFileSegmentsRequested(object? sender, MissingSegmentsEventArgs args)
        {
            var server = args.Server;
            await server.RequestMissingFileSegments(args.DownloadID,
                args.FileHash,
                args.NumbersOfMissingSegments);
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

        private async Task OnClientMessageReceived(object? sender, NetEventArgs e)
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
                    await ReceiveFileSegment(server, reader);
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

        private async Task OnServerMessageReceived(object? sender, NetEventArgs e)
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
                    await StartSendingFileToClient(client, reader);
                    break;

                case NetMessageType.FileSegment:
                    await SendFileSegmentToClient(client, reader);
                    break;

                case NetMessageType.ResendFileSegment:
                    await ResendFileSegmentToClient(client, reader);
                    break;

                case NetMessageType.FileSegmentAck:
                    await ReceiveAckFromClient (client, reader);
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
                var confirmDownloadRestart = MessageBox.Show(
                    $"File '{file.Name}' is already downloading! Do you want to restart download of this file?",
                    "Restart Download Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Exclamation);

                if (confirmDownloadRestart == MessageBoxResult.Yes)
                {
                    SendFileDenial(server, downloadID);
                    PrepareForFileReceiving(newDownload);
                    server.SendFileRequest(newDownload);
                }
            }
            else
            if (File.Exists(newDownload.FilePath))
            {
                var confirmDownloadRepeat = MessageBox.Show(
                    $"File '{file.Name}' already exists. Do you want to download this file again?",
                    "Download Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Exclamation);

                if (confirmDownloadRepeat == MessageBoxResult.Yes)
                {
                    PrepareForFileReceiving(newDownload);
                    server.SendFileRequest(newDownload);
                }
            }
            else
            {
                PrepareForFileReceiving(newDownload);
                server.SendFileRequest(newDownload);
            }
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

        private async Task AddFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "All files(*.*)|*.*",
                Title = "Select file to share"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await _sharedFiles.AddFile(openFileDialog.FileName);
            }
        }

        private async Task GetFileToShare(FilesDroppedEventArgs? args)
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

                    await _sharedFiles.AddFile(file);
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
        private async Task StartSendingFileToClient(EncryptedPeer destination, NetDataReader reader)
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
                    return;
                }

                var upload = new Upload(uploadID,
                    desiredFile.Name,
                    desiredFile.Size,
                    desiredFile.Hash,
                    destination,
                    desiredFile.NumberOfSegments);

                _uploads.Add(upload);

                byte channelNumber = 0;
                var initialSegmentsCount = upload.NumberOfSegments < Constants.ChannelsCount ? upload.NumberOfSegments : Constants.ChannelsCount;

                Debug.WriteLine($"(StartSendingFileToClient) Upload {uploadID} has started!");
                Debug.WriteLine($"(StartSendingFileToClient) InitialSegmentsCount: {initialSegmentsCount}");

                for (byte numberOfSegment = 0; numberOfSegment < initialSegmentsCount; numberOfSegment++)
                {
                    await SendFileSegmentToClient (destination, channelNumber, upload.ID, numberOfSegment);
                    channelNumber += 1;
                }
            }
        }

        private async Task SendFileSegmentToClient(EncryptedPeer destination, NetDataReader reader)
        {
            if (!reader.TryGetString(out string uploadID) ||
                !reader.TryGetLong(out long numOfSegment) ||
                !reader.TryGetByte(out byte channel))
            {
                return;
            }

            await SendFileSegmentToClient(destination, channel, uploadID, numOfSegment);
        }

        private async Task SendFileSegmentToClient(EncryptedPeer destination, byte channelNumber, string uploadID, long numberOfSegment)
        {
            if (_uploads.Has(uploadID) &&
                _uploads[uploadID].IsActive &&
                _sharedFiles.HasFileAvailable(_uploads[uploadID].FileHash))
            {
                var file = _sharedFiles.GetByHash(_uploads[uploadID].FileHash);
                if (file == null)
                {
                    return;
                }

                var segment = await file.TryReadSegment(numberOfSegment);
                if (segment.Length > 0)
                {
                    var message = new NetDataWriter();
                    message.Put((byte)NetMessageType.FileSegment);
                    message.Put(uploadID);
                    message.Put(numberOfSegment);
                    message.Put(CRC32.Compute(segment));
                    message.Put(segment.Length);
                    message.Put(segment);
                    message.Put(channelNumber);

                    destination.SendEncrypted(message, channelNumber);
                }
            }
        }

        private async Task ResendFileSegmentToClient(EncryptedPeer destination, NetDataReader reader)
        {
            if (!reader.TryGetString(out string uploadID) ||
                !reader.TryGetString(out string fileHash) ||
                !reader.TryGetLong(out long numberOfSegment) ||
                !reader.TryGetByte(out byte channelNumber))
            {
                return;
            }

            if (_uploads.Has(uploadID) &&
                _uploads[uploadID].IsActive &&
                _sharedFiles.HasFileAvailable(fileHash))
            {
                var file = _sharedFiles.GetByHash(fileHash);
                if (file == null)
                {
                    return;
                }

                var segment = await file.TryReadSegment(numberOfSegment);
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
                    message.Put(channelNumber);

                    destination.SendEncrypted(message, channelNumber);
                    _uploads[uploadID].AddResendedSegment();
                }
            }
        }

        private void SendFileDenial(EncryptedPeer server, string downloadID)
        {
            if (!_downloads.HasDownload(downloadID))
            {
                return;
            }

            _downloads[downloadID].Cancel();

            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.CancelDownload);
            message.Put(downloadID);

            server.SendEncrypted(message, 0);
        }

        private void PrepareForFileReceiving(Download download)
        {
            _downloads.AddDownload(download);

            Notify("New download",
                $"File {download.Name} is now downloading!",
                1500,
                System.Windows.Forms.ToolTipIcon.Info);
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
            if (filesList != null &&
                _availableFiles.HasServer(server.Id))
            {
                _availableFiles[server.Id].UpdateWith(filesList);
            }
        }

        private async Task ReceiveFileSegment(EncryptedPeer server, NetDataReader reader)
        {
            if (!reader.TryGetString(out string downloadID) ||
                !reader.TryGetLong(out long numOfSegment) ||
                !reader.TryGetUInt(out uint receivedCrc) ||
                !reader.TryGetBytesWithLength(out byte[] segment) ||
                !reader.TryGetByte(out byte channel))
            {
                return;
            }

            if (_downloads.HasDownload(downloadID) &&
                _downloads[downloadID].IsActive)
            {
                if (receivedCrc != CRC32.Compute(segment))
                {
                    Debug.WriteLine("(ReceiveFileSegment_Warning) " +
                        $"CRC32's of received file segment of { _downloads[downloadID].Name} don't match!");

                    server.RequestFileSegment(downloadID, _downloads[downloadID].Hash, numOfSegment, channel);
                }
                else
                {
                    if (await _downloads[downloadID].TryWrite(numOfSegment, segment, channel))
                    {
                        server.SendFileSegmentAck(downloadID, numOfSegment, channel);
                    }
                }
            }
        }

        private void ReceiveDownloadCancellation(NetDataReader reader)
        {
            if (!reader.TryGetString(out string downloadID))
            {
                return;
            }

            if (_downloads.HasDownload(downloadID))
            {
                _downloads[downloadID].Cancel();
            }
        }

        private async Task ReceiveAckFromClient(EncryptedPeer client, NetDataReader reader)
        {
            if (!reader.TryGetString(out string uploadID) ||
                !reader.TryGetLong(out long numOfSegment) ||
                !reader.TryGetByte(out byte channel))
            {
                return;
            }

            if (!_uploads.Has(uploadID))
            {
                return;
            }

            if (channel >= Constants.ChannelsCount || channel < 0)
            {
                return;
            }

            _uploads[uploadID].AddAck(numOfSegment);
            await SendFileSegmentToClient(client, channel, uploadID, _uploads[uploadID].NumberOfAckedSegments);
        }


        private void CancelUpload(NetDataReader reader)
        {
            if (!reader.TryGetString(out string uploadID))
            {
                return;
            }

            if (_uploads.Has(uploadID))
            {
                _uploads[uploadID].Cancel();
            }
        }
        #endregion

        #region Methods for enabling and disabling application
        private void StartApp()
        {
            _client.StartListening();

            var isFreePortChosen = false;
            var defaultPort = 55000;
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