using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using System.Net;

namespace MessageServer
{
    public partial class MessageServer : Form
    {
        Server server;
        static string hostName = Dns.GetHostName();
        IPAddress[] iphost = Dns.GetHostAddresses(hostName);
        
        public MessageServer()
        {
            InitializeComponent();
            for (int i = 0; i < iphost.Length; i++)
            {
                if (iphost[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    this.textBox_Ip.Text = iphost[i].ToString();
                }
            }
            }
           

        // 启动服务
        private void button_start_Click(object sender, EventArgs e)
        {
            if (server == null)
                server = new Server(SeverPrint, textBox_Ip.Text, textBox_Port.Text);
            if (!server.started)
                server.start();
        }

        // 服务器端输出信息
        private void SeverPrint(string info)
        {
            if (textBox_showing.IsDisposed) server.print = null;
            else
            {
                if (textBox_showing.InvokeRequired)
                {
                    Server.Print F = new Server.Print(SeverPrint);
                    this.Invoke(F, new object[] { info });
                }
                else
                {
                    if (info != null)
                    {
                        textBox_showing.SelectionColor = Color.Green;
                        textBox_showing.AppendText(info);
                        textBox_showing.AppendText(Environment.NewLine);
                        textBox_showing.ScrollToCaret();
                    }
                }
            }
        }

        // 发送信息到客户端
        private void button_send_Click(object sender, EventArgs e)
        {
            if (server != null) server.Send(textBox_send.Text, comboBox_clients.Text);
        }

        private void MessageServer_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (server != null)
                server.stop();
        }

        // 选择客户端时，更新客户端列表信息
        private void comboBox_clients_DropDown(object sender, EventArgs e)
        {
            if (server != null)
            {
                List<string> clientList = server.clients.Keys.ToList<string>();
                comboBox_clients.DataSource = clientList;
            }
        }

        private void MessageServer_Load(object sender, EventArgs e)
        {
            this.timer1.Start();
        }
    }
}
