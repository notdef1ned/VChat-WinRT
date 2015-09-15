using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using VChat.Backend.Helpers;
using NAudio.Wave;
using Windows.Networking.Sockets;
using Windows.UI.Popups;
using Windows.Storage.Streams;
using static VChat.Backend.Helpers.ChatHelper;
using Windows.System.Threading;
using NAudio.Win8.Wave.WaveOutputs;
using NAudio.CoreAudioApi;
using Backend.Client;
using System.Threading.Tasks;

namespace VChat.Backend.Client
{
    public class ChatClient
    {
        #region Fields

        #region Sockets
        private StreamSocket server;
        private readonly DatagramSocket udpSocket = new DatagramSocket();
        #endregion Sockets

        #region Stream Reading & Writing to server
        DataReader serverReader;
        DataWriter serverWriter;
        DataWriter udpWriter;
        string CaptureDevice = "0";
        #endregion

        #region IP Points
        private readonly IPEndPoint localEndPoint;
        private IPEndPoint remoteEndPoint;
        #endregion IP Points

        #region Sound streams
        private readonly WasapiCaptureRT recorder = new WasapiCaptureRT { WaveFormat = new WaveFormat()};
        private readonly WasapiOutRT player = new WasapiOutRT(AudioClientShareMode.Shared, 200);
        private BufferedWaveProvider waveProvider;
        #endregion

        #region Udp fields
        private string udpSubscriber;
        private bool udpConnectionActive;
        #endregion Udp fields

        #endregion

        #region Events
        public event EventHandler<ServerEventArgs> UserListReceived;
        public event EventHandler MessageReceived;
        public event EventHandler CallRecieved;
        public event EventHandler CallRequestResponded;
        public event EventHandler FileRecieved;
        #endregion

        #region Properties

        #region Profile Info
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        #endregion

        public string ClientAddress { get; set; }
        public string ServerAddress { get; set; }
        public string ServerName { get; set; }
        public bool IsReceivingData { get; set; }
        public bool IsConnected { get; set; }
        public int InputAudioDevice { get; set; }
        public int OutputAudioDevice { get; set; }
        public bool DoubleClickToCall { get; set; }
        public bool LaunchOnStartup { get; set; }
        public string Scheme { get; set; }


        #endregion

        #region Consructor
        /// <summary>
        /// Connect to server
        /// </summary>
        /// <param name="port"></param>
        /// <param name="serverAddress"></param>
        /// <param name="userName"></param>
        public ChatClient(int port, string serverAddress, string userName)
        {
            try
            {
                ConnectToServer(port, serverAddress);
                IsConnected = true;

                //localEndPoint = GetHostEndPoint();
                ServerAddress = serverAddress;
                UserName = userName;

                udpSocket.MessageReceived += UdpSocket_MessageReceived;
            }
            catch (SocketException)
            {
                var messageDialog = new MessageDialog(@"Unable to connect to server");
                messageDialog.Commands.Add(new UICommand("Ok"));
                return;
            }
            SendUsername(userName);
        }
        #endregion

        #region Methods


        /// <summary>
        /// Sends username to server
        /// </summary>
        /// <param name="userName"></param>
        private async void SendUsername(string userName)
        {
            // Send username to server

            var bytes = Encoding.Unicode.GetBytes(userName);
            serverWriter.WriteUInt32((uint)bytes.Length);
            serverWriter.WriteBytes(bytes);

            await serverWriter.StoreAsync();
        }

        /// <summary>
        /// Asynchronously connects to server, 
        /// initializes reader and writer
        /// </summary>
        /// <param name="port"></param>
        /// <param name="serverAddress"></param>
        private async void ConnectToServer(int port, string serverAddress)
        {
            server = new StreamSocket();


            serverWriter = new DataWriter(server.OutputStream);
            serverReader = new DataReader(server.InputStream);


            await server.ConnectAsync(
                new Windows.Networking.HostName(serverAddress),
                port.ToString()
                );


            udpWriter = new DataWriter(udpSocket.OutputStream);

            // set reader to only wait for availlable data
            serverReader.InputStreamOptions = InputStreamOptions.Partial;

            Init();
        }


        private void BindSocket()
        {
            //if (!udpSocket.IsBind)
              //  udpSocket.Bind(localEndPoint);
        }

        private IPEndPoint GetHostEndPoint()
        {
            
            var host = server.Information.RemoteAddress;

            throw new NotImplementedException();
            //host.DisplayName
            //var ipAddress = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            //if (ipAddress == null) 
            //    return null;
            //var random = new Random();
            //var endPoint = new IPEndPoint(ipAddress,random.Next(65000,65536));
            //ClientAddress = string.Format("{0}:{1}",endPoint.Address,endPoint.Port);
            //return endPoint;
        }


