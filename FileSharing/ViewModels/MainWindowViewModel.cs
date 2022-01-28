using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Win32;
using SystemTrayApp.WPF;
using FileSharing.Models;
using FileSharing.Networking;
using FileSharing.Utils;
using Newtonsoft.Json;

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
            _server = new Server(55000);
            _server.ClientAdded += OnClientAdded;
            _server.ClientRemoved += OnClientRemoved;
            _server.MessageReceived += OnServerMessageReceived;
            _sharedFiles = new SharedFiles();
            _sharedFiles.SharedFileAdded += OnSharedFileAdded;
            _sharedFiles.SharedFileChanged += OnSharedFileChanged;
            _sharedFiles.SharedFileRemoved += OnSharedFileRemoved;
            _uploads = new Uploads();
            _uploads.UploadAdded += OnUploadAdded;
            _uploads.UploadRemoved += OnUploadRemoved;

            //server-side commands
            RemoveSharedFileCommand = new RelayCommand<SharedFile>(RemoveSharedFile);
            AddFileCommand = new RelayCommand(AddFile);
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

        public ICommand ConnectToServerCommand { get; }
        public ICommand DownloadFileCommand { get; }
        public ICommand CancelDownloadCommand { get; }
        public ICommand OpenFileInFolderCommand { get; }
        public ICommand DisconnectFromServerCommand { get; }
        public ICommand RemoveSharedFileCommand { get; }
        public ICommand AddFileCommand { get; }
        public ObservableCollection<FileInfo> AvailableFiles => _availableFiles.AvailableFiles;
        public ObservableCollection<Download> Downloads => new ObservableCollection<Download>(_downloads.DownloadsList);
        public ObservableCollection<CryptoPeer> Servers => new ObservableCollection<CryptoPeer>(_client.Servers);
        public ObservableCollection<SharedFile> SharedFiles => new ObservableCollection<SharedFile>(_sharedFiles.SharedFilesList);
        public ObservableCollection<Upload> Uploads => new ObservableCollection<Upload>(_uploads.UploadsList);
        public ObservableCollection<CryptoPeer> Clients => new ObservableCollection<CryptoPeer>(_server.Clients);
        public FileInfo? SelectedAvailableFile { get; set; }
        public Download? SelectedDownload { get; set; }
        public CryptoPeer? SelectedServer { get; set; }
        public SharedFile? SelectedSharedFile { get; set; }

        #region Client & Server event handlers
        private void OnServerAdded(object? sender, CryptoPeerEventArgs e)
        {
            OnPropertyChanged(nameof(Servers));
        }

        private void OnServerRemoved(object? sender, CryptoPeerEventArgs e)
        {
            _downloads.CancelAllDownloadsFromServer(e.PeerID);
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

        private void OnSharedFileChanged(object? sender, EventArgs e)
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

        }

        private void OnServerMessageReceived(object? sender, NetEventArgs e)
        {
            var reader = e.Message;
            var client = e.CryptoPeer;

            var typeByte = reader.GetByte();
            if (!Enum.TryParse(typeByte + "", out NetMessageTypes type))
            {
                Debug.WriteLine("(Server_ProcessIncomingMessage) Unknown type of message: " + typeByte);

                return;
            }

            switch (type)
            {
                case NetMessageTypes.FilesListRequest:
                {
                    SendFilesList(client);
                }
                break;

                case NetMessageTypes.FileRequest:
                {
                    var fileHash = reader.GetString(64);
                    var uploadID = reader.GetString(20);

                    StartSendingFileToClient(fileHash, uploadID, client);
                }
                break;

                case NetMessageTypes.FileSegment:
                {
                    var uploadID = reader.GetString(20);
                    var numOfSegment = reader.GetLong();

                    if (!_uploads.Has(uploadID))
                    {
                        Debug.WriteLine("(Server_ProcessIncomingMessage_FileSegmentAck) No upload with such ID: " + uploadID);

                        return;
                    }

                    SendFileSegmentToClient(_uploads[uploadID].FileHash, uploadID, numOfSegment, client);
                }
                break;

                case NetMessageTypes.FileSegmentAck:
                {
                    var uploadID = reader.GetString(20);

                    if (!_uploads.Has(uploadID))
                    {
                        Debug.WriteLine("(Server_ProcessIncomingMessage_FileSegmentAck) No upload with such ID: " + uploadID);

                        return;
                    }

                    Debug.WriteLine("(Server_ProcessIncomingMessage_FileSegmentAck) Received ACK to segment of file: " + _uploads[uploadID].FileName);

                    _uploads[uploadID].AddAck();
                    SendFileSegmentToClient(_uploads[uploadID].FileHash, uploadID, _uploads[uploadID].NumberOfAckedSegments, client);
                }
                break;

                case NetMessageTypes.CancelDownload:
                {
                    var uploadID = reader.GetString(20);

                    if (!_uploads.Has(uploadID))
                    {
                        Debug.WriteLine("(Server_ProcessIncomingMessage_CancelDownload) No upload with such ID: " + uploadID);

                        return;
                    }

                    Debug.WriteLine("(Server_ProcessIncomingMessage_CancelDownload) Cancelling download of file: " + _uploads[uploadID].FileName);

                    _uploads[uploadID].Cancel();
                }
                break;
            }
        
        }
        #endregion

        #region Commands implementations
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

        private void DisconnectFromServer(CryptoPeer? server)
        {
            if (server == null)
            {
                return;
            }

            //todo
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
                        SendUploadDenial(upload.ID, upload.Destination);
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
        #endregion

        #region Methods for information exchange between clients and servers
        private void SendFilesListToAllClients()
        {
            var filesList = _sharedFiles.GetAvailableFiles();
            var filesListJson = JsonConvert.SerializeObject(filesList);
            var crc = CRC32.Compute(filesListJson);

            var message = new SimpleWriter();
            message.Put((byte)NetMessageTypes.FilesList);
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
            message.Put((byte)NetMessageTypes.FilesList);
            message.Put(crc);
            message.Put(filesListJson);

            destination.SendEncrypted(message);
        }

        private void StartSendingFileToClient(string fileHash, string uploadID, CryptoPeer destination)
        {
            if (!_sharedFiles.HasFileAvailable(fileHash) ||
                _uploads.Has(uploadID))
            {
                return;
            }

            var desiredFile = _sharedFiles.GetByHash(fileHash);
            var upload = new Upload(
                uploadID,
                desiredFile.Name,
                desiredFile.Size,
                desiredFile.Hash,
                destination,
                desiredFile.NumberOfSegments);

            _uploads.Add(upload);

            SendFileSegmentToClient(upload.FileHash, upload.ID, upload.NumberOfAckedSegments, destination);

            Debug.WriteLine("(FileSendRoutine) Upload " + id + " has started!");
        }

        private void SendFileSegmentToClient(string fileHash, string uploadID, long numberOfSegment, CryptoPeer destination)
        {
            if (_sharedFiles.HasFileAvailable(fileHash) &&
                _uploads.Has(uploadID) &&
                !_uploads[uploadID].IsFinished &&
                !_uploads[uploadID].IsCancelled)
            {
                var file = _sharedFiles.GetByHash(fileHash);

                if (file.TryReadSegment(numberOfSegment, uploadID, out SimpleWriter message))
                {
                    destination.SendEncrypted(message);
                }
            }
        }

        private void SendUploadDenial(string uploadID, CryptoPeer destination)
        {
            var message = new SimpleWriter();
            message.Put((byte)NetMessageTypes.CancelDownload);
            message.Put(uploadID);

            destination.SendEncrypted(message);
        }


        #endregion

        private void StartApp()
        {
            _client.StartListening();
            _server.StartListening();
        }

        private void ShutdownApp()
        {
            _client.DisconnectAll();
            _downloads.ShutdownAllDownloads();

            _server.DisconnectAll();
            _sharedFiles.CloseAllFileStreams();

            Application.Current.Shutdown();
        }
    }
}
