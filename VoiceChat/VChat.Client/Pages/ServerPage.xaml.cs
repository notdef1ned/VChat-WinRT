using Backend.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using VChat.Backend.Helpers;
using VChat.Backend.Threading;
using VChat.UserControls;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.System.Threading;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace VChat.Client
{
    /// <summary>
    /// Server page implementation
    /// </summary>
    public sealed partial class ServerPage : Page
    {
        public ServerPage()
        {
            InitializeComponent();
            ContentGrid.Children.Add(new ServerControl(this));
        }
    }

}
