using System;
using System.Net.Sockets;
using System.Threading;

namespace NTRIPCaster.Server {

    public class ServerConnection {
        Socket Socket;
        NTRIPServer Server;

        public string Mountpoint { get; private set; }

        public string Id { get { return $"Server_{Socket.RemoteEndPoint}/{Mountpoint}"; } }

        public ServerConnection(Socket socket, string moutpoint, NTRIPServer server) {
            this.Socket = socket;
            this.Mountpoint = moutpoint;
            this.Server = server;

            socket.Blocking = true;
            socket.ReceiveTimeout = 10000;
        }

        public void StartProcessing() {
            var thread = new Thread(() => Process());
            thread.IsBackground = true;
            thread.Start();
        }

        private void Process() {
            var buffer = new byte[1024];

            try {
                while (Socket.Connected) {
                    var bytesCount = Socket.Receive(buffer);

                    var data = new byte[bytesCount];
                    Array.Copy(buffer, data, bytesCount);

                    foreach (var client in Server.GetClients(Mountpoint)) {
                        if (client.IsClosed) {
                            Server.RemoveClient(client);
                        } else {
                            client.Enqueue(data);
                        }
                    }
                }
            } catch (SocketException) {
            } finally {
                Socket.Close();
            }
        }

        public override string ToString() {
            return Id;
        }
    }
}
