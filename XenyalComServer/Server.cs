using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

public class User
{
    public User(string name, Socket soc)
    {
        Name = name;
        Soc = soc;
    }

    public string Name;
    public Socket Soc;
    public MessageListenerThread MessageThread;
}

public class MessageListenerThread
{
    public MessageListenerThread(User user)
    {
        User = user;
    }

    public bool Active = true;
    public User User;

    public async void WaitForMessages()
    {
        while(Active)
        {
            byte[] bytes = new Byte[1024];
            Console.WriteLine($"Waiting for Message by {User.Name}...");
            string data = null;

            if (User != null)
            {
                if (User.Soc.Connected)
                {
                    int userbytesRec = User.Soc.Receive(bytes);
                    data += Encoding.ASCII.GetString(bytes, 0, userbytesRec);
                    await XenyalServer.ProcessData(data);
                }
                else break;
            }
        }
        Console.WriteLine($"{User.Name}'s Message Thread ended.");
    }
}

public class XenyalServer
{
    public static int Port;
    public static DataType dataType = DataType.None;
    public static User[] Users = new User[5];
    public static Socket[] socs = new Socket[7];
    public static Socket newestSoc;
    public static int newestSocIndex;
    public static TcpListener listener;
    public static byte[] bytes = new Byte[1024];

    public static int Main(String[] args)
    {
        Console.Title = "Xenyal Server";

        Console.Write("Port: ");
        int.TryParse(Console.ReadLine(), out Port);

        string name = (args.Length < 1) ? Dns.GetHostName() : args[0];
        listener = new TcpListener(Dns.GetHostAddresses(name)[3], Port);
        Console.WriteLine($"Room IP: {Dns.GetHostAddresses(name)[3]}");
        Console.CursorVisible = false;
        Start();
        while (true) { }
    }

    public static void Start()
    {
        listener.Start();
        AcceptConnections();
        while(true)
        {
            if(Users.Length > 0)
            {
                foreach (var item in Users)
                {
                    if(item != null)
                    {
                        if (item.MessageThread == null)
                        {
                            MessageListenerThread mlt = new MessageListenerThread(item);

                            Thread Messages = new Thread(new ThreadStart(mlt.WaitForMessages));
                            item.MessageThread = mlt;
                            Messages.Start();
                            Console.WriteLine($"Created new Message Thread for {item.Name}");
                        }
                    }
                }
            }
        }
    }

    public static async void AcceptConnections()
    {
        while(true)
        {
            Console.WriteLine("Waiting for Connection...");
            for (int i = 1; i < socs.Length; i++)
            {
                if (socs[i] == null)
                {
                    socs[i] = await listener.AcceptSocketAsync();
                    string data = null;
                    int bytesRec = socs[i].Receive(bytes);
                    newestSoc = socs[i];
                    newestSocIndex = i;
                    data += Encoding.ASCII.GetString(bytes, 0, bytesRec);
                    await ProcessData(data);
                    break;
                }
            }
        }
    }

