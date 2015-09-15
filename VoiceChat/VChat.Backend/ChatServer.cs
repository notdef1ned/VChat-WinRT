using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using VChat.Backend.Helpers;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.System.Threading;

namespace Backend.Server
{
    public class ChatServer
    {
        #region Fields
        private StreamSocketListener tcpServer;

        private readonly int portNumber;
        private bool isRunning;
        //private readonly NetworkInterface networkInterface;
        private readonly string serverName;
        private readonly List<ConnectedClient> clients = new List<ConnectedClient>();
        public event EventHandler ClientConnected;
        public event EventHandler ClientDisconnected;

        
        #endregion

        #region Constructor

        public ChatServer(int portNumber, object networkInterface, string serverName)
        {
            this.serverName = serverName;
            this.portNumber = portNumber;
            //this.networkInterface = networkInterface as NetworkInterface;
            CreateEventLog();
        }

        #endregion

        #region Server Start/Stop

        /// <summary>
        /// Starts the server
        /// </summary>
        public async void StartServer()
        {
            await ThreadPool.RunAsync(new WorkItemHandler((IAsyncResult) => StartListen()));
        }


        /// <summary>
        /// Server listens to specified port and accepts connection from client
        /// </summary>
        public async void StartListen()
        {
           // var ip = (networkInterface != null)
           //     ? GetInterfaceIpAddress()
           //     : IPAddress.Any;

            tcpServer = new StreamSocketListener();
            tcpServer.ConnectionReceived += TcpServer_ConnectionReceived;
            await tcpServer.BindServiceNameAsync(portNumber.ToString(),SocketProtectionLevel.PlainSocket);

            
            isRunning = true;
            
        }

        private async void TcpServer_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            if (isRunning)
            {
                await ThreadPool.RunAsync(new WorkItemHandler((IAsyncResult) => AddClient(args)));
            }
        }


        private void AddClient(StreamSocketListenerConnectionReceivedEventArgs args)
        {
            ClientAdded(this, new CustomEventArgs(args.Socket));
        }
        

        private IPAddress GetInterfaceIpAddress()
        {
            throw new NotImplementedException();
            //var ipAddresses = networkInterface.GetIPProperties().UnicastAddresses;
            //return (from ip in ipAddresses where ip.Address.AddressFamily == AddressFamily.InterNetwork select ip.Address).FirstOrDefault();
        }

        /// <summary>
        /// Method to stop TCP communication
        /// </summary>
        public void StopServer()
        {
            isRunning = false;
            if (tcpServer == null)
                return;
            clients.Clear();
            tcpServer.Dispose();
            tcpServer = null;
        }

        #endregion

        #region Add/Remove Clients

        /// <summary>
        /// When new client is added
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async void ClientAdded(object sender, EventArgs e)
        {
            var socket = ((CustomEventArgs)e).ClientSocket;

            var clientReader = new DataReader(socket.InputStream);
            clientReader.InputStreamOptions = InputStreamOptions.Partial;

            string clientName;

            while (true)
            {
                uint sizeFieldCount = await clientReader.LoadAsync(sizeof(uint));
                if (sizeFieldCount != sizeof(uint))
                {
                    // The underlying socket was closed before we were able to read the whole data.
                    return;
                }
                uint bytesLength = clientReader.ReadUInt32();
                uint actualBytesLength = await clientReader.LoadAsync(bytesLength);
                if (bytesLength != actualBytesLength)
                {
                    // The underlying socket was closed before we were able to read the whole data.
                    return;
                }
                var buffer = new byte[bytesLength];
                clientReader.ReadBytes(buffer);
                clientName = Encoding.Unicode.GetString(buffer);

                var newClient = new ConnectedClient(clientName, socket);
                clients.Add(newClient);
                break;
            }

            OnClientConnected(socket, clientName);

            foreach (var client in clients)
                SendUsersList(client.Connection, client.UserName, clientName, ChatHelper.CONNECTED);

            var state = new ChatHelper.StateObject
            {
                InputStream = socket.InputStream
            };

            var dataReader = new DataReader(socket.InputStream);


            var buff = new byte[dataReader.UnconsumedBufferLength];
            dataReader.ReadBytes(buff);

            //ChatHelper.WriteToEventLog(Log.ClientConnected, EventLogEntryType.Information);
        }