        public async void Init()
        {
            //Receive list of users online 
            await ReceiveUsersList();
            //heartBeatThread = new Thread(HeartBeat);
            //heartBeatThread.Start();

           // RunReceive();
        }

        /// <summary>
        /// Run receive from server method in separate thread
        /// </summary>
        private async void RunReceive()
        {
             await ThreadPool.RunAsync(new WorkItemHandler(
                (IAsyncResult) => RecieveFromServer()),
                WorkItemPriority.Normal);
        }


        /// <summary>
        /// Creates wave provider for playback
        /// </summary>
        /// <returns></returns>
        private IWaveProvider CreateReader()
        {
            return waveProvider = new BufferedWaveProvider(new
                WaveFormat());
        }
    
        private void RecieveFromServer()
        {
            var state = new StateObject
            {
                InputStream = server.InputStream
            };

            while (IsConnected)
            {
                if (IsReceivingData) 
                    continue;
                IsReceivingData = true;
                ReceiveData(state);
            }
        }

        // Asynchronously recieve data from server
        public async void ReceiveData(StateObject state)
        {
            // wait for the available data up to StateObject.BUFFER_SIZE 
            var count = await serverReader.LoadAsync(StateObject.BUFFER_SIZE);

            if (count > 0)
            {
                serverReader.ReadBytes(state.Buffer);
                CallbackRecieve(state);
            }
        }

        

        private async Task ReceiveUsersList()
        {
            string[] serverMessage = null;

            while (true)
            {

                uint sizeFieldCount = await serverReader.LoadAsync(sizeof(int));

                if (sizeFieldCount != sizeof(int))
                {
                    return;
                }

                var bytesLength = serverReader.ReadInt32();
                uint actualBytesLength = await serverReader.LoadAsync((uint)bytesLength);
                if (bytesLength != actualBytesLength)
                {
                    // The underlying socket was closed before we were able to read the whole data.
                    return;
                }

                var bytes = new byte[serverReader.UnconsumedBufferLength];
                serverReader.ReadBytes(bytes);
                var data = new Data(bytes);

                serverMessage = data.Message.Split(new[] { '|' }, StringSplitOptions.None);

                if (data.Command == Command.NameExist)
                {
                    //MessageBox.Show(string.Format("Name \"{0}\" already exist on server", serverMessage[1]));
                    break;
                }

                break;
            }

            if (serverMessage == null)
                return;

            ServerName = serverMessage[3];
            
            OnUserListReceived(serverMessage);

        }

        public void CallbackRecieve(StateObject state)
        {
            if (state == null)
                return;
            try
            {
                ParseMessage(new Data(state.Buffer));
            }
            catch (SocketException)
            {
                IsConnected = false;
                //server.Client.Disconnect(true);
            }
        }

        

        /// <summary>
        /// Parse received message
        /// </summary>
        /// <param name="data"></param>
        public void ParseMessage(Data data)
        {
            switch (data.Command)
            {
                case Command.SendMessage:
                    OnMessageReceived(data.Message, data.From);
                break;
                case Command.SendFile:
                    OnFileRecieved(data.File, data.From, data.Message);
                break;
                case Command.Broadcast:
                    OnUserListReceived(data.Message.Split('|'));
                break;
                case Command.Call:
                    if (!udpConnectionActive)
                        OnCallRecieved(data.From, data.ClientAddress);
                    SendResponse(Command.Busy);
                break;
                case Command.AcceptCall:
                case Command.CancelCall:
                case Command.EndCall:
                case Command.Busy:
                    ParseResponse(data.From, data.Command, data.ClientAddress);
                    OnCallResponseReceived(data.Command);
                break;
            }
        }

        /// <summary>
        /// Parses file extension
        /// </summary>
        /// <param name="fileName">Full filename</param>
        private string ParseFileExtension(string fileName)
        {
            return System.IO.Path.GetExtension(fileName);
        }

        /// <summary>
        /// Parse call response
        /// </summary>
        /// <param name="user">Caller</param>
        /// <param name="response">Response</param>
        /// <param name="address">Caller Address</param>
        private void ParseResponse(string user,Command response,string address)
        {
            switch (response)
            {
                case Command.AcceptCall:
                    udpSubscriber = user;
                    StartVoiceChat(address);
                break;
                case Command.EndCall:
                case Command.CancelCall:
                case Command.Busy:
                    EndChat(false);
                break;
            }
        }
        /// <summary>
        /// Open socket for voice chat
        /// </summary>
        /// <param name="address"></param>
        private void StartVoiceChat(string address)
        {
            var splittedAddress = address.Split(':');
            var ip = splittedAddress[0];
            var port = splittedAddress[1];
            
            BindSocket();

            remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip),Int32.Parse(port));

            

