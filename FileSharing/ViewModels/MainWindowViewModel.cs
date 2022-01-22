﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using SystemTrayApp.WPF;
using FileSharing.Models;

namespace FileSharing.ViewModels
{
    public class MainWindowViewModel : ObservableRecipient
    {
        private NotifyIconWrapper.NotifyRequestRecord? _notifyRequest;
        private bool _showInTaskbar;
        private WindowState _windowState;

        private readonly Client _client;
        private readonly Server _server;

        public MainWindowViewModel()
        {
            LoadedCommand = new RelayCommand(Loaded);
            ClosingCommand = new RelayCommand<CancelEventArgs>(Closing);
            NotifyCommand = new RelayCommand(() => Notify("Hello world!"));
            NotifyIconOpenCommand = new RelayCommand(() => { WindowState = WindowState.Normal; });
            NotifyIconExitCommand = new RelayCommand(() => { Application.Current.Shutdown(); });

            _client = new Client();
            _client.ServerUpdated += OnServerUpdated;
            _client.ServerRemoved += OnServerRemoved;
            _client.MessageReceived += OnClientMessageReceived;

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
                return;
            e.Cancel = true;
            WindowState = WindowState.Minimized;
        }
        #endregion

        private void OnServerUpdated(object sender, CryptoPeerEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnServerRemoved(object sender, CryptoPeerEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnClientMessageReceived(object sender, NetEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnClientUpdated(object sender, CryptoPeerEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnClientRemoved(object sender, CryptoPeerEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnServerMessageReceived(object sender, NetEventArgs e)
        {
            throw new NotImplementedException();
        }


    }
}
