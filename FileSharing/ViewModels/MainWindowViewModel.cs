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
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.Win32;
using SystemTrayApp.WPF;
using Newtonsoft.Json;
using FileSharing.Models;
using FileSharing.Networking;
using FileSharing.Utils;
using FileSharing.InputBox;

namespace FileSharing.ViewModels
{
    public class MainWindowViewModel : ObservableRecipient
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

            //client-side commands
            ConnectToServerCommand = new RelayCommand(ConnectToServer);
            DownloadFileCommand = new RelayCommand<FileInfo>(DownloadFile);
            CancelDownloadCommand = new RelayCommand<Download>(CancelDownload);
            OpenFileInFolderCommand = new RelayCommand<Download>(OpenFileInFolder);
            DisconnectFromServerCommand = new RelayCommand<CryptoPeer>(DisconnectFromServer);

            //server-side structures
            _server = new Server();
            _server.ClientAdded += OnClientAdded;
            _server.ClientRemoved += OnClientRemoved;
            _server.MessageReceived += OnServerMessageReceived;
            _sharedFiles = new SharedFiles();
            _sharedFiles.SharedFileAdded += OnSharedFileAdded;
            _sharedFiles.SharedFileHashCalculated += OnSharedFileHashCalculated;
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
        public ObservableCollection<FileInfo> AvailableFiles => new ObservableCollection<FileInfo>(_availableFiles.List);
        public ObservableCollection<Download> Downloads => new ObservableCollection<Download>(_downloads.DownloadsList);
        public ObservableCollection<CryptoPeer> Servers => new ObservableCollection<CryptoPeer>(_client.Servers);
        public ObservableCollection<SharedFile> SharedFiles => new ObservableCollection<SharedFile>(_sharedFiles.SharedFilesList);
        public ObservableCollection<Upload> Uploads => new ObservableCollection<Upload>(_uploads.UploadsList);
        public ObservableCollection<CryptoPeer> Clients => new ObservableCollection<CryptoPeer>(_server.Clients);
        public FileInfo? SelectedAvailableFile { get; set; }
        public Download? SelectedDownload { get; set; }
        public CryptoPeer? SelectedServer { get; set; }
        public SharedFile? SelectedSharedFile { get; set; }
        #endregion

        #region Client & Server event handlers
        private void OnServerAdded(object? sender, CryptoPeerEventArgs e)
        {
            var server = _client.GetServerByID(e.PeerID);
            if (server != null)
            {
                Debug.WriteLine("Adding files list of server " + server.Peer.EndPoint);

                _availableFiles.AddServer(server.Peer);
                OnPropertyChanged(nameof(Servers));
            }
        }

        private void OnServerConnected(object? sender, CryptoPeerEventArgs e)
        {
            var server = _client.GetServerByID(e.PeerID);
            if (server != null)
            {
                Debug.WriteLine("Requesting files list from server " + server.Peer.EndPoint);

                SendFilesListRequest(server);
            }
        }

        private void OnServerRemoved(object? sender, CryptoPeerEventArgs e)
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

        private void OnClientAdded(object? sender, CryptoPeerEventArgs e)
        {
            OnPropertyChanged(nameof(Clients));
        }

        private void OnClientRemoved(object? sender, CryptoPeerEventArgs e)
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

            if (!TryParseType(reader.GetByte(), out NetMessageType type))
            {
                return;
            }

