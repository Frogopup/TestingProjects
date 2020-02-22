using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace ChatServer
{
    public partial class Form1 : Form
    {
        private delegate void UpdateStatusCallback(string strMessage);

        public Form1()
        {
            InitializeComponent();
        }

        private void btnListen_Click(object sender, EventArgs e)
        {
            int port;
            if(!int.TryParse(textBox_port.Text, out port) || port < 0 || port > 65535)
            {
                MessageBox.Show("Invalid port number.");
                return;
            }

            ChatServer mainServer = new ChatServer(port);
            // Привязка
            ChatServer.StatusChanged += new StatusChangedEventHandler(mainServer_StatusChanged);
            // Запуск прослушивания
            mainServer.StartListening();
            // Отображение прослушивания
            btnListen.Enabled = false;
            textBox_port.Enabled = false;
            txtLog.AppendText("Monitoring for connections...\r\n");
        }

        public void mainServer_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            // Вызов метода обновления статуса
            this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { e.EventMessage });
        }

        private void UpdateStatus(string strMessage)
        {
            // Запись статуса в лог
            txtLog.AppendText(strMessage + "\r\n");
        }
    }
}