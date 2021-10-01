using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Sockets;

namespace AGWPEAPI
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.BackgroundColor = ConsoleColor.Black;

            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            string text = fvi.ProductName + " " + fvi.FileVersion + " by " + fvi.CompanyName + "\r\n";
            Console.WriteLine(text);
            Console.WriteLine("AGWPE Bridge for Lanchonlh HG-UV98 APRS via Serial over Bluetooth");
            Console.WriteLine(" Author: milokz@gmail.com ");
            Console.WriteLine(" .. loading config.xml .. ");
            Console.WriteLine();

            CONFIG config = CONFIG.Load();

            AGWPEServer server = new AGWPEServer(config.AGW_PORT, config.COM_PORT, config.BAUD_RATE);
            server.MaxClients = config.MAX_CLIENTS;
            try
            {
                server.Start();
            }
            catch (SocketException ex)
            {
                ConsoleColor was = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Socket Error [Port " + server.ServerPort.ToString() + "]: " + ex.Message);
                Console.ForegroundColor = was;
                Console.ReadLine();
                return;
            }
            catch (Exception ex)
            {
                ConsoleColor was = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Error: " + ex.Message);
                Console.ForegroundColor = was;
                Console.ReadLine();
                return;
            };

            Console.WriteLine("AGWPE Bridge Started at {0}", DateTime.Now);
            Console.WriteLine(" -  tcp://localhost:{0}/ - ", server.ServerPort);
            Console.WriteLine(" -  " + server.PortMode + " - ");
            Console.WriteLine(" -  Port 1: " + server.PortDescription);
            Console.WriteLine();
            Console.WriteLine("You can connect it using KMZViewer, APRSWin, UISS, UI-View32");
            Console.WriteLine("Press Enter key to Exit");
            Console.WriteLine("------------------------------ LOG -------------------------------");

            Console.ReadLine();
            server.Stop();
        }
    }    
}
