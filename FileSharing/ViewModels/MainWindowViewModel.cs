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
            SendFilesListToAllClients();
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
            SendFilesListToAllClients();
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

            if (!reader.TryGetByte(out byte typeByte))
            {
                return;
            }

            if (!typeByte.TryParseType(out NetMessageType type))
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
                {
                    Debug.WriteLine("(Client_ProcessIncomingMessage) " + type);

                    if (reader.TryGetString(out string downloadID) &&
                        reader.TryGetLong(out long numOfSegment) &&
                        reader.TryGetUInt(out uint receivedCrc) &&
                        reader.TryGetBytesWithLength(out byte[] segment) &&
                        reader.TryGetByte(out byte channel))
                    {
                        await ReceiveFileSegment(server, downloadID, numOfSegment, receivedCrc, segment, channel);
                    }
                    else
                    {
                        Debug.WriteLine("(Client_ProcessIncomingMessage) Failed to get " + type);
                    }
                }
                break;

                case NetMessageType.CancelDownload:
                {
                    Debug.WriteLine("(Client_ProcessIncomingMessage) Cancel Download");

                    if (reader.TryGetString(out string downloadID))
                    {
                        ReceiveDownloadCancellation(downloadID);
                    }
                    else
                    {
                        Debug.WriteLine("(Client_ProcessIncomingMessage) Failed to get Cancel Download");
                    }
                }
                break;

                case NetMessageType.None:
                    Debug.WriteLine("(Client_ProcessIncomingMessage_Error) Unknown type");
                    break;
            }
        }

        private void OnServerMessageReceived(object? sender, NetEventArgs e)
        {
            var reader = e.Message;
            var client = e.CryptoPeer;

            if (!reader.TryGetByte(out byte typeByte))
            {
                return;
            }

            if (!typeByte.TryParseType(out NetMessageType type))
            {
                return;
            }

            switch (type)
            {
                case NetMessageType.FilesListRequest:
                {
                    Debug.WriteLine("(Server_ProcessIncomingMessage) Files List Request");

                    SendFilesList(client);
                }
                break;

                case NetMessageType.FileRequest:
                {
                    Debug.WriteLine("(Server_ProcessIncomingMessage) File Request");

                    if (reader.TryGetString(out string fileHash) &&
                        reader.TryGetString(out string uploadID))
                    {
                        StartSendingFileToClient(client, fileHash, uploadID);
                    }
                    else
                    {
                        Debug.WriteLine("(Client_ProcessIncomingMessage) Failed to get File Request");
                    }
                }
                break;

                case NetMessageType.FileSegment:
                {
                    Debug.WriteLine("(Server_ProcessIncomingMessage) File Segment");

                    if (reader.TryGetString(out string uploadID) &&
                        reader.TryGetLong(out long numOfSegment) &&
                        reader.TryGetByte(out byte channel))
                    {
                        SendFileSegmentToClient(client, channel, uploadID, numOfSegment);
                    }
                    else
                    {
                        Debug.WriteLine("(Client_ProcessIncomingMessage) Failed to get File Segment");
                    }
                }
                break;

                case NetMessageType.ResendFileSegment:
                {
                    Debug.WriteLine("(Server_ProcessIncomingMessage) Resend File Segment");

                    if (reader.TryGetString(out string uploadID) &&
                        reader.TryGetString(out string fileHash) &&
                        reader.TryGetLong(out long numOfSegment) &&
                        reader.TryGetByte(out byte channel))
                    {
                        ResendFileSegmentToClient(client, channel, uploadID, fileHash, numOfSegment);
                    }
                    else
                    {
                        Debug.WriteLine("(Client_ProcessIncomingMessage) Failed to get File Segment");
                    }
                }
                break;

                case NetMessageType.FileSegmentAck:
                {
                    Debug.WriteLine("(Server_ProcessIncomingMessage) File Segment Ack");

                    if (reader.TryGetString(out string uploadID) &&
                        reader.TryGetLong(out long numOfSegment) &&
                        reader.TryGetByte(out byte channel))
                    {
                        ReceiveAckFromClient(client, uploadID, numOfSegment, channel);
                    }
                    else
                    {
                        Debug.WriteLine("(Server_ProcessIncomingMessage) Failed to receive File Segment Ack");
                    }
                }
                break;

                case NetMessageType.CancelDownload:
                {
                    Debug.WriteLine("(Server_ProcessIncomingMessage) Cancel Download");

                    if (reader.TryGetString(out string uploadID))
                    {
                        CancelUpload(uploadID);
                    }
                    else
                    {
                        Debug.WriteLine("(Server_ProcessIncomingMessage) Failed to receive Cancel Download");
                    }
                }
                break;

                case NetMessageType.None:
                    Debug.WriteLine("(Server_ProcessIncomingMessage_Error) Unknown type");
                    break;
            }
        }
        #endregion

        #region Commands implementations
        private void ConnectToServer()
        {
            var addressDialog = new InputBoxUtils();
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
                MessageBox.Show("File " + file.Name + " is unreachable: unknown file server",
                    "Download error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            if (!_client.IsConnectedToServer(file.Server.Id, out EncryptedPeer? server) ||
                server == null)
            {
                MessageBox.Show("File '" + file.Name + "' is unreachable: no connection to file server",
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
                Filter = fileExtension.Length > 0 ?
                    string.Format("{1} files (*{0})|*{0}|All files (*.*)|*.*", fileExtension, fileExtension.Remove(0, 1).ToUpper()) :
                    "All files (*.*)|*.*"
            };

            var dialogResult = saveFileDialog.ShowDialog();
            if (!dialogResult.HasValue ||
                !dialogResult.Value)
            {
                return;
            }

            Debug.WriteLine("(DownloadFile) Path: " + saveFileDialog.FileName);

            var newDownload = new Download(file, server, saveFileDialog.FileName);
            if (_downloads.HasDownloadWithSamePath(newDownload.FilePath, out string downloadID))
            {
                var confirmDownloadRestart = MessageBox.Show("File '" + file.Name + "' is already downloading! Do you want to restart download of this file?",
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
                var confirmDownloadRepeat = MessageBox.Show("File '" + file.Name + "' already exists. Do you want to download this file again?",
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
                MessageBox.Show("File '" + download.Name + "' is already downloaded.",
                    "Cancel download notification",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (download.IsCancelled)
            {
                MessageBox.Show("Download of file '" + download.Name + "' is already cancelled!",
                    "Cancel download notification",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            var confirmDownloadCancellation = MessageBox.Show("Do you want to cancel the download of this file: '" + download.Name + "'?",
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

            var confirmDisconnect = MessageBox.Show("Do you want to disconnect from server " + server.EndPoint + "?",
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

            var messageBoxResult = MessageBox.Show("Do you want to stop sharing this file: '" + sharedFile.Name + "'? Users' current downloads will be stopped.",
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
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All files(*.*)|*.*";
            openFileDialog.Title = "Select file to share";
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
            var text = port == 0 ? "Local file server is not listening yet" : "Local file server port: " + port;
            MessageBox.Show(text, 
                "Local port info", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
        }
        #endregion

        #region Methods for information exchange between clients and servers
        private void SendFilesListToAllClients()
        {
            var filesList = _sharedFiles.GetAvailableFiles();
            var filesListJson = JsonConvert.SerializeObject(filesList);
            var crc = CRC32.Compute(filesListJson);

            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.FilesList);
            message.Put(crc);
            message.Put(filesListJson);

            _server.SendToAll(message);
        }

        private void SendFilesList(EncryptedPeer destination)
        {
            var filesList = _sharedFiles.GetAvailableFiles();
            var filesListJson = JsonConvert.SerializeObject(filesList);
            var crc = CRC32.Compute(filesListJson);

            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.FilesList);
            message.Put(crc);
            message.Put(filesListJson);

            destination.SendEncrypted(message, 0);
        }

        private void StartSendingFileToClient(EncryptedPeer destination, string fileHash, string uploadID)
        {
            if (_sharedFiles.HasFileAvailable(fileHash) &&
                !_uploads.Has(uploadID))
            {
                var desiredFile = _sharedFiles.GetByHash(fileHash);
                if (desiredFile == null)
                {
                    return;
                }

                var upload = new Upload(
                    uploadID,
                    desiredFile.Name,
                    desiredFile.Size,
                    desiredFile.Hash,
                    destination,
                    desiredFile.NumberOfSegments);

                _uploads.Add(upload);

                Debug.WriteLine("(FileSendRoutine) Upload " + uploadID + " has started!");

                byte channelNumber = 0;
                var initialSegmentsCount = upload.NumberOfSegments < Constants.ChannelsCount ? upload.NumberOfSegments : Constants.ChannelsCount;

                Debug.WriteLine("(InitialSegmentsCount) " + initialSegmentsCount);

                for (long numberOfSegment = 0; numberOfSegment < initialSegmentsCount; numberOfSegment++)
                {
                    SendFileSegmentToClient(destination, channelNumber, upload.ID, numberOfSegment);
                    channelNumber += 1;
                }
            }
        }

        private void SendFileSegmentToClient(EncryptedPeer destination, byte channelNumber, string uploadID, long numberOfSegment)
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

                if (file.TryReadSegment(numberOfSegment, out byte[] segment))
                {
                    var message = new NetDataWriter();
                    message.Put((byte)NetMessageType.FileSegment);
                    message.Put(uploadID);
                    message.Put(numberOfSegment);
                    var crc = CRC32.Compute(segment);
                    message.Put(crc);
                    message.Put(segment.Length);
                    message.Put(segment);
                    message.Put(channelNumber);

                    destination.SendEncrypted(message, channelNumber);
                }
            }
        }

        private void ResendFileSegmentToClient(EncryptedPeer destination, byte channelNumber, string uploadID, string fileHash, long numberOfSegment)
        {
            if (_uploads.Has(uploadID) &&
                _uploads[uploadID].IsActive &&
                _sharedFiles.HasFileAvailable(fileHash))
            {
                var file = _sharedFiles.GetByHash(fileHash);
                if (file == null)
                {
                    return;
                }

                if (file.TryReadSegment(numberOfSegment, out byte[] segment))
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
                }
            }

            if (_uploads.Has(uploadID))
            {
                _uploads[uploadID].AddResendedSegment();
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

            var calculatedCrc = CRC32.Compute(jsonFilesList);
            if (receivedCrc != calculatedCrc)
            {
                Debug.WriteLine("(ProcessIncomingMessage_Warning) CRC32 of received files list does not match: {0} != {1}", receivedCrc, calculatedCrc);

                server.SendFilesListRequest();

                return;
            }

            var filesList = JsonConvert.DeserializeObject<List<SharedFileInfo>>(jsonFilesList);
            if (filesList != null &&
                _availableFiles.HasServer(server.Id))
            {
                _availableFiles[server.Id].UpdateWith(filesList);
            }
        }

        private async Task ReceiveFileSegment(EncryptedPeer server, string downloadID, long numOfSegment, uint receivedCrc, byte[] segment, byte channel)
        {
            if (_downloads.HasDownload(downloadID) &&
                _downloads[downloadID].IsActive)
            {
                var calculatedCrc = CRC32.Compute(segment);

                if (receivedCrc != calculatedCrc)
                {
                    Debug.WriteLine("(ProcessIncomingMessage_Warning) CRC32 of received file segment of {0} does not match: {1} != {2}",
                        _downloads[downloadID].Name,
                        receivedCrc,
                        calculatedCrc);

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

        private void ReceiveDownloadCancellation(string downloadID)
        {
            if (_downloads.HasDownload(downloadID))
            {
                _downloads[downloadID].Cancel();
            }
        }

        private void ReceiveAckFromClient(EncryptedPeer client, string uploadID, long numOfSegment, byte channel)
        {
            if (!_uploads.Has(uploadID))
            {
                return;
            }

            if (channel >= Constants.ChannelsCount || channel < 0)
            {
                return;
            }

            _uploads[uploadID].AddAck(numOfSegment);
            SendFileSegmentToClient(client, channel, uploadID, _uploads[uploadID].NumberOfAckedSegments);
        }


        private void CancelUpload(string uploadID)
        {
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
            var portNumberDialog = new InputBoxUtils();
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
                        MessageBox.Show("Port " + portNumber + " is already occupied! Try another port", 
                            "Local server port error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    //take default port number if it's not occupied
                    //otherwise don't start application at all
                    if (!defaultPort.IsPortOccupied())
                    {
                        isFreePortChosen = true;
                        port = defaultPort;
                    }
                    else
                    {
                        MessageBox.Show("Default port is already occupied, appliction will be shut down!",
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