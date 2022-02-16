using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Win32;
using SystemTrayApp.WPF;
using Newtonsoft.Json;
using FileSharing.Models;
using FileSharing.Networking;
using FileSharing.Utils;
using InputBox;
using DropFiles;

namespace FileSharing.ViewModels
{
    public class MainWindowViewModel : ObservableRecipient, IFilesDropped
    {
        private NotifyIconWrapper.NotifyRequestRecord? _notifyRequest;
        private bool _showInTaskbar;
        private WindowState _windowState;

        private readonly Client _client;
        private readonly FilesFromServers _availableFiles;
        private readonly Downloads _downloads;

        private readonly Server _server;
        private readonly SharedFiles _sharedFiles;
        private readonly Uploads _uploads;

        public MainWindowViewModel()
        {
            LoadedCommand = new RelayCommand(Loaded);
            ClosingCommand = new RelayCommand<CancelEventArgs>(Closing);
            NotifyCommand = new RelayCommand(() => Notify("Hello world!"));
            NotifyIconOpenCommand = new RelayCommand(() => { WindowState = WindowState.Normal; });
            NotifyIconExitCommand = new RelayCommand(ShutdownApp);

            //client-side structures
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

            //client-side commands
            ConnectToServerCommand = new RelayCommand(ConnectToServer);
            DownloadFileCommand = new RelayCommand<SharedFileInfo>(DownloadFile);
            CancelDownloadCommand = new RelayCommand<Download>(CancelDownload);
            OpenFileInFolderCommand = new RelayCommand<Download>(OpenFileInFolder);
            DisconnectFromServerCommand = new RelayCommand<EncryptedPeer>(DisconnectFromServer);
            VerifyFileHashCommand = new RelayCommand<Download>(VerifyFileHash);

            //server-side structures
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

            //server-side commands
            RemoveSharedFileCommand = new RelayCommand<SharedFile>(RemoveSharedFile);
            AddFileCommand = new RelayCommand(AddFile);
            ShowLocalPortCommand = new RelayCommand(ShowLocalPort);
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

            Debug.WriteLine("(Closing)");
        }
        #endregion

        #region View-model bindings
        public ICommand ConnectToServerCommand { get; }
        public ICommand DownloadFileCommand { get; }
        public ICommand CancelDownloadCommand { get; }
        public ICommand OpenFileInFolderCommand { get; }
        public ICommand DisconnectFromServerCommand { get; }
        public ICommand RemoveSharedFileCommand { get; }
        public ICommand AddFileCommand { get; }
        public ICommand ShowLocalPortCommand { get; }
        public ICommand VerifyFileHashCommand { get; }
        public ObservableCollection<SharedFileInfo> AvailableFiles => new ObservableCollection<SharedFileInfo>(_availableFiles.List);
        public ObservableCollection<Download> Downloads => new ObservableCollection<Download>(_downloads.DownloadsList);
        public ObservableCollection<EncryptedPeer> Servers => new ObservableCollection<EncryptedPeer>(_client.Servers);
        public ObservableCollection<SharedFile> SharedFiles => new ObservableCollection<SharedFile>(_sharedFiles.SharedFilesList);
        public ObservableCollection<Upload> Uploads => new ObservableCollection<Upload>(_uploads.UploadsList);
        public ObservableCollection<EncryptedPeer> Clients => new ObservableCollection<EncryptedPeer>(_server.Clients);
        public SharedFileInfo? SelectedAvailableFile { get; set; }
        public Download? SelectedDownload { get; set; }
        public EncryptedPeer? SelectedServer { get; set; }
        public SharedFile? SelectedSharedFile { get; set; }
        #endregion

        #region Client & Server event handlers
        private void OnServerAdded(object? sender, EncryptedPeerEventArgs e)
        {
            var server = _client.GetServerByID(e.PeerID);
            if (server != null)
            {
                Debug.WriteLine("(OnServerAdded) Adding files list of server " + server.Peer.EndPoint);

                _availableFiles.AddServer(server);
                OnPropertyChanged(nameof(Servers));
            }
        }

