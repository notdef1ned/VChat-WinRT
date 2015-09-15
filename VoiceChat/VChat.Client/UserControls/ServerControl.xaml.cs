using Backend.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using VChat.Backend.Helpers;
using VChat.Backend.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.System.Threading;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace VChat.UserControls
{
    public sealed partial class ServerControl : UserControl
    {
        private Timer timer = new Timer();
        private int portNumber;
        private Page currentPage;


        public string TimeElapsed
        {
            get; set;
        }

        public ServerControl(Page currentPage)
        {
            this.InitializeComponent();
            ObtainNetworkInterfaces();
            UIDispatcher.Initialize();
            SetControls(true);
            this.currentPage = currentPage;
            timer.PropertyChanged += Timer_PropertyChanged;
        }

        private ChatServer server;

        private readonly List<LocalHostItem> localHostItems = new List<LocalHostItem>();

        public delegate void SetListBoxItem(string str, string type);

        private void tb_Toggled(object sender, RoutedEventArgs e)
        {
            if (tsStartStop.IsOn)
            {
                // validate the port number
                try
                {
                    portNumber = int.Parse(tbPortNumber.Text);

                    server = new ChatServer(portNumber, cbInterfaces.SelectedItem, tbServerName.Text);
                    server.ClientConnected += ServerOnClientConnected;
                    server.ClientDisconnected += ServerOnClientDisconnected;

                    var serverName = tbServerName.Text;

                    if (string.IsNullOrWhiteSpace(serverName))
                        ShowError();
                    else
                    {
                        server.StartServer();
                        timer.StartTimer();
                        SetControls(false);
                    }
                }
                catch
                {
                    ShowError();
                }


            }

            else
            {
                if (server == null)
                    return;
                timer.StopTimer();
                server.StopServer();
                SetControls(true);
            }
        }

        private async void ShowError()
        {
            var dialog = new MessageDialog(ChatHelper.ErrorMessage);
            await dialog.ShowAsync();
            tsStartStop.IsOn = false;
        }

        private void SetControls(bool enabled)
        {
            tbPortNumber.IsEnabled = tbServerName.IsEnabled = cbInterfaces.IsEnabled = enabled;
            btnStartClient.IsEnabled = !enabled;
        }

        private void InitTimer()
        {
            timer.PropertyChanged += Timer_PropertyChanged;
        }

        private void Timer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            tsStartStop.OnContent = timer.TimeElapsed;
        }

        private void ServerOnClientDisconnected(object sender, EventArgs e)
        {
            var userName = (string)sender;
            var item = FormatClient(userName, e);
            UpdateConnectedClients(item, "Delete");
        }

        private void ServerOnClientConnected(object sender, EventArgs e)
        {
            var userName = (string)sender;
            var item = FormatClient(userName, e);
            UpdateConnectedClients(item, "Add");
        }

        private static string FormatClient(string userName, EventArgs e)
        {
            var args = e as CustomEventArgs;
            if (args == null)
                return string.Empty;

            var client = args.ClientSocket;
            var remoteIp = client.Information.RemoteAddress;
            var remotePort = client.Information.RemotePort;

            return string.Format("{0} ({1}:{2})", userName, remoteIp, remotePort);
        }


        private async void UpdateConnectedClients(string str, string type)
        {
            if (!lvConnectedClients.Dispatcher.HasThreadAccess)
            {
                SetListBoxItem d = UpdateConnectedClients;
                await ThreadPool.RunAsync(operation => UIDispatcher.Execute(() => AddItem(str, type)));
            }
            else
                AddItem(str, type);

        }


        private void AddItem(string str, string type)
        {
            // If type is Add, the add Client info in ListView
            if (type.Equals("Add"))
            {
                lvConnectedClients.Items.Add(str);
            }
            // Else delete Client information from ListView
            else
            {
                lvConnectedClients.Items.Remove(str);
            }
        }

        /// <summary>
        /// Obtain all network interfaces
        /// </summary>
        private void ObtainNetworkInterfaces()
        {
            localHostItems.Clear();
            cbInterfaces.ItemsSource = localHostItems;
            cbInterfaces.DisplayMemberPath = "DisplayString";

            foreach (HostName localHostInfo in NetworkInformation.GetHostNames())
            {
                if (localHostInfo.IPInformation != null)
                {
                    LocalHostItem adapterItem = new LocalHostItem(localHostInfo);
                    localHostItems.Add(adapterItem);
                }
            }
        }


        private void ServerForm_OnClosing(object sender, CancelEventArgs e)
        {
            if (server != null)
                server.StopServer();
        }

        class LocalHostItem
        {
            public string DisplayString
            {
                get;
                private set;
            }

            public HostName LocalHost
            {
                get;
                private set;
            }

            public LocalHostItem(HostName localHostName)
            {
                if (localHostName == null)
                {
                    throw new ArgumentNullException("localHostName");
                }

                if (localHostName.IPInformation == null)
                {
                    throw new ArgumentException("Adapter information not found");
                }

                this.LocalHost = localHostName;
                this.DisplayString = "Address: " + localHostName.DisplayName +
                    " Adapter: " + localHostName.IPInformation.NetworkAdapter.NetworkAdapterId;
            }
        }

        private void Start_Client(object sender, RoutedEventArgs e)
        {
            var localHostItem = cbInterfaces.SelectedItem as LocalHostItem;
            var connectStruct = new ConnectStruct(portNumber, localHostItem.LocalHost.DisplayName, tbServerName.Text);
            currentPage.Frame.Navigate(typeof(BaseChatPage),connectStruct);
        }
    }

    public class Timer : INotifyPropertyChanged
    {
        #region Fields
        private DispatcherTimer timer;
        private Stopwatch stopWatch;
        #endregion Fields

        #region Properties
        public string TimeElapsed { get; set; }
        #endregion Properties

        public event PropertyChangedEventHandler PropertyChanged;

        public void StartTimer()
        {
            timer = new DispatcherTimer();
            #region Subscriptions
            timer.Tick += dispatcherTimerTick_;
            #endregion Subscriptions
            stopWatch = new Stopwatch();
            stopWatch.Start();
            timer.Start();
        }

        public void StopTimer()
        {
            timer.Tick -= dispatcherTimerTick_;
            timer.Stop();
        }

        private void dispatcherTimerTick_(object sender, object e)
        {
            var ts = stopWatch.Elapsed;
            TimeElapsed = string.Format("{0} {1:00}:{2:00}.{3:00}", ChatHelper.SERVER_RUNNING, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            PropertyChanged(this, new PropertyChangedEventArgs("TimeElapsed"));
        }
    }
}
