using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NTRIPCaster.Configs;

namespace NTRIPCaster.Server {

    public class NTRIPServer : IDisposable {
        private Config Config;
        private TcpListener Listener;
        private IDictionary<string, ImmutableList<ClientConnection>> Clients;
        private object lockObj = new object();
        private Thread ServerThread;

        public NTRIPServer(Config config) {
            this.Config = config;
            this.Listener = new TcpListener(IPAddress.Parse(config.ServerAddress), config.ServerPort);
            this.Clients = new Dictionary<string, ImmutableList<ClientConnection>>();

            foreach (var source in config.Sources) {
                this.Clients.Add(source.Mountpoint, ImmutableList.Create<ClientConnection>());
            }
        }

        public void Start() {
            ServerThread = new Thread(() => ProcessRequests());
            ServerThread.Start();
        }

        public void Stop() {
            // TODO: Proper Shutdown
            if (ServerThread != null) {
                ServerThread.Abort();
            }
            Environment.Exit(0);
        }

        public IEnumerable<ClientConnection> GetClients(string mountpoint) {
            return Clients[mountpoint];
        }

        public void AddClient(ClientConnection client) {
            Console.WriteLine($"Add Client: {client.Id}");
            lock(lockObj) {
                this.Clients[client.Mountpoint] = this.Clients[client.Mountpoint].Add(client);
            }
        }

        public void RemoveClient(ClientConnection client) {
            Console.WriteLine($"Remove Client: {client.Id}");
            lock (lockObj) {
                this.Clients[client.Mountpoint] = this.Clients[client.Mountpoint].Remove(client);
            }
        }

        private void ProcessRequests() {
            Listener.Start();

            Console.WriteLine($"Server is listening at {Listener.LocalEndpoint}");

            while (true) {
                var socket = Listener.AcceptSocket();
                new Thread(() => {
                    try {
                        ProcessRequest(socket);
                    } catch (Exception ex) {
                        Console.WriteLine(ex); 
                    }}).Start();
            }
        }

        private void ProcessRequest(Socket socket) {
            var buffer = new byte[1024];
            var bytesCount = socket.Receive(buffer);
            var headersText = Encoding.ASCII.GetString(buffer, 0, bytesCount);

            var parsedHeaders = ParsedHttpHeaders.Parse(headersText);

            if (!parsedHeaders.IsNTRIP) {
                var response = "HTTP/1.0 200 OK\r\nServer: NTRIPCaster\r\nContent-Type: text/plain\r\n\r\nThis is a NTRIP Caster";
                SendToSocket(socket, response);
                socket.Close();
            } else if (parsedHeaders.Mountpoint == "") {
                // Send Sourcetable
                SendSourcetable(socket);
                socket.Close();
            } else if (parsedHeaders.IsSource) {
                ProcessSource(socket, parsedHeaders);
            } else {
                ProcessClient(socket, parsedHeaders);
            }
        }

        private void ProcessSource(Socket socket, ParsedHttpHeaders parsedHeaders) {
            // Check Mountpoint
            var source = Config.Sources.FirstOrDefault(x => x.Mountpoint == parsedHeaders.Mountpoint);
            if (source == null) {
                SendSourcetable(socket);
                socket.Close();
                return;
            }

            //// Check if there is another Server
            //if (Servers[parsedHeaders.Mountpoint] != null) {
            //    SendToSocket(socket, "ERROR - Mountpoint already in use"); // Check behavior
            //    socket.Close();
            //    return;
            //}

            // Check Login
            if (!source.Password.Equals(parsedHeaders.Password, StringComparison.OrdinalIgnoreCase)) {
                SendToSocket(socket, "ERROR - Bad Password");
                socket.Close();
                return;
            }

            // Read Data
            socket.Send(Encoding.ASCII.GetBytes("ICY 200 OK\r\n"));

            Console.WriteLine($"New Server: {socket.RemoteEndPoint}");
            var server = new ServerConnection(socket, parsedHeaders.Mountpoint, this);
            server.StartProcessing();
        }

        private void ProcessClient(Socket socket, ParsedHttpHeaders parsedHeaders) {
            // Check Mountpoint
            var source = Config.Sources.FirstOrDefault(x => x.Mountpoint == parsedHeaders.Mountpoint);
            if (source == null) {
                SendSourcetable(socket);
                socket.Close();
                return;
            }

            // Check Login
            var user = Config.Users.FirstOrDefault(x => x.Name.Equals(parsedHeaders.Username, StringComparison.OrdinalIgnoreCase));
            if (!source.AuthRequired ||
                (user != null && user.Password.Equals(parsedHeaders.Password, StringComparison.OrdinalIgnoreCase) && user.Mountpoints.Contains(parsedHeaders.Mountpoint))) {
                socket.Send(Encoding.ASCII.GetBytes("ICY 200 OK\r\n"));
                socket.SendTimeout = 1000;

                var client = new ClientConnection(socket, parsedHeaders.Mountpoint, parsedHeaders.Username);
                client.StrartProcessing();
                AddClient(client);
            } else {
                SendToSocket(socket, "ERROR - Bad Password");
                socket.Close();
            }
        }

