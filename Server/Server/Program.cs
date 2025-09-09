using System.Net;
using System.Net.Sockets;

namespace EchorServer
{
    public class ClientState
    {
        public Socket Socket;
        
        public byte[] ReadBuffer = new byte[1024];
        
        public int hp = -100;
        public float x = 0;
        public float y = 0;
        public float z = 0;
        public float eulY = 0;
    }
    
    class MainClass
    {
        private static Socket _listenfd;
        public static Dictionary<Socket, ClientState> clients = new Dictionary<Socket, ClientState>();
        
        public static void Main(string[] args)
        {
            Console.WriteLine("Echo Server started");
            
            _listenfd = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
            IPAddress ipAdr = IPAddress.Parse("127.0.0.1");
            IPEndPoint ipep = new IPEndPoint(ipAdr, 8888);
            _listenfd.Bind(ipep);
            
            _listenfd.Listen(0);
            Console.WriteLine("Listening on " + ipep.ToString());

            // _listenfd.BeginAccept(AcceptCallback, _listenfd);

            var checkRead = new List<Socket>();
            while (true)
            {
                checkRead.Clear();
                checkRead.Add(_listenfd);

                foreach (var socket in clients.Keys)
                {
                    checkRead.Add(socket);
                }
                
                Socket.Select(checkRead, null, null, 1000);
                
                // 检查可读对象
                foreach (var socket in checkRead)
                {
                    if (socket == _listenfd)
                    {
                        ReadListenfd(socket);
                    }
                    else
                    {
                        ReadClientfd(socket);
                    }
                }
            }
        }

        public static void ReadListenfd(Socket listenfd)
        {
            var clientfd = listenfd.Accept();
            Console.WriteLine($"Accept listenfd {clientfd.RemoteEndPoint}");
            var state = new ClientState();
            state.Socket = clientfd;
            clients.Add(clientfd, state);
        }

        public static bool ReadClientfd(Socket clientfd)
        {
            var state = clients[clientfd];
            var count = 0;
            try
            {
                count = clientfd.Receive(state.ReadBuffer);
            }
            catch(SocketException ex)
            {
                var mei = typeof(EventHandler).GetMethod("OnDisconnect");
                object[] ob = [state];
                mei.Invoke(null, ob);
                
                clientfd.Close();
                clients.Remove(clientfd);
                Console.WriteLine("SocketException: " + ex.ToString());
                return false;
            }

            if (count == 0)
            {
                var mei = typeof(EventHandler).GetMethod("OnDisconnect");
                object[] ob = [state];
                mei.Invoke(null, ob);
                
                clientfd.Close();
                clients.Remove(clientfd);
                Console.WriteLine("Client fd closed");
                return false;
            }
            
            // 这里转发
            // 首先要在这里解决粘包问题 解决方案 用#来表示一条协议结束
            var recvStr = System.Text.Encoding.UTF8.GetString(state.ReadBuffer, 0, count);
            var protos = recvStr.Split('#', StringSplitOptions.RemoveEmptyEntries); // 处理粘包问题
            foreach (var singleProto in protos)
            {
                var split = singleProto.Split('|');
                Console.WriteLine("[ReceiveMsg] IP:" + clientfd.RemoteEndPoint + " msg: " + recvStr);
            
                var msgName = split[0];
                var msgArgs = split[1];
                var funcName = "Msg" + msgName;
                var mi = typeof(MsgHandler).GetMethod(funcName);
                object[] o = [state, msgArgs];
                mi.Invoke(null, o);
            }
           
            
            // var sendStr = recvStr;
            // var sendBytes = System.Text.Encoding.UTF8.GetBytes(sendStr);
            // foreach (var cs in clients.Values)
            // {
            //     cs.Socket.Send(sendBytes);
            // }
            //
            return true;
        }        
        // private static void AcceptCallback(IAsyncResult ar)
        // {
        //     try
        //     {
        //         // 接收到客户端发起的连接请求, 在服务端会生成一个与之对应的socket
        //         // 把这个与之对应的Socket存起来
        //         Console.WriteLine("[Server] Accepting clients...");
        //         var listenfd = (Socket)ar.AsyncState;
        //         var clientfd = listenfd.EndAccept(ar);
        //         
        //         var state = new ClientState();
        //         state.Socket = clientfd;
        //         
        //         clients.Add(clientfd, state);
        //         
        //         clientfd.BeginReceive(state.ReadBuffer, 0, 1024, 0, ReceiveCallback, state);
        //         
        //         listenfd.BeginAccept(AcceptCallback, listenfd);
        //
        //     }
        //     catch (SocketException e)
        //     {
        //         Console.WriteLine("[Server] Error: {0}", e);
        //     }
        // }
        //
        // private static void ReceiveCallback(IAsyncResult ar)
        // {
        //     try
        //     {
        //         // 会把客户端的Socket发送过来吗?
        //         // 服务端就知道这是哪个客户端了.
        //         var state = (ClientState)ar.AsyncState;
        //         var clientfd = state.Socket;
        //         var count = clientfd.EndReceive(ar);
        //         if (count == 0)
        //         {
        //             clientfd.Close();
        //             clients.Remove(state.Socket);
        //             Console.WriteLine("[Server] Disconnected");
        //             return;
        //         }
        //         
        //         var recvStr = System.Text.Encoding.UTF8.GetString(state.ReadBuffer, 0, count);
        //         var sendStr = clientfd.RemoteEndPoint + ":" + recvStr;
        //         var sendBytes = System.Text.Encoding.UTF8.GetBytes(sendStr);
        //
        //         foreach (var s in clients.Values)
        //         {
        //             s.Socket.Send(sendBytes);
        //         }
        //         
        //         Console.WriteLine($"[Server] 接收来自客户端{clientfd.RemoteEndPoint} Recieved {0} bytes", recvStr.Length);
        //         clientfd.BeginReceive(state.ReadBuffer, 0, 1024, 0, ReceiveCallback, state);
        //         
        //     }
        //     catch (SocketException e)
        //     {
        //         Console.WriteLine("[Server] Error: {0}", e);
        //     }
        // }

        public static void Send(ClientState cs, string msg)
        {
            if (!msg.EndsWith("#"))
            {
                msg += "#";
            }
            var sendBytes = System.Text.Encoding.UTF8.GetBytes(msg);
            cs.Socket.Send(sendBytes);
            Console.WriteLine("[SendMsg] IP: " + cs.Socket.RemoteEndPoint + " msg: " + msg);
        }
    }    
}