            udpConnectionActive = true;

            recorder.DataAvailable += sourceStream_DataAvailable;
            recorder.StartRecording();
        }
        


        private void sourceStream_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (recorder == null)
                return;
            SendUdpData(e.Buffer, e.BytesRecorded);
        }


        private void SendUdpData(byte[] buf, int bytesRecorded)
        {
            try
            {
                if (!udpConnectionActive)
                {
                    recorder.StopRecording();
                    return;
                }
                var ep = remoteEndPoint as EndPoint;
                udpWriter.WriteBytes(buf);
            }
            catch (Exception)
            {
                recorder.StopRecording();    
            }
        }
        

        private void UdpSocket_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            var datareader = args.GetDataReader();
            var dataLength = datareader.UnconsumedBufferLength;
            var buff = new byte[dataLength];
            datareader.ReadBytes(buff);
            
            try
            {
                waveProvider.AddSamples(buff, 0, (int)dataLength);

                player.Init(CreateReader);
                player.Play();

                var ep = (EndPoint)localEndPoint;
                if (!udpConnectionActive)
                {
                    datareader.Dispose();
                    udpSocket.OutputStream.Dispose();
                }
            }
            catch (Exception)
            {
                udpConnectionActive = false;
            }

        }

        /// <summary>
        /// Send message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="recipient"></param>
        public void SendMessage(string message,string recipient)
        {
            var data = new Data {Command = Command.SendMessage, To = recipient, From = UserName, Message = message};
            serverWriter.WriteBytes(data.ToByte());
        }

        /// <summary>
        /// Send file to recipient
        /// </summary>
        /// <param name="file"></param>
        /// <param name="recipient"></param>
        /// <param name="fileName"></param>
        public void SendFile(byte[] file, string recipient, string fileName)
        {
            var data = new Data {Command = Command.SendFile, File = file, Message = fileName, To = recipient, From = UserName};
            serverWriter.WriteBytes(data.ToByte());
        }

        /// <summary>
        /// Call to user
        /// </summary>
        /// <param name="recipient"></param>
        public void SendChatRequest(string recipient)
        {
            var data = new Data {Command = Command.Call, To = recipient, From = UserName, ClientAddress = ClientAddress};
            serverWriter.WriteBytes(data.ToByte());
        }
        /// <summary>
        /// Send call response to caller
        /// </summary>
        /// <param name="response"></param>
        public void SendResponse(Command response)
        {
            var data = new Data {Command = response, To = udpSubscriber, From = UserName, ClientAddress = ClientAddress};
            serverWriter.WriteBytes(data.ToByte());
        }


        /// <summary>
        /// Closes server connection
        /// </summary>
        public void CloseConnection()
        {
            IsConnected = false;
            var data = new Data {Command = Command.Disconnect};
            serverWriter.WriteBytes(data.ToByte());
        }
        
        /// <summary>
        /// Ends UDP connection
        /// </summary>
        /// <param name="requestNeeded"></param>
        public void EndChat(bool requestNeeded)
        {
            if (requestNeeded)
                SendResponse(Command.EndCall);
            udpConnectionActive = false;
        }


        public void AnswerIncomingCall(string caller, string address, Command answer)
        {
            var data = new Data {Command = answer, From = UserName, To = caller, ClientAddress = ClientAddress};
            if (answer == Command.AcceptCall)
            {
                udpSubscriber = caller;
                StartVoiceChat(address);
            }
            serverWriter.WriteBytes(data.ToByte());
        }

        #endregion

        #region Event Invokers

        protected virtual void OnUserListReceived(string[] serverMessage)
        {
            var handler = UserListReceived;
            if (handler != null)
                handler(serverMessage[0], new ServerEventArgs(serverMessage[1], serverMessage[2], serverMessage[3]));
        }

        protected virtual void OnMessageReceived(string message, string sender)
        {
            var handler = MessageReceived;
            if (handler != null) 
                handler(sender, new ServerEventArgs(message));
        }

        protected virtual void OnCallRecieved(string caller, string address)
        {
            var handler = CallRecieved;
            if (handler != null) 
                handler(address, new ServerEventArgs(caller));
        }

        protected virtual void OnCallResponseReceived(Command response)
        {
            var handler = CallRequestResponded;
            if (handler != null) 
                handler(response, EventArgs.Empty);
        }
        protected virtual void OnFileRecieved(byte[] file, string from, string fileName)
        {
            var handler = FileRecieved;
            if (handler != null) 
                handler(this, new FileEventArgs(file, from, fileName,ParseFileExtension(fileName)));
        }

        #endregion
    }

    
}
