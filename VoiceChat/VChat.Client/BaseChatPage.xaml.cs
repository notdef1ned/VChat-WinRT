using Backend.Client;
using VChat.Backend.Helpers;
using VChat.Client.UserControls;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace VChat
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public partial class BaseChatPage : Page
    {
        private Messaging messagingControl;

        public BaseChatPage()
        {
            this.InitializeComponent();
            this.messagingControl = new Messaging();
            ContentGrid.Children.Add(messagingControl);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var connParameters = e.Parameter as ConnectStruct;
            ClientSingleton.CreateClient(connParameters.PortNumber, 
                connParameters.HostName, connParameters.UserName);
            InitClient();
       }

        private void InitClient()
        {
            #region Event subscription
            var instance = ClientSingleton.Instance;
            instance.UserListReceived += Instance_UserListReceived;
            instance.CallRecieved += Instance_CallRecieved;
            instance.CallRequestResponded += Instance_CallRequestResponded;
            instance.MessageReceived += Instance_MessageReceived;
            instance.FileRecieved += Instance_FileRecieved;
            #endregion
        }


        private void SplitPaneChecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            (sender as RadioButton).IsChecked = false;
            // toggle the splitview pane
            this.ShellSplitView.IsPaneOpen = !this.ShellSplitView.IsPaneOpen;
        }

        private void ProfileChecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            this.ContentGrid.Children.Clear();
            this.ContentGrid.Children.Add(new Profile());
        }

        private void HomePaneChecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            this.ContentGrid.Children.Clear();
            this.ContentGrid.Children.Add(messagingControl);
        }


        private void Instance_FileRecieved(object sender, System.EventArgs e)
        {
            throw new System.NotImplementedException();
        }

        private void Instance_MessageReceived(object sender, System.EventArgs e)
        {
            throw new System.NotImplementedException();
        }

        private void Instance_CallRequestResponded(object sender, System.EventArgs e)
        {
            throw new System.NotImplementedException();
        }

        private void Instance_CallRecieved(object sender, System.EventArgs e)
        {
            throw new System.NotImplementedException();
        }

        private void Instance_UserListReceived(object sender, ServerEventArgs e)
        {
            if (e.Message != null)
                messagingControl.UpdateUsers(ChatHelper.Split(e.Message));
        }

        
    }
}
