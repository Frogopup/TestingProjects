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
    // �������� ��� StatusChanged event
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

        // ����������� ��� ��������� ��������� � �������
        public StatusChangedEventArgs(string strEventMsg)
        {
            EventMsg = strEventMsg;
        }
    }

    // ���� ������� ��������� ��� �������� ����������, ������� �� �������� � ����� ��������
    public delegate void StatusChangedEventHandler(object sender, StatusChangedEventArgs e);

    public class ChatServer
    {
        public static Hashtable htUsers = new Hashtable(30);
        public static Hashtable htConnections = new Hashtable(30);
        int port;

        // ������� � ��� �������� ����� ���������� �����, ����� ������������ �����������, ���������� � �.�.
        public static event StatusChangedEventHandler StatusChanged;
        private static StatusChangedEventArgs e;
        private Thread thrListener;
        private Socket serverSocket;
        bool ServRunning = false;

        public ChatServer(int port)
        {
            this.port = port;
        }

        // �������� ������������ � ���-�������
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

        // ���������� Status Changed �������
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

        // ��������� ��������� �� ������
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

        // ������� �������� ���������
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
            // ��������� listener'�
            serverSocket.Listen(20);
            ServRunning = true;

            // ��������� ����� �����, �������� listener'�
            thrListener = new Thread(KeepListening);
            thrListener.IsBackground = true;
            thrListener.Start();
        }

        private void KeepListening()
        {
            Socket clientSocket;
            while (ServRunning == true)
            {
                // ��������� �����������
                clientSocket = serverSocket.Accept();
                // ������� ��������� �����������
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

        // ����������� ������ ��������� TCP ����������
        public Connection(Socket Con)
        {
            clientSocket = Con;
            thrSender = new Thread(AcceptClient);
            thrSender.IsBackground = true;
            thrSender.Start();
        }

        // Network ������

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

        // ����������� ��� �������� ������ �������
        private void AcceptClient()
        {
            // ��������� ���������� �������
            currUser = Net_RecieveData();
            // �������� ����� �� �������
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

                    string readPath = Environment.CurrentDirectory + @"\history.txt"; // ���� � �������
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
