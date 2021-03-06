﻿/// <summary>
/// 六轴机器人远程故障监测与预警系统v1.0
/// ©韩山师范学院
/// provide by 赵亮(Zeno)
/// </summary>
namespace MessageServer
{
    using MySql.Data.MySqlClient;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;

    public class Server
    {
        public string ipString = "";
        public int port = 8899;                // 服务器端口

        public Socket socket;
        public Print print;                     // 运行时的信息输出方法
        public string receive_data;       //heart
        public int count = 0;
        public Dictionary<string, Socket> clients = new Dictionary<string, Socket>();   // 存储连接到服务器的客户端信息   ip 和socket 
        public Dictionary<string, int> ip = new Dictionary<string, int>();//存储心跳包
        public bool started = false;            // 标识当前是否启动了服务
        public Server(Print print = null, string ipString = null, int port = -1)
        {
            this.print = print;
            if (ipString != null)
                this.ipString = ipString;
            if (port >= 0)this.port = port;
        }

        public Server(Print print = null, string ipString = null, string port = "-1")
        {
            this.print = print;
            if (ipString != null) this.ipString = ipString;

            int port_int = Int32.Parse(port);
            if (port_int >= 0) this.port = port_int;
        }

        /// <summary>
        /// Print用于输出Server的输出信息
        /// </summary>
        public delegate void Print(string info);

        /// <summary>
        /// 启动服务
        /// </summary>
        public void start()
        {
            try
            {
                IPAddress address = IPAddress.Parse(ipString);
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(address, port));   
                socket.Listen(1000);

                if (print != null)
                {
                    try
                    {
                        print("启动服务【" + socket.LocalEndPoint.ToString() + "】成功");
                    }
                    catch
                    { print = null; }
                }
                started = true;
                new Thread(listenClientConnect).Start(socket);  // 在新的线程中监听客户端连接
            }
            catch (Exception exception)
            {
                if (print != null)
                {
                    print("启动服务失败 " + exception.ToString());
                }
                started = false;
            }
        }

        /// <summary>
        /// 监听客户端的连接
        /// </summary>
        private void listenClientConnect(object obj)
        {
            Socket socket = (Socket) obj;
            while (true)
            {
                Socket clientScoket = socket.Accept();
                if (print != null)
                {
                    print("客户端" + clientScoket.RemoteEndPoint.ToString() + "已连接");
                }
                new Thread(receiveData).Start(clientScoket);   // 在新的线程中接收客户端信息

                Thread.Sleep(1000);                            // 延时1秒后，接收连接请求
                if (!started) return;
            }
        }

        /// <summary>
        /// 发送信息
        /// </summary>
        public void Send(string info, string id)
        {
            if (clients.ContainsKey(id))
            {
                Socket socket = clients[id];
                try 
                { 
                    Send(socket, info); 
                }
                catch
                {
                    clients.Remove(id);
                    ip.Remove(id);
                    if (print != null)
                       print("客户端已断开，【" + id + "】");
                }
            }
        }

        /// <summary>
        /// 通过socket发送数据data
        /// </summary>
        private void Send(Socket socket, string data)
        {
            if (socket != null && data != null && !data.Equals(""))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(data);   // 将data转化为byte数组
                socket.Send(bytes);                            
            }
        }

        private string clientIp = "";
        /// <summary>
        /// 输出Server的输出信息到客户端
        /// </summary>
        public void PrintOnClient(string info)
        {
            Send(info, clientIp);
        }

        /// <summary>
        /// 接收通过socket发送过来的数据
        /// </summary>
        private void receiveData(object obj)
        {
           

            Socket socket = (Socket) obj;
            string clientIp = socket.RemoteEndPoint.ToString();                 // 获取客户端标识 ip和端口
            if (!clients.ContainsKey(clientIp))
            {
                clients.Add(clientIp, socket);  // 将连接的客户端socket添加到clients中保存
                ip.Add(clientIp, 0);
            }

            else
            {
                clients[clientIp] = socket;
            }
            while (true)
            {
                try
                {
                    string str = Receive(socket);

                    int res1 = str.IndexOf("$-") + 2;// 协议头
                    int res2 = str.IndexOf("-$");//协议尾
                    int res = res2 - res1;          //协议长
                    receive_data = str;
                    if (!str.Equals(""))
                    {
                        if (print != null)
                        {

                            if (res > 0)                       //符合协议格式
                            {
                                Send("$heart", clientIp);
                                string constr = "server=localhost;User Id=root;password=;Database=hanshi";
                                MySqlConnection mycon = new MySqlConnection(constr);
                                mycon.Open();     //打开数据库
                                string result = str.Substring(res1, res);
                                string[] resultArry = result.Split('&'); //取出协议数据
                                try
                                {
                                    MySqlCommand mycmd = new MySqlCommand("insert into "+resultArry[0]+"(W_info, S_info,Y_info,D_info,roll_info,pitch_info,yaw_info,Time) values ('" + resultArry[1] + "','" + resultArry[2] + "','" + resultArry[3] + "','" + resultArry[4] + "','" + resultArry[5] + "','" + resultArry[6] + "','" + resultArry[7] + "','" + System.DateTime.Now + "');", mycon); //写入记录
                                    mycmd.ExecuteNonQuery();
                                    mycon.Close();
                                    print("【" + clientIp + "】" + str + "写入成功");                                   
                                }
                                catch
                                {
                                    print(str + "数据非法");
                                }
                                foreach (string key in clients.Keys)
                                {
                                    if (ip[key] == 1)
                                    {
                                        Send(str, key);
                                        print("向" + key.ToString() + "转发成功");
                                    }
                                }
                            }
                            else
                            {
                                if (str.Equals("@zeno@"))
                                {
                                    ip[clientIp] = 1;
                                    print("【" + clientIp + "】" + "握手成功！");
                                }
                                else
                                {
                                    print("【" + clientIp + "】" + "不是我方客户端，拒绝!");
                                }
                                str = "";
                            }


                            
                        }
                    }
                }
                catch (Exception exception)
                {
                    if (print != null) print("连接已自动断开，【" + clientIp + "】" + exception.Message);
                    //socket.Shutdown(SocketShutdown.Send);
                    //socket.Close();
                    if (clients.ContainsKey(clientIp))
                    {
                        //clients.Remove(clientIp);
                        ip[clientIp] = 0;
                    }
                    //return;
                }

                if (!started) return;
                Thread.Sleep(100);      // 延时0.2秒后再接收客户端发送的消息
            }
        }
        /// <summary>
        /// 从socket接收数据
        /// </summary>
        private string Receive(Socket socket)
        {
            string data = "";
            byte[] bytes = null;
            int len = socket.Available;
            if (len > 0)
            {
                bytes = new byte[len];
                int receiveNumber = socket.Receive(bytes);
                data = Encoding.UTF8.GetString(bytes, 0, receiveNumber);
            }

            return data;
        }

        /// <summary>
        /// 停止服务
        /// </summary>
        public void stop()
        {
            started = false;
            socket.Close();
        }
    }
}
