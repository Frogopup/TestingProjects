using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace ChatClient
{
    public partial class Form1 : Form
    {
        private string UserName = "Unknown";
        private Socket socket;
        // Обновление формы с сообщением из другого потока
        private delegate void UpdateLogCallback(string strMessage);
        private delegate void CloseConnectionCallback(string strReason);
        private Thread thrMessaging;
        private IPAddress ipAddr;
        private int port;
        private bool Connected;

        public Form1()
        {
            // Закрывая приложение - сначала дисконектаем юзера
            Application.ApplicationExit += new EventHandler(OnApplicationExit);
            InitializeComponent();
        }

        // Событие на закрытие
        public void OnApplicationExit(object sender, EventArgs e)
        {
            if (Connected == true)
            {
                Connected = false;
                Net_ShutdownSocket();
                socket.Close();
            }
        }
        // Network методы

        private void Net_SendData(string data)
        {
            try { socket.Send(Encoding.Unicode.GetBytes(data)); }
            catch (Exception ex) { CloseConnection(ex.Message); }
        }

        private string Net_RecieveData()
        {
            StringBuilder sb = new StringBuilder();
            byte[] buffer = new byte[256];
            int bytesRecieved = 0;
            do
            {
                try { bytesRecieved = socket.Receive(buffer); }
                catch (Exception ex) { CloseConnection(ex.Message); return null; }
                if (bytesRecieved == 0) { CloseConnection("Connection lost."); return null; }
                sb.Append(Encoding.Unicode.GetString(buffer, 0, bytesRecieved));
            } while (socket.Available > 0);

            return sb.ToString();
        }

        private void Net_ShutdownSocket()
        {
            if (socket != null && socket.Connected)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Disconnect(false);
                socket.Close();
            }
        }


        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (Connected == false)
            {
                InitializeConnection();
            }
            else
            {
                CloseConnection("Disconnected at user's request.");
            }
        }

        private void InitializeConnection()
        {
            if (!IPAddress.TryParse(txtIp.Text, out ipAddr)) { MessageBox.Show("Invalid IP address."); return; }
            if (!int.TryParse(txtPort.Text, out port) || port < 0 || port > 65535) { MessageBox.Show("Invalid prot number."); return; }
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try { socket.Connect(new IPEndPoint(ipAddr, port)); }
            catch(Exception ex) { CloseConnection(ex.Message); return; }

            Connected = true;
            UserName = txtUser.Text.Equals(string.Empty) ? "Unknown" : txtUser.Text;

            // Включаем/откючаем необходимые поля
            txtIp.Enabled = false;
            txtPort.Enabled = false;
            txtUser.Enabled = false;
            txtMessage.Enabled = true;
            btnSend.Enabled = true;
            btnConnect.Text = "Disconnect";

            // Отправка имени пользователя
            Net_SendData(UserName);

            // Запуск потока для получения сообщений
            thrMessaging = new Thread(new ThreadStart(ReceiveMessages));
            thrMessaging.Start();
        }

        private void ReceiveMessages()
        {
            // Получаем ответ от сервера
            string ConResponse = Net_RecieveData(); 
            if (ConResponse == null) return;
            if (ConResponse[0] == '1')
            {
                this.Invoke(new UpdateLogCallback(this.UpdateLog), new object[] { "Connected Successfully!" });
            }
            else
            {
                string Reason = "Not Connected: ";
                // Вытаскиваем причину отключения
                Reason += ConResponse.Substring(2, ConResponse.Length - 2);
                this.Invoke(new CloseConnectionCallback(this.CloseConnection), new object[] { Reason });
                return;
            }
            while (Connected)
            {
                ConResponse = Net_RecieveData();
                if (ConResponse == null) return;
                this.Invoke(new UpdateLogCallback(this.UpdateLog), new object[] { ConResponse });
            }
        }

        private void UpdateLog(string strMessage)
        {
            txtLog.AppendText(strMessage + "\r\n");
        }

        private void CloseConnection(string Reason)
        {
            txtLog.AppendText(Reason + "\r\n");
            txtIp.Enabled = true;
            txtPort.Enabled = true;
            txtUser.Enabled = true;
            txtMessage.Enabled = false;
            btnSend.Enabled = false;
            btnConnect.Text = "Connect";


            Connected = false;
            Net_ShutdownSocket();
        }

        // Отправка написанного сообщения
        private void SendMessage()
        {
            if (txtMessage.Lines.Length >= 1)
            {
                Net_SendData(txtMessage.Text);
                txtMessage.Lines = null;
            }
            txtMessage.Text = "";
        }

        // Отправка по нажатию
        private void btnSend_Click(object sender, EventArgs e)
        {
            SendMessage();
        }

        // И по Enter'у
        private void txtMessage_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
            {
                SendMessage();
            }
        }
    }
}