        /// <summary>
        /// Notify connecting user that specified name already exist
        /// </summary>
        /// <param name="clientSocket"></param>
        /// <param name="name"></param>
        public void SendNameAlreadyExist(StreamSocket clientSocket, string name)
        {
            var data = new Data {Command = Command.NameExist, To = name};
            var dataWriter = new DataWriter(clientSocket.OutputStream);
            dataWriter.WriteBytes(data.ToByte());
        }

        /// <summary>
        /// Broadcast clients number changed
        /// </summary>
        /// <param name="clientSocket"></param>
        /// <param name="userName"></param>
        /// <param name="changedUser"></param>
        /// <param name="state"></param>
        public async void SendUsersList(StreamSocket clientSocket, string userName, string changedUser, string state)
        {
            var data = new Data
            {
                Command = Command.Broadcast,
                To = userName,
                Message = string.Format("{0}|{1}|{2}|{3}",
                    string.Join(",", clients.Select(u => u.UserName).Where(name => name != userName)), changedUser, state,
                    serverName)
            };
            var dataWriter = new DataWriter(clientSocket.OutputStream);
            var bytes = data.ToByte();

            dataWriter.WriteInt32(bytes.Length);
            dataWriter.WriteBytes(bytes);
            await dataWriter.StoreAsync();
        }
        

        /// <summary>
        /// Parse client request
        /// </summary>
        /// <param name="state"></param>
        /// <param name="handlerSocket"></param>
        private void ParseRequest(ChatHelper.StateObject state, StreamSocket handlerSocket)
        {
            var data = new Data(state.Buffer);
            if (data.Command == Command.Disconnect)
            {
                DisconnectClient(handlerSocket);
                return;
            }

            var clientStr = clients.FirstOrDefault(cl => cl.UserName == data.To);
            if (clientStr == null) 
                return;

            var writer = new DataWriter(clientStr.Connection.OutputStream);
            writer.WriteBytes(data.ToByte());

        }

        /// <summary>
        /// Disconnect connected  TCP client
        /// </summary>
        /// <param name="clientSocket"></param>
        public void DisconnectClient(StreamSocket clientSocket)
        {
            var clientStr = clients.FirstOrDefault(k => k.Connection == clientSocket);
            if (clientStr == null)
                return;
            clientStr.IsConnected = false;
            OnClientDisconnected(clientSocket, clientStr.UserName);

            //clientSocket.Close();
            clients.Remove(clientStr);

            foreach (var client in clients)
                SendUsersList(client.Connection, client.UserName, clientStr.UserName, ChatHelper.DISCONNECTED);

            //ChatHelper.WriteToEventLog(Log.ClientDisconnected, EventLogEntryType.Information);
        }


        private static void CreateEventLog()
        {
           // if (EventLog.SourceExists(Log.ApplicationName))
           //     return;
           // EventLog.CreateEventSource(Log.ApplicationName, Log.ApplicationName);
        }

        #endregion

        #region Event Invokers

        protected virtual void OnClientConnected(StreamSocket clientSocket, string name)
        {
            var handler = ClientConnected;
            if (handler != null) handler(name, new CustomEventArgs(clientSocket));
        }

        protected virtual void OnClientDisconnected(StreamSocket clientSocket, string name)
        {
            var handler = ClientDisconnected;
            if (handler != null) handler(name, new CustomEventArgs(clientSocket));
        }

        #endregion
    }
    /// <summary>
    /// Used to store custom network interface description
    /// </summary>
    public class NetworkInterfaceDescription
    {
        public string Description { get;set; }
        public NetworkInterfaceDescription(string description)
        {
            Description = description;
        }
    }
}
