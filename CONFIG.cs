using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace AGWPEAPI
{
    public class CONFIG
    {
        public int AGW_PORT = 8000;
        public string COM_PORT = "COM1";
        public int BAUD_RATE = 9600;
        public ushort MAX_CLIENTS = 5;

        public static CONFIG Load()
        {
            try
            {
                return XMLSaved<CONFIG>.LoadFile(XMLSaved<int>.GetCurrentDir() + @"\config.xml");
            }
            catch
            {
                CONFIG n = new CONFIG();
                try { n.Save(); } catch { };
                return n;
            };
        }

        public void Save()
        {
            XMLSaved<CONFIG>.Save(XMLSaved<int>.GetCurrentDir() + @"\config.xml", this);
        }
    }
}
