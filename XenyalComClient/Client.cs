using System;
using YayaLib;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using XenyalComClient;
using System.Text;
using System.Collections.Generic;

namespace XenyalComClient
{
    class Client
    {
        static List<string> MessageHistory = new List<string> { };
        static string Username;
        static string IP;
        static int Port;
        static Socket soc;
        static bool connected = false;
        static bool disconnecting = false;

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(System.IntPtr hWnd, int cmdShow);

        [DllImport("user32.dll")]
        public static extern int DeleteMenu(IntPtr hMenu, int nPosition, int wFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler(CtrlType sig);
        static EventHandler handler;

        static void Main(string[] args)
        {
            IntPtr handle = GetConsoleWindow();
            IntPtr sysMenu = GetSystemMenu(handle, false);
            DeleteMenu(sysMenu, 0xF000, 0x00000000);
            DeleteMenu(sysMenu, 0xF030, 0x00000000);
            ShowWindow(handle, 1);
            handler += new EventHandler(Handler);
            SetConsoleCtrlHandler(handler, true);

            Console.Title = "Xenyal Terminal";
            Console.WindowHeight = 31;
            Console.BufferHeight = Console.WindowHeight;
            Console.BufferWidth = Console.WindowWidth;
            DrawHeader();

            Login();
        }

        static void DrawHeader()
        {
            Console.CursorTop = 0;
            for (int i = 0; i < Console.BufferWidth; i++)
            {
                Console.Write("#");
            }
            Text.WriteLineCenter("__    __   ____    __    _   __    __   _____      __   ");
            Text.WriteLineCenter("\\ \\  / /  |  __|  |  \\  | |  \\ \\  / /  /  _  \\    |  |  ");
            Text.WriteLineCenter(" \\ \\/ /   | |_    |   \\ | |   \\ \\/ /  /  / \\  \\   |  |  ");
            Text.WriteLineCenter("  >  <    |  _|   | |\\ \\| |    |  |  |  |___|  |  |  |  ");
            Text.WriteLineCenter(" / /\\ \\   | |__   | | \\   |    |  |  |   ___   |  |  |__");
            Text.WriteLineCenter("/_/  \\_\\  |____|  |_|  \\__|    |__|  |__|   |__|  |_____|");
            Text.WriteLineCenter("Communication Client");
            for (int i = 0; i < Console.BufferWidth; i++)
            {
                Console.Write("#");
            }
            Console.WriteLine("\n");
            Console.CursorTop = 29;
            for (int i = 0; i < Console.BufferWidth; i++)
            {
                Console.Write("#"); 
            }
        }
        
        static void Login()
        {
            bool choosingName = true;
            while (choosingName)
            {
                Text.ClearLines(9, 29);
                Console.SetCursorPosition(50, 15);
                Console.Write("Username: ");
                Username = Console.ReadLine();
                Text.ClearLine(15);
                if (Username.Length > 10)
                {
                    Text.WriteLineCenter("Too long.", 15);
                    Console.ReadKey();
                    Username = string.Empty;
                }
                if (Username.Length == 0)
                {
                    Text.WriteLineCenter("Too short.", 15);
                    Console.ReadKey();
                    Username = string.Empty;
                }
                if (Username != string.Empty) choosingName = false;
            }
            Username = Username.Trim();
            Text.WriteLineCenter("Name Accepted.", 15);
            Console.ReadKey();
            bool connecting = true;
            while(connecting)
            {
                Text.ClearLines(10, 29);
                Console.SetCursorPosition(50, 15);
                Console.Write("Server IP: ");
                IP = Console.ReadLine();
                int.TryParse(IP.Split(':')[1], out Port);
                IP = IP.Split(':')[0];
                try
                {
                    IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                    IPAddress.TryParse(IP, out IPAddress ipAddress);
                    //IPAddress ipAddress = ipHostInfo.AddressList[0];
                    IPEndPoint remoteEP = new IPEndPoint(ipAddress, Port);

                    soc = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        soc.Connect(remoteEP);

                        Text.ClearLines(9, 29);
                        Console.CursorTop = 9;
                        Console.WriteLine($"Currently connected to {soc.RemoteEndPoint}");

                        soc.Send(Encoding.ASCII.GetBytes($"{Username}<CON>"));
                        connected = true;
                        disconnecting = false;
                        ReceiveDataAsync();
                        while (connected)
                        {
                            string messageText = Console.ReadLine();
                            Text.ClearLine(Console.CursorTop - 1);
                            if (messageText.Length > 0)
                            {
                                
                                if(messageText.StartsWith('/'))
                                {
                                    if (messageText == "/exit")
                                    {
                                        disconnecting = true;
                                        soc.Send(Encoding.ASCII.GetBytes($"{Username}<DIS>"));
                                        LogOut();
                                        return;
                                    }
                                    soc.Send(Encoding.ASCII.GetBytes($"{Username}=>{messageText.Remove(0,1)}<CMD>"));
                                }
                                else
                                {
                                    soc.Send(Encoding.ASCII.GetBytes($"{Username} said {messageText}<MSG>"));
                                }
                            }
                            else UpdateMessageDisplay();
                        }
                    }
                    catch (ArgumentNullException ane)
                    {
                        Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
                    }
                    catch (SocketException se)
                    {
                        Console.WriteLine("SocketException : {0}", se.ToString());
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Unexpected exception : {0}", e.ToString());
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
                Console.ReadKey();
            }
            Console.ReadKey();
        }

        static void LogOut()
        {
            MessageHistory.Clear();
            connected = false;
            soc.Close();
            Console.ReadKey();
            Login();
        }

        static async void ReceiveDataAsync()
        {
            while (!disconnecting)
            {
                byte[] bytes = new byte[1024];
                try
                {
                    int bytesRec = await soc.ReceiveAsync(new ArraySegment<byte>(bytes), SocketFlags.None);
                    string dataRec = Encoding.ASCII.GetString(bytes, 0, bytesRec);

                    if (dataRec.EndsWith("<KICK>"))
                    {
                        dataRec = dataRec.Replace("<KICK>", "");
                        MessageHistory.Add(dataRec);
                        UpdateMessageDisplay();
                        LogOut();
                        return;
                    }

                    string[] messagesRec = dataRec.Split("\n");
                    if (messagesRec.Length > 0)
                    {
                        foreach (var item in messagesRec)
                        {
                            MessageHistory.Add(item);
                        }   
                    }
                    else MessageHistory.Add(dataRec);
                    UpdateMessageDisplay();
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                    break;
                }
            }
        }
        
        static void UpdateMessageDisplay()
        {
            Text.ClearLines(10, 29);
            Console.CursorTop = 10;
            for (int i = Math.Max(MessageHistory.Count - 18, 0); i < MessageHistory.Count; i++)
            {
                Console.WriteLine(MessageHistory[i]);
            }
            Console.Write(" > ");
        }

        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private static bool Handler(CtrlType sig)
        {
            switch (sig)
            {
                case CtrlType.CTRL_LOGOFF_EVENT:
                case CtrlType.CTRL_SHUTDOWN_EVENT:
                case CtrlType.CTRL_CLOSE_EVENT:
                    soc.Send(Encoding.ASCII.GetBytes($"{Username}<DIS>"));
                    return true;
                case CtrlType.CTRL_C_EVENT:
                default:
                    return false;
            }
        }
    }
}
