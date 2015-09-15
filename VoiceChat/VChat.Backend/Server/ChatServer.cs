﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using VChat.Backend.Helper;
using VChat.Backend.Helpers;
using Windows.Networking.Sockets;
using Windows.System.Threading;

namespace Backend.Server
{
    public class ChatServer
    {
        #region Fields
        private StreamSocketListener tcpServer;
        private readonly int portNumber;
        private bool isRunning;
        private readonly NetworkInterface networkInterface;
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
            this.networkInterface = networkInterface as NetworkInterface;
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
            var ip = (networkInterface != null)
                ? GetInterfaceIpAddress()
                : IPAddress.Any;

            tcpServer = new StreamSocketListener();
            await tcpServer.BindServiceNameAsync(portNumber.ToString());
            tcpServer.ConnectionReceived += TcpServer_ConnectionReceived;
            
            isRunning = true;
            // Keep accepting client connection
            while (isRunning)
            {
                if (!tcpServer.Pending())
                {
                    Thread.Sleep(500);
                    continue;
                }
                // New client is connected, call event to handle it
                var clientThread = new Thread(NewClient);
                var tcpClient = tcpServer.AcceptTcpClient();
                tcpClient.ReceiveTimeout = 20000;
                clientThread.Start(tcpClient.Client);
            }
        }

        private void TcpServer_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            if (isRunning)
            {
                ClientAdded(this, new CustomEventArgs(args.Socket));
            }
        }
        

        private IPAddress GetInterfaceIpAddress()
        {
            var ipAddresses = networkInterface.GetIPProperties().UnicastAddresses;
            return (from ip in ipAddresses where ip.Address.AddressFamily == AddressFamily.InterNetwork select ip.Address).FirstOrDefault();
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
            //tcpServer.Stop();
        }

        #endregion

        #region Add/Remove Clients

        /// <summary>
        /// When new client is added
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ClientAdded(object sender, EventArgs e)
        {
            var socket = ((CustomEventArgs) e).ClientSocket;
            var bytes = new byte[1024];
            var bytesRead = socket.Receive(bytes);
           
            var newUserName = Encoding.Unicode.GetString(bytes, 0, bytesRead);
            
            if (clients.Any(client => client.UserName == newUserName))
            {
                SendNameAlreadyExist(socket,newUserName);
                return;
            }

            var newClient = new ConnectedClient(newUserName, socket);
            clients.Add(newClient);
            
            OnClientConnected(socket, newUserName);

            foreach (var client in clients)
                SendUsersList(client.Connection, client.UserName, newUserName, ChatHelper.CONNECTED);
           
            var state = new ChatHelper.StateObject
            {
                InputStream = socket
            };
            
            socket.BeginReceive(state.Buffer, 0, ChatHelper.StateObject.BUFFER_SIZE, 0,
            OnReceive, state);

            ChatHelper.WriteToEventLog(Log.ClientConnected, EventLogEntryType.Information);
        }

        /// <summary>
        /// Notify connecting user that specified name already exist
        /// </summary>
        /// <param name="clientSocket"></param>
        /// <param name="name"></param>
        public void SendNameAlreadyExist(Socket clientSocket, string name)
        {
            var data = new Data {Command = Command.NameExist, To = name};
            clientSocket.Send(data.ToByte());
        }

        /// <summary>
        /// Broadcast clients number changed
        /// </summary>
        /// <param name="clientSocket"></param>
        /// <param name="userName"></param>
        /// <param name="changedUser"></param>
        /// <param name="state"></param>
        public void SendUsersList(Socket clientSocket, string userName, string changedUser, string state)
        {
            var data = new Data
            {
                Command = Command.Broadcast,
                To = userName,
                Message = string.Format("{0}|{1}|{2}|{3}",
                    string.Join(",", clients.Select(u => u.UserName).Where(name => name != userName)), changedUser, state,
                    serverName)
            };
            clientSocket.Send(data.ToByte());
        }

        public void OnReceive(IAsyncResult ar)
        {
            var state = ar.AsyncState as ChatHelper.StateObject;
            if (state == null)
                return;
            var handler = state.InputStream;
            if (!handler.Connected)
                return;
            try
            {
                var bytesRead = handler.EndReceive(ar);
                if (bytesRead <= 0)
                    return;

                ParseRequest(state,handler);
            }

            catch (Exception)
            {
                ChatHelper.WriteToEventLog(Log.TcpClientUnexpected,EventLogEntryType.Error);
                DisconnectClient(handler);
            }
        }

        /// <summary>
        /// Parse client request
        /// </summary>
        /// <param name="state"></param>
        /// <param name="handlerSocket"></param>
        private void ParseRequest(ChatHelper.StateObject state, Socket handlerSocket)
        {
            var data = new Data(state.Buffer);
            if (data.Command == Command.Disconnect)
            {
                DisconnectClient(state.InputStream);
                return;
            }
            var clientStr = clients.FirstOrDefault(cl => cl.UserName == data.To);
            if (clientStr == null) 
                return;
            clientStr.Connection.Send(data.ToByte());
            handlerSocket.BeginReceive(state.Buffer, 0, ChatHelper.StateObject.BUFFER_SIZE, 0,
              OnReceive, state);
        }

        /// <summary>
        /// Disconnect connected  TCP client
        /// </summary>
        /// <param name="clientSocket"></param>
        public void DisconnectClient(Socket clientSocket)
        {
            var clientStr = clients.FirstOrDefault(k => k.Connection == clientSocket);
            if (clientStr == null)
                return;
            clientStr.IsConnected = false;
            OnClientDisconnected(clientSocket, clientStr.UserName);

            clientSocket.Close();
            clients.Remove(clientStr);

            foreach (var client in clients)
                SendUsersList(client.Connection, client.UserName, clientStr.UserName, ChatHelper.DISCONNECTED);

            ChatHelper.WriteToEventLog(Log.ClientDisconnected, EventLogEntryType.Information);
        }


        private static void CreateEventLog()
        {
            if (EventLog.SourceExists(Log.ApplicationName))
                return;
            EventLog.CreateEventSource(Log.ApplicationName, Log.ApplicationName);
        }

        #endregion

        #region Event Invokers

        protected virtual void OnClientConnected(Socket clientSocket, string name)
        {
            var handler = ClientConnected;
            if (handler != null) handler(name, new CustomEventArgs(clientSocket));
        }

        protected virtual void OnClientDisconnected(Socket clientSocket, string name)
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