        private void SendSourcetable(Socket socket) {
            var table = new StringBuilder();
            foreach (var source in Config.Sources) {
                table.Append("STR;");
                table.Append(source.Mountpoint); table.Append(";"); // Mountpoint
                table.Append(source.Identifier); table.Append(";"); // Identifier
                table.Append(source.Format); table.Append(";;");  // Format (No details)
                table.Append((int)source.Carrier); table.Append(";"); // Carrier
                table.Append(source.NavSystem); table.Append(";"); // NavSystem
                table.Append(source.Network); table.Append(";"); // Ref-Network
                table.Append(source.Country); table.Append(";"); // Country
                table.Append(source.Latitude.ToString("0.00")); table.Append(";"); // Latitude
                table.Append(source.Longitude.ToString("0.00")); table.Append(";"); // Longitude
                table.Append("0;"); // Client doesn't have to send NMEA
                table.Append("0;"); // Single Base Solution
                table.Append("Unknown;"); // Generator
                table.Append("none;");  // Compression/Encryption
                table.Append(source.AuthRequired ? "B" : "N"); table.Append(";"); // Basic Authentication
                table.Append("N;"); // No fee
                table.Append("9600;"); // Bitrate
                table.Append("\r\n");
            }

            var builder = new StringBuilder();
            builder.Append("SOURCETABLE 200 OK\r\n");
            builder.Append("Server: NTRIP Caster/1.0\r\n");
            builder.Append("Conent-Type: text/plain\r\n");
            builder.Append($"Conent-Length: #{table.Length}\r\n");
            builder.Append("\r\n");
            builder.Append(table.ToString());
            builder.Append("ENDSOURCETABLE\r\n");

            SendToSocket(socket, builder.ToString());
        }

        private void SendToSocket(Socket socket, string str) {
            socket.Send(Encoding.ASCII.GetBytes(str));
        }

        public void Dispose() {
            throw new NotImplementedException();
        }

        class ParsedHttpHeaders {
            public string Mountpoint;
            public string Username;
            public string Password;
            public bool IsSource;
            public string Agent;
            public bool IsNTRIP { get { return Agent.Contains("NTRIP"); } }

            public static ParsedHttpHeaders Parse(string headers) {
                var parsedHeaders = new ParsedHttpHeaders();

                foreach (var header in headers.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)) {
                    if (header.StartsWith("GET ", StringComparison.Ordinal)) {
                        // HTTP GET
                        parsedHeaders.Mountpoint = NormalizeMountpoint(header.Split(new char[] { ' ' })[1]);
                    } else if (header.StartsWith("Authorization:", StringComparison.Ordinal)) {
                        // Basic Auth
                        var authorizationParts = header.Split(new char[] { ' ' });
                        if (authorizationParts.Length == 2 && authorizationParts[0].Equals("Basic", StringComparison.OrdinalIgnoreCase)) {
                            var usernamePassword = DecodeBase64(authorizationParts[1]).Split(new char[] { ':' });
                            if (usernamePassword.Length == 2) {
                                parsedHeaders.Username = usernamePassword[0];
                                parsedHeaders.Password = usernamePassword[1];
                            }
                        }
                    } else if (header.StartsWith("SOURCE", StringComparison.Ordinal)) {
                        // Source
                        parsedHeaders.IsSource = true;
                        var sourceParts = header.Split(new char[] { ' ' });
                        if (sourceParts.Length == 3) {
                            parsedHeaders.Password = sourceParts[1];
                            parsedHeaders.Mountpoint = NormalizeMountpoint(sourceParts[2]);
                        }
                    } else if (header.StartsWith("User-Agent:", StringComparison.Ordinal)) {
                        parsedHeaders.Agent = header.Substring("User-Agent:".Length).TrimStart();
                    } else if (header.StartsWith("Source-Agent:", StringComparison.Ordinal)) {
                        parsedHeaders.Agent = header.Substring("Source-Agent:".Length).TrimStart();
                    } else {
                        Console.WriteLine("ignore header: " + header);
                    }
                }

                return parsedHeaders;
            }

            private static string DecodeBase64(string base64String) {
                var bytes = System.Convert.FromBase64String(base64String);
                return Encoding.UTF8.GetString(bytes);
            }

            private static string NormalizeMountpoint(string mountpoint) {
                if (mountpoint == null) {
                    return String.Empty;
                }
                return mountpoint.ToLowerInvariant().TrimStart(new char[] { '/' });
            }
        }
    }
}
