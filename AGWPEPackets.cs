using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Sockets;

namespace AGWPEAPI
{
    /// <summary>
    ///     AGWPE TCP/IP Socket Frame
    ///     https://on7lds.net/42/sites/default/files/AGWPEAPI.HTM
    /// </summary>
    public class AGWFrame
    {
        /// <summary>
        ///     Header Size, 36 bytes
        /// </summary>
        public const int HEADER_SIZE = 36;  
      
        public  byte   AGWPEPort  = 0;              // 00..00 // offset 00
        public  byte[] Reserver01 = new byte[03];   // 01..03 // offset 01
        public  byte   DataKind = 0;                // 04..04 // offset 04
        public  byte[] Reserver02 = new byte[01];   // 05..05 // offset 05
        public  byte   PID        = 0;              // 06..06 // offset 06
        public  byte[] Reserver03 = new byte[01];   // 07..07 // offset 07
        public  byte[] CallFrom   = new byte[10];   // 08..17 // offset 08
        public  byte[] CallTo     = new byte[10];   // 18..27 // offset 18
        public  byte[] DataLen    = new byte[04];   // 28..31 // offset 28
        public  byte[] Reserver04 = new byte[04];   // 32..35 // offset 32   
     
        // Custom Payload Data
        public List<byte> PayLoad = new List<byte>();

        public byte[] PAYLOAD
        {
            get
            {
                return PayLoad.ToArray();
            }
            set
            {
                this.DATALEN = value.Length;
                PayLoad.Clear();
                PayLoad.AddRange(value);
            }
        }

        public AGWFrame(char DATAKIND)
        {
            this.DATAKIND = DATAKIND;
        }

        public AGWFrame(byte[] packet)
        {
            this.HEADER = packet;
        }

        public byte[] HEADER
        {
            get
            {
                List<byte> res = new List<byte>();
                res.Add(AGWPEPort);
                res.AddRange(Reserver01);
                res.Add(DataKind);
                res.AddRange(Reserver02);
                res.Add(PID);
                res.AddRange(Reserver03);
                res.AddRange(CallFrom);
                res.AddRange(CallTo);
                res.AddRange(DataLen);
                res.AddRange(Reserver04);
                return res.ToArray();
            }
            set
            {
                AGWPEPort = value[0];
                Reserver01[0] = value[1]; Reserver01[1] = value[2]; Reserver01[2] = value[3];
                DataKind = value[4];
                Reserver02[0] = value[5];
                PID = value[6];
                Reserver03[0] = value[7];
                for (int i = 0; i < 10; i++) CallFrom[i] = value[8 + i];
                for (int i = 0; i < 10; i++) CallTo[i] = value[18 + i];
                for (int i = 0; i < 04; i++) DataLen[i] = value[28 + i];
                for (int i = 0; i < 04; i++) Reserver04[i] = value[32 + i];
                int dl = DATALEN;
                PayLoad.Clear();
                if(dl > 0)
                {
                    byte[] arr = new byte[dl];
                    Array.Copy(value, 36, arr, 0, dl);
                    PayLoad.AddRange(arr);
                };
            }
        }

        public string CALLFROM
        {
            get
            {
                return System.Text.Encoding.ASCII.GetString(CallFrom).Trim('\0').Trim();
            }
            set
            {
                List<byte> res = new List<byte>();
                res.AddRange(System.Text.Encoding.ASCII.GetBytes(value));
                while(res.Count > 10) res.RemoveAt(10);
                while (res.Count < 10) res.Add(0);
                CallFrom = res.ToArray();
            }
        }

        public string CALLTO
        {
            get
            {
                return System.Text.Encoding.ASCII.GetString(CallTo).Trim('\0').Trim();
            }
            set
            {
                List<byte> res = new List<byte>();
                res.AddRange(System.Text.Encoding.ASCII.GetBytes(value));
                while (res.Count > 10) res.RemoveAt(10);
                while (res.Count < 10) res.Add(0);
                CallTo = res.ToArray();
            }
        }

        public char DATAKIND
        {
            get
            {
                return (char)DataKind;
            }
            set
            {
                DataKind = (byte)value;
            }
        }

        public int DATALEN
        {
            get
            {
                return BitConverter.ToInt32(DataLen, 0);
            }
            set
            {
                DataLen = BitConverter.GetBytes(value);
            }
        }

        public int TOTALLEN
        {
            get
            {
                return HEADER_SIZE + DATALEN;
            }
        }

        public byte[] ToPacket()
        {
            List<byte> res = new List<byte>();
            res.AddRange(HEADER);
            res.AddRange(PayLoad);
            return res.ToArray();
        }

        public override string ToString()
        {
            return String.Format("{0}-{1}: {2} -> {3} - {4} bytes", new object[] { AGWPEPort, DATAKIND, CALLFROM, CALLTO, DATALEN });
        }
    }

    /// <summary>
    ///     Socket Client Info
    /// </summary>
    public class ClientInfo
    {
        public ulong ID;
        public TcpClient Socket;
        public bool receiveRAW = false;
        public bool receiveMON = false;
        public string Callsign = "NOCALL";

        public ClientInfo(ulong ID, TcpClient Socket)
        {
            this.ID = ID;
            this.Socket = Socket;
        }
    }
}
