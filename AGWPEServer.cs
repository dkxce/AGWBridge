using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Sockets;

namespace AGWPEAPI
{
    /// <summary>
    ///     AGWPE Server
    /// </summary>
    public class AGWPEServer : SimpleServersPBAuth.ThreadedTCPServer, ax25kiss.AX25Handler
    {
        private ax25kiss.KISSTNC kiss = null;
        private string serial = "COM1";
        private int baud = 9600;
        private Dictionary<ulong, ClientInfo> clients = new Dictionary<ulong, ClientInfo>();
        private string portDescription = "KISS TNC via SERIAL on COM0";

        public AGWPEServer(int port, string serial) : base(port)
        {
            this.serial = serial;
            this.kiss = new ax25kiss.KISSTNC(this.serial, this.baud);
            this.kiss.onPacket = this;
            this.portDescription = "KISS TNC via SERIAL on " + this.serial + ":" + this.baud.ToString();
        }

        public AGWPEServer(int port, string serial, int baud) : base(port)
        {
            this.serial = serial;
            this.baud = baud;
            this.kiss = new ax25kiss.KISSTNC(this.serial, this.baud);
            this.kiss.onPacket = this;
            this.portDescription = "KISS TNC via SERIAL on " + this.serial + ":" + this.baud.ToString();
        }

        public string PortDescription { get { return portDescription; } }
        public string PortMode { get { return kiss.Mode.ToString() + " " + this.serial + " " + this.baud.ToString(); } }
        public override void Start() { base.Start(); kiss.Start(); }
        public override void Stop() { kiss.Stop(); base.Stop(); }

