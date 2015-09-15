using VChat.Client.UserControls;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace VChat.Client.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MessagePage : BaseChatPage
    {
        public MessagePage()
        {
            this.InitializeComponent();
            Main.Children.Add(new Messaging());
        }

        private void RadioButton_Checked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            (sender as RadioButton).IsChecked = false;

            // toggle the splitview pane
            //this.ShellSplitView.IsPaneOpen = !this.ShellSplitView.IsPaneOpen;
        }
    }
}
