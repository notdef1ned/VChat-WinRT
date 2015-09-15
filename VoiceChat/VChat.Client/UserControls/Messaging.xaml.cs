using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using VChat.Backend.Helpers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;


// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace VChat.Client.UserControls
{
    public sealed partial class Messaging : UserControl, INotifyPropertyChanged
    {
        #region Constructors
        public Messaging(Dictionary<Guid,string> usersDict = null)
        {
            DataContext = this;
            #region Global Guid selection
            usersDict = new Dictionary<Guid, string>();
            var globalGuid = Guid.NewGuid();
            usersDict.Add(globalGuid, ChatHelper.GLOBAL);
            selectedUser = globalGuid;
            
            #region TEMP
            usersDict.Add(Guid.NewGuid(), "User1");
            usersDict.Add(Guid.NewGuid(), "User2");
            usersDict.Add(Guid.NewGuid(), "User3");
            #endregion TEMP

            #endregion
            this.InitializeComponent();
            SetUsers(usersDict);
            InitList();
        }
        #endregion Constructors

        #region Fields
        private Guid selectedUser;
        private string selectedUsername = ChatHelper.GLOBAL;
        private ObservableCollection<DataItem> selectedMessageCollection;
        private readonly Dictionary<Guid, ChatHistoryState> history = new Dictionary<Guid, ChatHistoryState>(); 
        public static readonly Color DefaultColor = Color.FromArgb(255, 255, 255, 255);
        private const string Lorem = "Lorem ipsum dolor sit amet, ferri assentior ea his. Eleifend recteque iracundia ne ius, usu tempor aliquid periculis ea, ad nisl malis epicurei usu. Nec posse adversarium ex. Vix facete expetenda incorrupte no, cibo verear mel no.";
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion Events

        #region Properties


        /// <summary>
        /// Gets chat history with selected user
        /// </summary>
        public ChatHistoryState SelectedUserState
        {
            get
            {
                return history[selectedUser];
            }
        }

        public string SelectedUserName
        {
            get
            {
                return selectedUsername;
            }
            set
            {
                selectedUsername = value;
                OnPropertyChanged("SelectedUserName");
            }
        }


        #endregion Properties

        #region Methods
        /// <summary>
        /// Update users list
        /// </summary>
        /// <param name="list"></param>
        public void UpdateUsers(string[] list)
        {
                foreach (var item in list)
                    usersList.Items.Add(item);
        }

        /// <summary>
        /// Sets list of users currently online
        /// </summary>
        /// <param name="usersDict"></param>
        private void SetUsers(Dictionary<Guid,string> usersDict)
        {
            foreach (var user in usersDict)
            {
                usersList.Items.Add(new ListViewItem
                {
                    Content = user.Value, Tag = user.Key 
                });
                history.Add(user.Key, new ChatHistoryState { LastMessage = string.Empty});
            }
        }

        /// <summary>
        /// Temporarily sets sample data   
        /// </summary>
        private void InitList()
        {
            var item = new DataItem { IsLeft = true };
            item.Text = string.Format(Lorem + "\n{0:t}", DateTime.Now);
            

            var itemRight = new DataItem { IsRight = true };
            itemRight.Text = string.Format(Lorem + "\n{0:t}", DateTime.Now);

            selectedMessageCollection = history[selectedUser].Messages;

            selectedMessageCollection.Add(item);
            selectedMessageCollection.Add(itemRight);
            
            ChatListView.ItemsSource = selectedMessageCollection;
        }

        /// <summary>
        /// Sends message
        /// </summary>
        private void SendMessage()
        {
            SelectedUserState.Messages.Add(new DataItem()
            {
                IsRight = true,
                Text = string.Format(MessageBox.Text + "\n{0:t}", DateTime.Now)
            });
            MessageBox.Text = string.Empty;
        }
        #endregion Methods

        #region Event Handlers

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void MessageBox_KeyUp(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
                SendMessage();
        }

        private void usersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var view = sender as ListView;
            var item = view.SelectedItem as ListViewItem;
            var guid = (Guid)item.Tag;

            SelectedUserState.LastMessage = MessageBox.Text;
            selectedUser = guid;
            var userName = (string)item.Content;
            SelectedUserName = userName == ChatHelper.GLOBAL 
                            ? ChatHelper.GLOBAL
                            : string.Format(ChatHelper.SELECTED_USERNAME, userName);

            ChatListView.ItemsSource = SelectedUserState.Messages;
            MessageBox.Text = SelectedUserState.LastMessage;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion Event Handlers

    }
    /// <summary>
    /// Contains conversation history as well as last typed message 
    /// </summary>
    public class ChatHistoryState : INotifyPropertyChanged
    {
        /// <summary>
        /// Collection of sent messages
        /// </summary>
        private ObservableCollection<DataItem> messagesCollection = new ObservableCollection<DataItem>();

        /// <summary>
        /// Last message in message box
        /// </summary>
        private string lastMessage;

        public string LastMessage
        {
            get
            {
                return lastMessage;
            }
            set
            {
                lastMessage = value;
            }
        }

        public ObservableCollection<DataItem> Messages
        {
            get
            {
                return messagesCollection;
            }
            set
            {
                messagesCollection = value;
                OnPropertyChanged("Messages");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        
    }

    public class ChatTemplateSelector : DataTemplateSelector
    {
        public DataTemplate LeftTemplate { get; set; }
        public DataTemplate RightTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            DataItem di = (DataItem)item;
            DataTemplate dt = di.IsLeft ? this.LeftTemplate : this.RightTemplate;
            return dt;
        }
    }


    public class DataItem
    {
        public bool IsLeft { get; set; }
        public bool IsRight { get; set; }
        public string Text { get; set; }

        public Brush Fill
        {
            get
            {
                return new SolidColorBrush() { Color = Messaging.DefaultColor };
            }
        }
    }
    
}
