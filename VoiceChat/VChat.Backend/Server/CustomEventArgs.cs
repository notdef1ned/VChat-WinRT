using System;
using System.Net.Sockets;
using Windows.Networking.Sockets;

namespace Backend.Server
{
    public class CustomEventArgs : EventArgs
    {
        public StreamSocket ClientSocket { get; set; }

        public CustomEventArgs(StreamSocket clientSocket)
        {
            ClientSocket = clientSocket;
        }
    }
}