            switch (type)
            {
                case NetMessageType.FilesList:
                {
                    Debug.WriteLine("(Client_ProcessIncomingMessage) Files List");

                    var receivedCrc = reader.GetUInt();
                    var jsonFilesList = reader.GetString();

                    ReceiveFilesList(server, receivedCrc, jsonFilesList);
                }
                break;

                case NetMessageType.FileSegment:
                {
                    Debug.WriteLine("(Client_ProcessIncomingMessage) File Segment");

                    var downloadID = reader.GetString(20);
                    var numOfSegment = reader.GetLong();
                    var receivedCrc = reader.GetUInt();
                    var segment = new byte[reader.GetInt()];
                    reader.GetBytes(segment, segment.Length);

                    ReceiveFileSegment(server, downloadID, numOfSegment, receivedCrc, segment);
                }
                break;

                case NetMessageType.CancelDownload:
                {
                    Debug.WriteLine("(Client_ProcessIncomingMessage) Cancel Download");

                    var downloadID = reader.GetString(20);

                    ReceiveDownloadCancellation(server, downloadID);
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

            if (!TryParseType(reader.GetByte(), out NetMessageType type))
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

                    var fileHash = reader.GetString(64);
                    var uploadID = reader.GetString(20);

                    StartSendingFileToClient(client, fileHash, uploadID);
                }
                break;

                case NetMessageType.FileSegment:
                {
                    Debug.WriteLine("(Server_ProcessIncomingMessage) File Segment");

                    var uploadID = reader.GetString(20);
                    var numOfSegment = reader.GetLong();

                    SendFileSegmentToClient(client, uploadID, numOfSegment);
                }
                break;

                case NetMessageType.FileSegmentAck:
                {
                    Debug.WriteLine("(Server_ProcessIncomingMessage) File Segment Ack");

                    var uploadID = reader.GetString(20);

                    ReceiveAckFromClient(client, uploadID);
                }
                break;

                case NetMessageType.CancelDownload:
                {
                    Debug.WriteLine("(Server_ProcessIncomingMessage) Cancel Download");

                    var uploadID = reader.GetString(20);

                    CancelUpload(uploadID);
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

        private void DownloadFile(FileInfo? file)
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

            if (!_client.IsConnectedToServer(file.Server.Id, out CryptoPeer? server) ||
                server == null)
            {
                MessageBox.Show("File '" + file.Name + "' is unreachable: no connection to file server",
                    "Download error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            var confirmFileDownload = MessageBox.Show("Do you want to download this file: '" + file.Name + "'?",
                "Download Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmFileDownload != MessageBoxResult.Yes)
            {
                return;
            }

            var folderPicker = new CommonOpenFileDialog();
            folderPicker.IsFolderPicker = true;
            if (folderPicker.ShowDialog() != CommonFileDialogResult.Ok)
            {
                return;
            }

            var folder = folderPicker.FileName;
            var newDownload = new Download(file.Name, file.Size, file.Hash, file.Server, folder);

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
            if (System.IO.File.Exists(newDownload.Path))
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
                if (_client.IsConnectedToServer(download.Server.Id, out CryptoPeer? server) &&
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

            if (!System.IO.File.Exists(download.Path))
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

        private void DisconnectFromServer(CryptoPeer? server)
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

            var message = new SimpleWriter();
            message.Put((byte)NetMessageType.FilesList);
            message.Put(crc);
            message.Put(filesListJson);

            _server.SendToAll(message);
        }

        private void SendFilesList(CryptoPeer destination)
        {
            var filesList = _sharedFiles.GetAvailableFiles();
            var filesListJson = JsonConvert.SerializeObject(filesList);
            var crc = CRC32.Compute(filesListJson);

            var message = new SimpleWriter();
            message.Put((byte)NetMessageType.FilesList);
            message.Put(crc);
            message.Put(filesListJson);

            destination.SendEncrypted(message);
        }

        private void StartSendingFileToClient(CryptoPeer destination, string fileHash, string uploadID)
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

                SendFileSegmentToClient(destination, upload.ID, upload.NumberOfAckedSegments);

                Debug.WriteLine("(FileSendRoutine) Upload " + uploadID + " has started!");
            }
        }

        private void SendFileSegmentToClient(CryptoPeer destination, string uploadID, long numberOfSegment)
        {
            if (_uploads.Has(uploadID) &&
                !_uploads[uploadID].IsFinished &&
                !_uploads[uploadID].IsCancelled &&
                _sharedFiles.HasFileAvailable(_uploads[uploadID].FileHash))
            {
                var file = _sharedFiles.GetByHash(_uploads[uploadID].FileHash);

                if (file.TryReadSegment(numberOfSegment, uploadID, out SimpleWriter message))
                {
                    destination.SendEncrypted(message);
                }
            }
        }

        private void SendUploadDenial(CryptoPeer destination, string uploadID)
        {
            var message = new SimpleWriter();
            message.Put((byte)NetMessageType.CancelDownload);
            message.Put(uploadID);

            destination.SendEncrypted(message);
        }

        private void SendFilesListRequest(CryptoPeer server)
        {
            var message = new SimpleWriter();
            message.Put((byte)NetMessageType.FilesListRequest);

            server.SendEncrypted(message);
        }

        private void SendFileSegmentAck(CryptoPeer server, string downloadID)
        {
            var message = new SimpleWriter();
            message.Put((byte)NetMessageType.FileSegmentAck);
            message.Put(downloadID);

            server.SendEncrypted(message);
        }

        private void RequestFileSegment(CryptoPeer server, string downloadID, long numOfSegment)
        {
            var message = new SimpleWriter();
            message.Put((byte)NetMessageType.FileSegment);
            message.Put(downloadID);
            message.Put(numOfSegment);

            server.SendEncrypted(message);
        }

        private void SendFileDenial(CryptoPeer server, string downloadID)
        {
            if (!_downloads.HasDownload(downloadID))
            {
                return;
            }

            _downloads[downloadID].Cancel();

            var message = new SimpleWriter();
            message.Put((byte)NetMessageType.CancelDownload);
            message.Put(downloadID);

            server.SendEncrypted(message);
        }

        private void SendFileRequest(CryptoPeer server, Download download)
        {
            var message = new SimpleWriter();
            message.Put((byte)NetMessageType.FileRequest);
            message.Put(download.Hash);
            message.Put(download.ID);

            server.SendEncrypted(message);
        }

        private void PrepareForFileReceiving(Download download)
        {
            _downloads.AddDownload(download);
        }

        private void ReceiveFilesList(CryptoPeer server, uint receivedCrc, string jsonFilesList)
        {
            var calculatedCrc = CRC32.Compute(jsonFilesList);
            if (receivedCrc != calculatedCrc)
            {
                Debug.WriteLine("(ProcessIncomingMessage_Warning) CRC32 of received files list does not match: {0} != {1}", receivedCrc, calculatedCrc);

                SendFilesListRequest(server);

                return;
            }

            Debug.WriteLine(jsonFilesList);

            var filesList = JsonConvert.DeserializeObject<List<FileInfo>>(jsonFilesList);
            if (filesList != null &&
                _availableFiles.HasServer(server.Peer.Id))
            {
                Debug.WriteLine("Start updating files list");

                _availableFiles[server.Peer.Id].UpdateWith(filesList);
            }
        }

        private void ReceiveFileSegment(CryptoPeer server, string downloadID, long numOfSegment, uint receivedCrc, byte[] segment)
        {
            if (_downloads.HasDownload(downloadID) &&
                !_downloads[downloadID].IsCancelled &&
                !_downloads[downloadID].IsDownloaded &&
                !_downloads[downloadID].IsCorrupted)
            {
                var calculatedCrc = CRC32.Compute(segment);

                if (receivedCrc != calculatedCrc)
                {
                    Debug.WriteLine("(ProcessIncomingMessage_Warning) CRC32 of received file segment of {0} does not match: {1} != {2}", _downloads[downloadID].Name, receivedCrc, calculatedCrc);

                    RequestFileSegment(server, downloadID, numOfSegment);
                }
                else
                {
                    if (_downloads[downloadID].TryWrite(numOfSegment, segment))
                    {
                        SendFileSegmentAck(server, downloadID);
                    }
                }
            }
        }

        private void ReceiveDownloadCancellation(CryptoPeer server, string downloadID)
        {
            if (_downloads.HasDownload(downloadID))
            {
                _downloads[downloadID].Cancel();
            }
        }

        private void ReceiveAckFromClient(CryptoPeer client, string uploadID)
        {
            if (!_uploads.Has(uploadID))
            {
                return;
            }

            _uploads[uploadID].AddAck();
            SendFileSegmentToClient(client, uploadID, _uploads[uploadID].NumberOfAckedSegments);
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
                "Download Confirmation",
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