        protected override void GetClient(TcpClient Client, ulong id)
        {
            string cip = ((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString();
            int cport = ((IPEndPoint)Client.Client.RemoteEndPoint).Port;
            Console.WriteLine("Client connected {0}:{1} at {2}", cip, cport, DateTime.Now);

            // ADD CLIENT
            lock (clients) clients.Add(id, new ClientInfo(id, Client));

            List<byte> Body = new List<byte>();
            List<AGWFrame> Packets = new List<AGWFrame>();
            int bRead = -1;
            int receivedBytes = 0;
            int rCounter = 0;
            bool loop = true;

            try
            {
                while (loop)
                {
                    // READ SOCKET
                    while ((Client.Available > 0) && ((bRead = Client.GetStream().ReadByte()) >= 0)) { receivedBytes++; Body.Add((byte)bRead); };

                    // PARSE DATA
                    if (Body.Count > 0)
                    {
                        while (Body.Count >= 36)
                        {
                            AGWFrame frame = new AGWFrame(Body.ToArray());
                            Body.RemoveRange(0, frame.TOTALLEN);
                            Packets.Add(frame);
                        };
                        Body.Clear();
                    };

                    // READ PACKETS
                    if (Packets.Count > 0)
                    {
                        foreach (AGWFrame p in Packets)
                        {
                            ConsoleColor was = Console.ForegroundColor;
                            Console.Write("<< ");
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.Write(p.DATAKIND);
                            Console.ForegroundColor = was;
                            Console.WriteLine(" from {0}:{1} at {2}", cip, cport, DateTime.Now);

                            if ((p.DATALEN > 0) && (p.DATAKIND != 'K') && (p.DATAKIND != 'V'))
                            {
                                Console.Write(" - ");
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine("{0}", System.Text.Encoding.ASCII.GetString(p.PAYLOAD)); ;
                            };
                            Console.ForegroundColor = was;

                            switch (p.DATAKIND)
                            {
                                case 'R': Response_R(Client, id, p); break; // VERSION
                                case 'X': Response_X(Client, id, p); break; // Reg Callsign
                                case 'x': Response_x(Client, id, p); break; // Unreg Callsign
                                case 'G': Response_G(Client, id, p); break; // Port Info
                                case 'g': Response_g(Client, id, p); break; // Port Capatibles
                                case 'H': Response_H(Client, id, p); break; // HEARD
                                case 'Y': Response_Y(Client, id, p); break; // Waiting
                                case 'y': Response_y(Client, id, p); break; // Waiting
                                case 'K': Receive_K(Client, id, p); break;  // SEND RAW DATA
                                case 'V': Receive_V(Client, id, p); break;  // SEND VIA DATA
                                case 'm': lock (clients) clients[id].receiveMON = true; break; // Receive MON
                                case 'k': lock (clients) clients[id].receiveRAW = true; break; // Receive RAW                                
                                default: break;
                            };
                        };

                        Packets.Clear();
                    };

                    // CHECK CONNECTED
                    if (!isRunning) loop = false;
                    if (rCounter >= 200) // 10s
                    {
                        try
                        {
                            if (!IsConnected(Client)) loop = false;
                            rCounter = 0;
                        }
                        catch { loop = false; };
                    };
                    rCounter++;

                    // NEXT
                    System.Threading.Thread.Sleep(50);
                };
            }
            catch (Exception ex) { };

            // REMOVE CLIENT
            lock (clients) clients.Remove(id);

            Console.WriteLine("Client disconnected {0}:{1} at {2}", cip, cport, DateTime.Now);
        }

        // Register
        private void Response_R(TcpClient Client, ulong id, AGWFrame p)
        {
            AGWFrame f = new AGWFrame('R');
            f.DATALEN = 8;
            f.PayLoad.Add(0xD0);
            f.PayLoad.Add(0x07);
            f.PayLoad.Add(0x00);
            f.PayLoad.Add(0x00);
            f.PayLoad.Add(0x14);
            f.PayLoad.Add(0x00);
            f.PayLoad.Add(0x00);
            f.PayLoad.Add(0x00);
            byte[] d = f.ToPacket();
            Client.GetStream().Write(d, 0, d.Length);
            Client.GetStream().Flush();

            //string cip = ((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString();
            //int cport = ((IPEndPoint)Client.Client.RemoteEndPoint).Port;
            //Console.WriteLine("Write R to {0}:{1} at {2}", cip, cport, DateTime.Now);
        }

        // Port Info
        private void Response_G(TcpClient Client, ulong id, AGWFrame p)
        {
            AGWFrame f = new AGWFrame('G');
            f.PAYLOAD = System.Text.Encoding.ASCII.GetBytes("1;Port1 " + portDescription + ";");
            byte[] d = f.ToPacket();
            Client.GetStream().Write(d, 0, d.Length);
            Client.GetStream().Flush();

            //string cip = ((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString();
            //int cport = ((IPEndPoint)Client.Client.RemoteEndPoint).Port;
            //Console.WriteLine("Write G 1;Port1 {3} to {0}:{1} at {2}", cip, cport, DateTime.Now, PortDescription);
        }

        // Port Capatibles
        private void Response_g(TcpClient Client, ulong id, AGWFrame p)
        {
            AGWFrame f = new AGWFrame('g');
            f.DATALEN = 10;
            f.PayLoad.Add(0x00);
            f.PayLoad.Add(0xFF);
            f.PayLoad.Add(0x00);
            f.PayLoad.Add(0x00);
            f.PayLoad.Add(0x00);
            f.PayLoad.Add(0x00);
            f.PayLoad.Add(0x00);
            f.PayLoad.Add(0x00);
            f.PayLoad.Add(0x00);
            f.PayLoad.Add(0x00);
            byte[] d = f.ToPacket();
            Client.GetStream().Write(d, 0, d.Length);
            Client.GetStream().Flush();

            //string cip = ((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString();
            //int cport = ((IPEndPoint)Client.Client.RemoteEndPoint).Port;
            //Console.WriteLine("Write g to {0}:{1} at {2}", cip, cport, DateTime.Now);
        }

        // Register Callsign
        private void Response_X(TcpClient Client, ulong id, AGWFrame p)
        {
            AGWFrame f = new AGWFrame('X');
            f.PAYLOAD = new byte[1] { 0x01 };
            byte[] d = f.ToPacket();
            Client.GetStream().Write(d, 0, d.Length);
            Client.GetStream().Flush();
            lock (clients) clients[id].Callsign = f.CALLFROM;

            //string cip = ((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString();
            //int cport = ((IPEndPoint)Client.Client.RemoteEndPoint).Port;
            //Console.WriteLine("Write X to {0}:{1} at {2}", cip, cport, DateTime.Now);
        }

        // Unregister Callsign
        private void Response_x(TcpClient Client, ulong id, AGWFrame p)
        {
            AGWFrame f = new AGWFrame('X');
            f.PAYLOAD = new byte[1] { 0x01 };
            byte[] d = f.ToPacket();
            Client.GetStream().Write(d, 0, d.Length);
            Client.GetStream().Flush();
            lock (clients) clients[id].Callsign = "NOCALL";

            //string cip = ((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString();
            //int cport = ((IPEndPoint)Client.Client.RemoteEndPoint).Port;
            //Console.WriteLine("Write X to {0}:{1} at {2}", cip, cport, DateTime.Now);
        }

        // Get HEARD
        private void Response_H(TcpClient Client, ulong id, AGWFrame p)
        {
            AGWFrame f = new AGWFrame('H');
            byte[] d = f.ToPacket();
            Client.GetStream().Write(d, 0, d.Length);
            Client.GetStream().Flush();

            //string cip = ((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString();
            //int cport = ((IPEndPoint)Client.Client.RemoteEndPoint).Port;
            //Console.WriteLine("Write H to {0}:{1} at {2}", cip, cport, DateTime.Now);
        }

        // Get Wait
        private void Response_Y(TcpClient Client, ulong id, AGWFrame p)
        {
            AGWFrame f = new AGWFrame('Y');
            f.PAYLOAD = BitConverter.GetBytes((int)0);
            byte[] d = f.ToPacket();
            Client.GetStream().Write(d, 0, d.Length);
            Client.GetStream().Flush();

            //string cip = ((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString();
            //int cport = ((IPEndPoint)Client.Client.RemoteEndPoint).Port;
            //Console.WriteLine("Write Y to {0}:{1} at {2}", cip, cport, DateTime.Now);
        }

        // Get Wait
        private void Response_y(TcpClient Client, ulong id, AGWFrame p)
        {
            AGWFrame f = new AGWFrame('y');
            f.PAYLOAD = BitConverter.GetBytes((int)0);
            byte[] d = f.ToPacket();
            Client.GetStream().Write(d, 0, d.Length);
            Client.GetStream().Flush();

            //string cip = ((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString();
            //int cport = ((IPEndPoint)Client.Client.RemoteEndPoint).Port;
            //Console.WriteLine("Write y to {0}:{1} at {2}", cip, cport, DateTime.Now);
        }

        // Get DATA
        private void ReceivePacket(TcpClient Client, ulong id, string source, string dest, string via, string packet, string ptype)
        {
            string cip = ((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString();
            int cport = ((IPEndPoint)Client.Client.RemoteEndPoint).Port;
            ConsoleColor was = Console.ForegroundColor;
            Console.Write("<< ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(String.Format("{4}: {0}>{1}{2}:{3}", new object[] { source, dest, (String.IsNullOrEmpty(via) ? "" : ",") + via, packet, ptype }));
            Console.ForegroundColor = was;
            Console.WriteLine(" from {0}:{1} at {2}", cip, cport, DateTime.Now);            

            if (kiss.Connected)
            {
                kiss.Send(dest, source, String.IsNullOrEmpty(via) ? null : via.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries), packet);
                was = Console.ForegroundColor;
                Console.Write("}} ");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write("A: {0} ", String.Format("{0}>{1}{2}:{3}", new object[] { source, dest, (String.IsNullOrEmpty(via) ? "" : ",") + via, packet }));
                Console.ForegroundColor = was;
                Console.WriteLine("to {0}:{1} at {2}", serial, baud, DateTime.Now);                
            };
        }

        // Receive RAW Packet
        private void Receive_K(TcpClient Client, ulong id, AGWFrame p)
        {
            // RAW DATA
            byte[] data = new byte[p.DATALEN - 1];
            Array.Copy(p.PAYLOAD, 1, data, 0, data.Length);
            ax25kiss.Packet packet = new ax25kiss.Packet(data);

            Regex rx = new Regex(@"(?<from>[^>]*)>(?<to>[^:]*):(?<data>.*)", RegexOptions.None);
            Match mx = rx.Match(packet.ToString());
            if (!mx.Success) return;
            string[] tv = mx.Groups["to"].Value.Split(new char[] { ',' }, StringSplitOptions.None);
            string via = "";
            List<string> vwas = new List<string>();
            if (tv.Length > 1)
                for (int i = 1; i < tv.Length; i++)
                    if (!vwas.Contains(via))
                    {
                        vwas.Add(via);
                        via += (via.Length > 0 ? "," : "") + tv[i];
                    };
            ReceivePacket(Client, id, mx.Groups["from"].Value, tv[0], via, mx.Groups["data"].Value.Trim('\0').Trim(), "K");
        }

        // Receive VIA Packet
        private void Receive_V(TcpClient Client, ulong id, AGWFrame p)
        {
            // VIA DATA            
            int num = p.PAYLOAD[0];
            int idx = 1;
            List<string> via = new List<string>();
            string VIA = "";
            if (num > 0)
            {
                for (int i = 0; i < num; i++)
                {
                    string v = System.Text.Encoding.ASCII.GetString(p.PAYLOAD, idx, 10);
                    idx += 10;
                    string s = v.Trim('\0').Trim();
                    via.Add(s);
                    VIA += (VIA.Length > 0 ? "," : "") + s;
                };
            };
            string packet = System.Text.Encoding.ASCII.GetString(p.PAYLOAD, idx, p.PAYLOAD.Length - idx);
            ReceivePacket(Client, id, p.CALLFROM, p.CALLTO, VIA, packet.Trim('\0').Trim(), "V");
        }        

        // Send RAW K
        private void Send_RAW_K(TcpClient Client, ulong id, string source, string dest, string via, string packet)
        {
            try
            {
                byte[] kissDataFrame = ax25kiss.Packet.KissDataFrame(dest, source, String.IsNullOrEmpty(via) ? null : via.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries), ax25kiss.Packet.AX25_CONTROL_APRS, ax25kiss.Packet.AX25_PROTOCOL_NO_LAYER_3, System.Text.Encoding.ASCII.GetBytes(packet));
                ax25kiss.Packet p1 = new ax25kiss.Packet(kissDataFrame);
                List<byte> res = new List<byte>(); res.Add(0); res.AddRange(p1.bytes());
                AGWFrame f = new AGWFrame('K');
                f.CALLFROM = source;
                f.CALLTO = dest;
                f.PAYLOAD = res.ToArray();
                byte[] d = f.ToPacket();
                Client.GetStream().Write(d, 0, d.Length);
                Client.GetStream().Flush();

                string cip = ((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString();
                int cport = ((IPEndPoint)Client.Client.RemoteEndPoint).Port;

                ConsoleColor was = Console.ForegroundColor;
                Console.Write(">> ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("K: {0} ", String.Format("{0}>{1}{2}:{3}", new object[] { source, dest, (String.IsNullOrEmpty(via) ? "" : ",") + "", packet }));
                Console.ForegroundColor = was;
                Console.WriteLine("to {0}:{1} at {2}", cip, cport, DateTime.Now);                
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            };
        }

        // Send Unproto U/VIA
        private void Send_Unproto_U(TcpClient Client, ulong id, string source, string dest, string via, string packet)
        {
            try
            {
                AGWFrame f = new AGWFrame('U');
                f.CALLFROM = source;
                f.CALLTO = dest;
                string data = packet;
                string txt = String.Format(" 1:Fm {0} To {1} {5}<UI pid=F0 Len={2} >[{3}]\r{4}", new object[] { f.CALLFROM, f.CALLTO, data.Length + 1, DateTime.Now.ToString("HH:mm:ss"), data + "\r", String.IsNullOrEmpty(via) ? "" : "Via " + via + " " });
                f.PAYLOAD = System.Text.Encoding.ASCII.GetBytes(txt);
                byte[] d = f.ToPacket();

                Client.GetStream().Write(d, 0, d.Length);
                Client.GetStream().Flush();

                string cip = ((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString();
                int cport = ((IPEndPoint)Client.Client.RemoteEndPoint).Port;
                ConsoleColor was = Console.ForegroundColor;
                Console.Write(">> ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("U: {0} ", String.Format("{0}>{1}{2}:{3}", new object[] { f.CALLFROM, f.CALLTO, (String.IsNullOrEmpty(via) ? "" : ",") + "", data }));
                Console.ForegroundColor = was;
                Console.WriteLine("to {0}:{1} at {2}", cip, cport, DateTime.Now);                
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            };
        }

        // Receive Packet From Serial
        public void handlePacket(ax25kiss.Packet packet)
        {
            string spacket = packet.ToString();
            ConsoleColor was = Console.ForegroundColor;
            Console.Write("{{ ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("A: {0} ", spacket);
            Console.ForegroundColor = was;
            Console.WriteLine("from {0}:{1} at {2}", serial, baud, DateTime.Now);            

            Regex rx = new Regex(@"(?<from>[^>]*)>(?<to>[^:]*):(?<data>.*)", RegexOptions.None);
            Match mx = rx.Match(spacket);
            if (!mx.Success) return;

            string[] tv = mx.Groups["to"].Value.Split(new char[] { ',' }, StringSplitOptions.None);
            string via = "";
            List<string> vwas = new List<string>();
            if (tv.Length > 1) 
                for (int i = 1; i < tv.Length; i++)
                    if(!vwas.Contains(via))
                    {
                        vwas.Add(via);
                        via += (via.Length > 0 ? "," : "") + tv[i];
                    };

            lock (clients)
            {
                foreach (KeyValuePair<ulong, ClientInfo> client in clients)
                {
                    bool rr = false;
                    bool rm = false;
                    lock (clients)
                    {
                        if (client.Value.receiveRAW) rr = true;
                        if (client.Value.receiveMON) rm = true;
                    };
                    if (rr) Send_RAW_K(client.Value.Socket, client.Key, mx.Groups["from"].Value, tv[0], via, mx.Groups["data"].Value.Trim('\0').Trim());
                    if (rm) Send_Unproto_U(client.Value.Socket, client.Key, mx.Groups["from"].Value, tv[0], via, mx.Groups["data"].Value.Trim('\0').Trim());
                };
            };
        }
    }
}
