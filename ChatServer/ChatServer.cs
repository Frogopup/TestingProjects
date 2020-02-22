using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Collections;
using System.Linq;

namespace ChatServer
{
    // Аргумент для StatusChanged event
    public class StatusChangedEventArgs : EventArgs
    {
        private string EventMsg;

        public string EventMessage
        {
            get
            {
                return EventMsg;
            }
            set
            {
                EventMsg = value;
            }
        }

        // Конструктор для установки сообщения о событии
        public StatusChangedEventArgs(string strEventMsg)
        {
            EventMsg = strEventMsg;
        }
    }

    // Этот делегат необходим для указания параметров, которые мы передаем с нашим событием
    public delegate void StatusChangedEventHandler(object sender, StatusChangedEventArgs e);

    public class ChatServer
    {
        public static Hashtable htUsers = new Hashtable(30);
        public static Hashtable htConnections = new Hashtable(30);
        int port;

        // Событие и его аргумент будут уведомлять форму, когда пользователь подключился, отключился и т.д.
        public static event StatusChangedEventHandler StatusChanged;
        private static StatusChangedEventArgs e;
        private Thread thrListener;
        private Socket serverSocket;
        bool ServRunning = false;

        public ChatServer(int port)
        {
            this.port = port;
        }

        // Добавить пользователя в хеш-таблицы
        public static void AddUser(Socket User, string strUsername)
        {
            ChatServer.htUsers.Add(strUsername, User);
            ChatServer.htConnections.Add(User, strUsername);
            SendAdminMessage(htConnections[User] + " has joined us");
        }

        public static void RemoveUser(Socket User)
        {
            if (htConnections[User] != null)
            {
                SendAdminMessage(htConnections[User] + " has left us");
                ChatServer.htUsers.Remove(ChatServer.htConnections[User]);
                ChatServer.htConnections.Remove(User);
            }
        }

        // Реализация Status Changed события
        public static void OnStatusChanged(StatusChangedEventArgs e)
        {
            StatusChanged?.Invoke(null, e);
        }

        public static void AddToHistory(string Message)
        {
            string writePath = Environment.CurrentDirectory + @"\history.txt";
            using (StreamWriter sw = new StreamWriter(writePath, true, Encoding.Unicode))
            {
                sw.WriteLine(Message);
            }

        }

        // Отправить сообщение от админа
        public static void SendAdminMessage(string Message)
        {
            e = new StatusChangedEventArgs("Administrator: " + Message);
            OnStatusChanged(e);

            Socket[] sockets = new Socket[htUsers.Count];
            ChatServer.htUsers.Values.CopyTo(sockets, 0);
            for (int i = 0; i < sockets.Length; i++)
            {
                    if (Message.Trim() == "" || !sockets[i].Connected || sockets[i] == null)
                    {
                        continue;
                    }
                    try { sockets[i].Send(Encoding.Unicode.GetBytes("Administrator: " + Message)); }
                    catch { sockets[i].Close(); RemoveUser(sockets[i]); }
            }
        }

        // Обычная отправка сообщения
        public static void SendMessage(string From, string Message)
        {
            e = new StatusChangedEventArgs(DateTime.Now + " | " + From + " says: " + Message);
            OnStatusChanged(e);

            Socket[] sockets = new Socket[htUsers.Count];
            ChatServer.htUsers.Values.CopyTo(sockets, 0);
            string historyString = DateTime.Now + " | " + From + " says: " + Message;
            AddToHistory(historyString);
            for (int i = 0; i < sockets.Length; i++)
            {
                    if (Message.Trim() == "" || sockets[i] == null)
                    {
                        continue;
                    }
                    try { sockets[i].Send(Encoding.Unicode.GetBytes(DateTime.Now + " | " + From + " says: " + Message)); }
                    catch { sockets[i].Close(); RemoveUser(sockets[i]); return; }
            }
        }


        public void StartListening()
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try { serverSocket.Bind(new IPEndPoint(IPAddress.Any, port)); }
            catch (Exception ex)
            {
                e = new StatusChangedEventArgs(ex.Message);
                OnStatusChanged(e);
                return;
            }
            // Запускаем listener'а
            serverSocket.Listen(20);
            ServRunning = true;

            // Запускаем новый поток, хостящий listener'а
            thrListener = new Thread(KeepListening);
            thrListener.IsBackground = true;
            thrListener.Start();
        }

        private void KeepListening()
        {
            Socket clientSocket;
            while (ServRunning == true)
            {
                // Принимаем подключение
                clientSocket = serverSocket.Accept();
                // Создаем экземпляр подключения
                Connection newConnection = new Connection(clientSocket);
            }
        }
    }


    class Connection
    {
        Socket clientSocket;
        private Thread thrSender;
        private string currUser;
        private string strResponse;

        // Конструктор класса принимает TCP соединение
        public Connection(Socket Con)
        {
            clientSocket = Con;
            thrSender = new Thread(AcceptClient);
            thrSender.IsBackground = true;
            thrSender.Start();
        }

        // Network методы

        private void Net_SendData(string data)
        {
            try { clientSocket.Send(Encoding.Unicode.GetBytes(data)); }
            catch { CloseConnection(); }
        }

        private string Net_RecieveData()
        {
            StringBuilder sb = new StringBuilder();
            byte[] buffer = new byte[256];
            int bytesRecieved = 0;
            do
            {
                try { bytesRecieved = clientSocket.Receive(buffer); }
                catch { CloseConnection(); return null; }
                if (bytesRecieved == 0) { CloseConnection(); return null; }
                sb.Append(Encoding.Unicode.GetString(buffer, 0, bytesRecieved));
            } while (clientSocket.Available > 0);

            return sb.ToString();
        }



        private void CloseConnection()
        {
            if (clientSocket != null)
            {
                if (clientSocket.Connected)
                {
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Disconnect(false);
                    clientSocket.Close();
                }
            }
        }

        // Выполняется при принятие нового клиента
        private void AcceptClient()
        {
            // Считываем информацию клиента
            currUser = Net_RecieveData();
            // Получили ответ от клиента
            if (currUser == null || currUser != "")
            {
                if (ChatServer.htUsers.Contains(currUser) == true)
                {
                    Net_SendData("0|This username already exists.");
                    CloseConnection();
                    return;
                }
                else if (currUser == "Administrator")
                {
                    Net_SendData("0|This username is reserved.");
                    CloseConnection();
                    return;
                }
                else
                {
                    Net_SendData("1");

                    string readPath = Environment.CurrentDirectory + @"\history.txt"; // путь к истории
                    if (!File.Exists(readPath)) File.Create(readPath).Close();
                    string history = string.Join(Environment.NewLine, File.ReadLines(readPath).Reverse().Take(20).Reverse());
                    ChatServer.AddUser(clientSocket, currUser);
                    Net_SendData(history);
                }
            }
            else
            {
                CloseConnection();
                return;
            }

            while ((strResponse = Net_RecieveData()) != "")
            {
                if (strResponse == null)
                {
                    ChatServer.RemoveUser(clientSocket);
                }
                else
                {
                    ChatServer.SendMessage(currUser, strResponse);
                }
            }
        }
    }
}
