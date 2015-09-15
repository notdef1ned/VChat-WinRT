using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using VChat.Client;


// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace VChat
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }


        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            foreach (var child in MainGrid.Children)
                child.Visibility = Visibility.Collapsed;

            signIn.Visibility = progress.Visibility = Visibility.Visible;
            progress.IsActive = true;

            

            Frame.Navigate(typeof(BaseChatPage));
        }


        private void btnCreateServer_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ServerPage));
        }

    }
}