    public static Task ProcessData(string data)
    {
        while (true)
        {
            if (data.IndexOf("<LOG>") > -1)
            {
                dataType = DataType.Info;
                data = data.Replace("<LOG>", "");
                break;
            }
            else if (data.IndexOf("<MSG>") > -1)
            {
                dataType = DataType.Message;
                data = data.Replace("<MSG>", "");
                break;
            }
            else if (data.IndexOf("<CON>") > -1)
            {
                dataType = DataType.Connect;
                data = data.Replace("<CON>", "");
                break;
            }
            else if (data.IndexOf("<DIS>") > -1)
            {
                dataType = DataType.Disconnect;
                data = data.Replace("<DIS>", "");
                break;
            }
            else if (data.IndexOf("<CMD>") > -1)
            {
                dataType = DataType.Command;
                data = data.Replace("<CMD>", "");
                break;
            }
        }

        switch (dataType)
        {
            case DataType.Info:
                Console.WriteLine(data);
                break;
            case DataType.Message:
                Console.WriteLine(data);
                SendToAll(Encoding.ASCII.GetBytes($"({DateTime.Now.Hour}:{DateTime.Now.Minute}) {data}"));
                break;
            case DataType.Connect:
                if(UserCount() >= 5)
                {
                    Console.WriteLine($"{data} failed to connect: Server Full.");
                    newestSoc.Send(Encoding.ASCII.GetBytes($"The Server you tried to connect to is full.<KICK>"));
                    newestSoc = null;
                    socs[newestSocIndex] = null;
                    break;
                }
                if (!UserExists(data))
                {
                    Console.WriteLine($"{data} connected. ({UserCount()}/5)");
                    AddUser(data, newestSoc);
                    newestSoc = null;
                    SendToAll(Encoding.ASCII.GetBytes($"{data} connected. ({UserCount()}/5)"));
                }
                else
                {
                    Console.WriteLine($"{data} failed to connect: Name taken.");
                    newestSoc.Send(Encoding.ASCII.GetBytes($"A User with the name {data} is already connected to this IP.<KICK>"));
                    newestSoc = null;
                }
                break;
            case DataType.Disconnect:
                Console.WriteLine($"{data} disconnected.");
                RemoveUser(data);
                SendToAll(Encoding.ASCII.GetBytes($"{data} disconnected."));
                break;
            case DataType.Command:
                string[] segments = data.Split("=>");
                Console.WriteLine($"{segments[0]} issued command {segments[1]}.");
                switch (segments[1])
                {
                    case "help":
                        SendToUser(Encoding.ASCII.GetBytes("Available Commands:\n" +
                            "- /help     = Displays all available commands.\n" +
                            "- /userlist = Displays all connected Users.\n" +
                            "- /exit     = Disconnects you."), segments[0]);
                        break;
                    case "userlist":
                        int i = 0;
                        string userList = string.Empty;
                        foreach (var item in Users)
                        {
                            if (item != null)
                            {
                                if (!item.Soc.Connected)
                                {
                                    RemoveUser(item.Name);
                                    Console.WriteLine($"User {item.Name} disconnected.");
                                }
                                else
                                {
                                    userList += $" {item.Name},";
                                    i++;
                                }
                            }
                        }
                        if (userList.EndsWith(',')) userList = userList.Remove(userList.Length - 1, 1);
                        SendToUser(Encoding.ASCII.GetBytes($"{i} Users Connected:{userList}"), segments[0]);
                        break;
                    default:
                        SendToUser(Encoding.ASCII.GetBytes($"Invalid command \"{segments[1]}\""), segments[0]);
                        break;
                }
                break;
            default:
                Console.WriteLine($"Received invalid data: {data}");
                break;
        }
        return Task.CompletedTask;
    }

    public static int SendToAll(byte[] bytes)
    {
        int i = 0;
        foreach (var item in Users)
        {
            if(item != null)
            {
                if(!item.Soc.Connected)
                {
                    RemoveUser(item.Name);
                    Console.WriteLine($"User {item.Name} disconnected.");
                }
                else
                {
                    item.Soc.Send(bytes);
                    i++;
                }
            }
        }
        Console.WriteLine($"Sent {bytes.Length} bytes to {i} Users");
        return i;
    }

    public static int UserCount()
    {
        int i = 0;
        foreach (var item in Users)
        {
            if (item != null)
            {
                i++;
            }
        }
        return i;
    }

    public static bool UserExists(string name)
    {
        foreach (var item in Users)
        {
            if (item != null)
            {
                if (item.Name == name)
                {
                    if (!item.Soc.Connected)
                    {
                        RemoveUser(item.Name);
                        Console.WriteLine($"User {item.Name} disconnected.");
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public static bool SendToUser(byte[] bytes, string name)
    {
        foreach (var item in Users)
        {
            if (item != null)
            {
                if(item.Name == name)
                {
                    if (!item.Soc.Connected)
                    {
                        RemoveUser(item.Name);
                        Console.WriteLine($"User {item.Name} disconnected.");
                        return false;
                    }
                    else
                    {
                        item.Soc.Send(bytes);
                        Console.WriteLine($"Sent {bytes.Length} bytes to User {item.Name}");
                        return true;
                    }
                }
            }
        }
        Console.WriteLine($"User {name} not found.");
        return false;
    }

    public static bool AddUser(string name, Socket soc)
    {
        int i = 0;
        foreach (var item in Users)
        {
            if(item == null)
            {
                Console.WriteLine($"User{i} \"{name}\" connected.");
                Users[i] = new User(name, soc);
                return true;
            }
            i++;
        }
        Console.WriteLine($"User \"{name}\" couldn't connect. (No empty slots)");
        return false;
    }

    public static bool RemoveUser(string name)
    {
        int i = 0;
        foreach (var item in Users)
        {
            if(item != null)
            {
                if (item.Name == name)
                {
                    Console.WriteLine($"User \"{name}\" disconnected.");
                    item.MessageThread.Active = false;
                    item.Soc.Close();
                    socs[i] = null;
                    Users[i] = null;
                    return true;
                }
            }
            i++;
        }
        Console.WriteLine($"User \"{name}\" not found.");
        return false;
    }
}

public enum DataType
{
    None,
    Info,
    Connect,
    Disconnect,
    Message,
    Command
}