        private void OnServerConnected(object? sender, EncryptedPeerEventArgs e)
        {
            var server = _client.GetServerByID(e.PeerID);
            if (server != null)
            {
                Debug.WriteLine("(OnServerConnected) Requesting files list from server " + server.Peer.EndPoint);

                SendFilesListRequest(server);
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

        private void OnMissingFileSegmentsRequested(object? sender, MissingSegmentsEventArgs e)
        {
            RequestMissingFileSegments(e);
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

        private void OnClientMessageReceived(object? sender, NetEventArgs e)
        {
            var reader = e.Message;
            var server = e.CryptoPeer;

            byte typeByte = 0;
            if (!reader.TryGetByte(out typeByte))
            {
                return;
            }

            var type = NetMessageType.None;
            if (!TryParseType(typeByte, out type))
            {
                return;
            }

            switch (type)
            {
                case NetMessageType.FilesList:
                {
                    Debug.WriteLine("(Client_ProcessIncomingMessage) Files List");

                    if (reader.TryGetUInt(out uint receivedCrc) &&
                        reader.TryGetString(out string jsonFilesList))
                    {
                        ReceiveFilesList(server, receivedCrc, jsonFilesList);
                    }
                    else
                    {
                        Debug.WriteLine("(Client_ProcessIncomingMessage) Failed to get Files List");
                    }
                }
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
                        ReceiveFileSegment(server, downloadID, numOfSegment, receivedCrc, segment, channel);
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
                        ReceiveDownloadCancellation(server, downloadID);
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

            byte typeByte = 0;
            if (!reader.TryGetByte(out typeByte))
            {
                return;
            }

            var type = NetMessageType.None;
            if (!TryParseType(typeByte, out type))
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

        private bool TryParseType(byte typeByte, out NetMessageType type)
        {
            if (Enum.TryParse(typeByte + "", out NetMessageType messageType))
            {
                type = messageType;

                return true;
            }
            else
            {
                type = NetMessageType.None;

                return false;
            }
        }
        #endregion

        #region Commands implementations
        private void ConnectToServer()
        {
            var addressDialog = new InputBoxUtils();
            if (addressDialog.AskServerAddressAndPort(out IPEndPoint? address) &&
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

            if (!_client.IsConnectedToServer(file.Server.Peer.Id, out EncryptedPeer? server) ||
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
            if (_downloads.HasDownloadWithSamePath(newDownload.Path, out string downloadID))
            {
                var confirmDownloadRestart = MessageBox.Show("File '" + file.Name + "' is already downloading! Do you want to restart download of this file?",
                    "Restart Download Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Exclamation);

                if (confirmDownloadRestart == MessageBoxResult.Yes)
                {
                    SendFileDenial(server, downloadID);
                    PrepareForFileReceiving(newDownload);
                    SendFileRequest(server, newDownload);
                }
            }
            else
            if (File.Exists(newDownload.Path))
            {
                var confirmDownloadRepeat = MessageBox.Show("File '" + file.Name + "' already exists. Do you want to download this file again?",
                    "Download Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Exclamation);

                if (confirmDownloadRepeat == MessageBoxResult.Yes)
                {
                    PrepareForFileReceiving(newDownload);
                    SendFileRequest(server, newDownload);
                }
            }
            else
            {
                PrepareForFileReceiving(newDownload);
                SendFileRequest(server, newDownload);
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
                if (_client.IsConnectedToServer(download.Server.Peer.Id, out EncryptedPeer? server) &&
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

            if (!File.Exists(download.Path))
            {
                MessageBox.Show("File '" + download.Name + "' was removed or deleted.",
                    "File not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            string argument = "/select, \"" + download.Path + "\"";
            Process.Start("explorer.exe", argument);
        }

        private void DisconnectFromServer(EncryptedPeer? server)
        {
            if (server == null)
            {
                return;
            }

            var confirmDisconnect = MessageBox.Show("Do you want to disconnect from server " + server.Peer.EndPoint + "?",
                "Disconnect Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmDisconnect == MessageBoxResult.Yes)
            {
                _client.DisconnectFromServer(server);
                _availableFiles.RemoveServer(server.Peer.Id);
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
                        SendUploadDenial(upload.Destination, upload.ID);
                    }
                }
            }
        }

        private void AddFile()
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All files(*.*)|*.*";
            openFileDialog.Title = "Select file to share";
            if (openFileDialog.ShowDialog() == true)
            {
                _sharedFiles.AddFile(openFileDialog.FileName);
            }
        }

        public void OnFilesDropped(string[] files)
        {
            foreach (var file in files)
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
            var text = port == 0 ? "Local file server is not listening yet" : "Local file server port: " + port;
            MessageBox.Show(text, 
                "Local port info", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
        }

        private void VerifyFileHash(Download? download)
        {
            if (download == null)
            {
                return;
            }

            if (!download.IsDownloaded)
            {
                MessageBox.Show("Unable to verify hash of file '" + download.Name + "' because it is not downloaded yet!",
                    "Hash verification error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            if (!File.Exists(download.Path))
            {
                MessageBox.Show("Unable to verify hash of file '" + download.Name + "' because it was removed or deleted!",
                    "File not found error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                download.ProhibitHashVerification();

                return;
            }

            if (download.IsHashVerificationStarted ||
                download.IsHashVerificationFailed)
            {
                return;
            }

            if (download.IsHashVerificationResultNegative ||
                download.IsHashVerificationResultPositive)
            {
                return;
            }

            download.VerifyHash();
        }
        #endregion

        #region Methods for information exchange between clients and servers
        private void SendFilesListToAllClients()
        {
            var filesList = _sharedFiles.GetAvailableFiles();
            var filesListJson = JsonConvert.SerializeObject(filesList);
            var crc = CRC32.Compute(filesListJson);

            var message = new SimpleWriter();
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

            var message = new SimpleWriter();
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

                if (file.TryReadSegment(numberOfSegment, out byte[] segment))
                {
                    var message = new SimpleWriter();
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

                if (file.TryReadSegment(numberOfSegment, out byte[] segment))
                {
                    var message = new SimpleWriter();
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

        private void SendUploadDenial(EncryptedPeer destination, string uploadID)
        {
            var message = new SimpleWriter();
            message.Put((byte)NetMessageType.CancelDownload);
            message.Put(uploadID);

            destination.SendEncrypted(message, 0);
        }

        private void SendFilesListRequest(EncryptedPeer server)
        {
            var message = new SimpleWriter();
            message.Put((byte)NetMessageType.FilesListRequest);

            server.SendEncrypted(message, 0);
        }

        private void SendFileSegmentAck(EncryptedPeer server, string downloadID, long numOfSegment, byte channel)
        {
            var message = new SimpleWriter();
            message.Put((byte)NetMessageType.FileSegmentAck);
            message.Put(downloadID);
            message.Put(numOfSegment);
            message.Put(channel);

            server.SendEncrypted(message, channel);
        }

        private void RequestFileSegment(EncryptedPeer server, string downloadID, string fileHash, long numOfSegment, byte channel)
        {
            Debug.WriteLine("(RequestFileSegment) " + fileHash + ", segment no. " + numOfSegment);

            var message = new SimpleWriter();
            message.Put((byte)NetMessageType.ResendFileSegment);
            message.Put(downloadID);
            message.Put(fileHash);
            message.Put(numOfSegment);
            message.Put(channel);

            server.SendEncrypted(message, channel);
        }

        private void SendFileDenial(EncryptedPeer server, string downloadID)
        {
            if (!_downloads.HasDownload(downloadID))
            {
                return;
            }

            _downloads[downloadID].Cancel();

            var message = new SimpleWriter();
            message.Put((byte)NetMessageType.CancelDownload);
            message.Put(downloadID);

            server.SendEncrypted(message, 0);
        }

        private void SendFileRequest(EncryptedPeer server, Download download)
        {
            var message = new SimpleWriter();
            message.Put((byte)NetMessageType.FileRequest);
            message.Put(download.Hash);
            message.Put(download.ID);

            server.SendEncrypted(message, 0);
        }

        private void PrepareForFileReceiving(Download download)
        {
            _downloads.AddDownload(download);
        }

        private void ReceiveFilesList(EncryptedPeer server, uint receivedCrc, string jsonFilesList)
        {
            var calculatedCrc = CRC32.Compute(jsonFilesList);
            if (receivedCrc != calculatedCrc)
            {
                Debug.WriteLine("(ProcessIncomingMessage_Warning) CRC32 of received files list does not match: {0} != {1}", receivedCrc, calculatedCrc);

                SendFilesListRequest(server);

                return;
            }

            Debug.WriteLine(jsonFilesList);

            var filesList = JsonConvert.DeserializeObject<List<SharedFileInfo>>(jsonFilesList);
            if (filesList != null &&
                _availableFiles.HasServer(server.Peer.Id))
            {
                _availableFiles[server.Peer.Id].UpdateWith(filesList);
            }
        }

        private void ReceiveFileSegment(EncryptedPeer server, string downloadID, long numOfSegment, uint receivedCrc, byte[] segment, byte channel)
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

                    RequestFileSegment(server, downloadID, _downloads[downloadID].Hash, numOfSegment, channel);
                }
                else
                {
                    if (_downloads[downloadID].TryWrite(numOfSegment, segment, channel))
                    {
                        SendFileSegmentAck(server, downloadID, numOfSegment, channel);
                    }
                }
            }
        }

        private void ReceiveDownloadCancellation(EncryptedPeer server, string downloadID)
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

        private void RequestMissingFileSegments(MissingSegmentsEventArgs args)
        {
            var task = new Task(() => RequestMissingFileSegmentsTask(args));
            task.Start();
        }

        private void RequestMissingFileSegmentsTask(MissingSegmentsEventArgs args)
        {
            for (int i = 0; i < args.NumbersOfMissingSegments.Count; i++)
            {
                RequestFileSegment(args.Server, args.DownloadID, args.FileHash, args.NumbersOfMissingSegments[i], Convert.ToByte(i % Constants.ChannelsCount));

                if (i % 4 == 0)
                {
                    Thread.Sleep(20);
                }
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
                if (portNumberDialog.AskPort(out int portNumber))
                {
                    if (!IsPortOccupied(portNumber))
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
                    if (!IsPortOccupied(defaultPort))
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

        private bool IsPortOccupied(int port)
        {
            return IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Any(p => p.Port == port);
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
}