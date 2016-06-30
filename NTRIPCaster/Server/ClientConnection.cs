using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;

namespace NTRIPCaster.Server {

    public class ClientConnection {
        Socket Socket;
        BlockingCollection<byte[]> Queue;

        public string Username { get; private set; }
        public string Mountpoint { get; private set; }
        public bool IsClosed { get; private set; }

        public string Id { get { return $"Client_{Socket.RemoteEndPoint}"; } }

        public ClientConnection(Socket socket, string mountpoint, string username) {
            this.Socket = socket;
            this.Queue = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
            this.Mountpoint = mountpoint;
            this.Username = username;
        }

        public void Enqueue(byte[] buffer) {
            Queue.Add(buffer);
        }

        public void StrartProcessing() {
            new Thread(() => Process()).Start();
        }

        private void Process() {
            try {
                while (Socket.Connected) {
                    var data = Queue.Take();
                    Socket.Send(data);
                }
            } catch (SocketException) {
                IsClosed = true;
            } finally {
                Socket.Close();
            }
        }

        public override string ToString() {
            return Id;
        }
    }
}
