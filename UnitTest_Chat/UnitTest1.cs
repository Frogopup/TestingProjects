using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChatServer;
using System.Net.Sockets;
using System.Text;
using System.Net;

namespace UnitTest_Chat
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void Message_ConnectTest()
        {
            Socket testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            ChatServer.ChatServer cs = new ChatServer.ChatServer(7777);
            cs.StartListening();

            testSocket.Connect("127.0.0.1", 7777);
            Net_SendData(testSocket, "TestUsername");
            string resp = Net_RecieveData(testSocket);


            //string resp = Net_RecieveData(testSocket);
            Assert.AreEqual(resp, "1");
        }


        private void Net_SendData(Socket s, string data)
        {
            s.Send(Encoding.Unicode.GetBytes(data));
        }

        private string Net_RecieveData(Socket s)
        {
            StringBuilder sb = new StringBuilder();
            byte[] buffer = new byte[256];
            int bytesRecieved = 0;
            do
            {
                bytesRecieved = s.Receive(buffer);
                if (bytesRecieved == 0) return null;
                sb.Append(Encoding.Unicode.GetString(buffer, 0, bytesRecieved));
            } while (s.Available > 0);

            return sb.ToString();
        }
    }
}
