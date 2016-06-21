using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NTRIPCaster.Configs;

namespace NTRIPCaster {

    public class NTRIPServer {
        private Config Config;
        private TcpListener Listener;
        private Dictionary<string, IImmutableList<Socket>> Clients;

        public NTRIPServer(Config config) {
            this.Config = config;
            this.Listener = new TcpListener(IPAddress.Parse(config.ServerAddress), config.ServerPort);
            this.Clients = new Dictionary<string, IImmutableList<Socket>>();

            foreach (var source in config.Sources) {
                this.Clients.Add(source.Mountpoint, ImmutableList.Create<Socket>());
            }
        }

        public void Start() {
            new Thread(() => ProcessRequests()).Start();
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
                var response = "HTTP/1.0 200 OK\r\nServer: NTRIPCaster\r\nContent-Type: text/plain\r\n\r\nHello";
                SendToSocket(socket, response);
                socket.Close();
            } else if (parsedHeaders.Mountpoint == "") {
                // Send Sourcetable
                SendSourcetable(socket);
                socket.Close();
            } else if (parsedHeaders.IsSource) {
                // Check Mountpoint
                var source = Config.Sources.FirstOrDefault(x => x.Mountpoint == parsedHeaders.Mountpoint);
                if (source == null) {
                    SendSourcetable(socket);
                    socket.Close();
                    return;
                }

                // Check Login
                if (!source.Password.Equals(parsedHeaders.Password, StringComparison.OrdinalIgnoreCase)) {
                    SendToSocket(socket, "ERROR - Bad Password");
                    socket.Close();
                    return;
                }

                // Read Data
                socket.Send(Encoding.ASCII.GetBytes("ICY 200 OK\r\n"));
                ProcessSource(socket, parsedHeaders.Mountpoint);
            } else {
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
                    lock (Clients) {
                        Clients[parsedHeaders.Mountpoint] = Clients[parsedHeaders.Mountpoint].Add(socket);
                    }
                } else {
                    SendToSocket(socket, "ERROR - Bad Password");
                    socket.Close();
                }   
            }
        }

        private void ProcessSource(Socket socket, string mountpoint) {
            var buffer = new byte[1024];
            socket.Blocking = true;
            socket.ReceiveTimeout = 10000;
            var socketsToRemove = new ConcurrentBag<Socket>();

            try {
                while (socket.Connected) {
                    var bytesCount = socket.Receive(buffer);

                    Parallel.ForEach(Clients[mountpoint], client => {
                        try {
                            client.Send(buffer, bytesCount, SocketFlags.None);
                        } catch (SocketException) {
                            try {
                                client.Close();
                            } finally {
                                socketsToRemove.Add(client);
                            }
                        }
                    });

                    if (socketsToRemove.Count > 0) {
                        lock (Clients[mountpoint]) {
                            Clients[mountpoint] = Clients[mountpoint].RemoveRange(socketsToRemove);
                        }
                        socketsToRemove = new ConcurrentBag<Socket>();
                    }
                }
            } catch (SocketException) {
            } finally {
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
                return mountpoint.TrimStart(new char[] { '/' });
            }
        }
    }
}
