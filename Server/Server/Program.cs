using System.Net;
using System.Net.Sockets;

namespace EchorServer
{
    /// <summary>
    /// 和客户端部分ByteArray的结构是一样的.
    /// TODO Eddie 需要测试 粘包、半包、线程冲突、大小端问题的处理代码是否生效
    /// </summary>
    public class ByteArray
    {
        public byte[] bytes; // 缓冲区长度

        public int readIdx; // 读取的idx
        public int writeIdx; // 写入的idx
        
        public int length => writeIdx - readIdx;

        public ByteArray(byte[] bytes)
        {
            this.bytes = bytes;
            readIdx = 0;
            writeIdx = bytes.Length;
        }
    }
    
    public class ClientState
    {
        public const int BUFFER_SIZE = 1024;
        public Socket Socket;
        public int bufferCount; // 像客户端一样, 有一个表示下标的角色
        public byte[] ReadBuffer = new byte[BUFFER_SIZE];
        
        public Queue<ByteArray> sendQueue = new Queue<ByteArray>();
        
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
                        // 为啥这里不用BeginReceive
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
                count = clientfd.Receive(state.ReadBuffer, state.bufferCount, ClientState.BUFFER_SIZE - state.bufferCount, SocketFlags.None);
                state.bufferCount += count;
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
            
            
            OnReceiveClientData(clientfd);
            // 这里转发
            // 首先要在这里解决粘包问题 解决方案 用#来表示一条协议结束
            // var recvStr = System.Text.Encoding.UTF8.GetString(state.ReadBuffer, 0, count);
            
            // 客户端和服务端应该是一样的代码吧?
            // var protos = recvStr.Split('#', StringSplitOptions.RemoveEmptyEntries); // 处理粘包问题
            // foreach (var singleProto in protos)
            // {
            //     var split = singleProto.Split('|');
            //     Console.WriteLine("[ReceiveMsg] IP:" + clientfd.RemoteEndPoint + " msg: " + recvStr);
            //
            //     var msgName = split[0];
            //     var msgArgs = split[1];
            //     var funcName = "Msg" + msgName;
            //     var mi = typeof(MsgHandler).GetMethod(funcName);
            //     object[] o = [state, msgArgs];
            //     mi.Invoke(null, o);
            // }
           
            
            // var sendStr = recvStr;
            // var sendBytes = System.Text.Encoding.UTF8.GetBytes(sendStr);
            // foreach (var cs in clients.Values)
            // {
            //     cs.Socket.Send(sendBytes);
            // }
            //
            return true;
        }

        private static void OnReceiveClientData(Socket clientfd)
        {
            var state = clients[clientfd];
            
            // 如果当前socket的缓冲区中的数据量小于2字节, 说明啥也没有
            if (state.bufferCount < 2) // 这时候什么也不做
            {
                return;
            }
            
            // 添加字节序处理
            short bodyLength;
            if (!BitConverter.IsLittleEndian)
            {
                // 传过来的是小端数据 如果当前是大端的机器 那么
                bodyLength = (short)((state.ReadBuffer[1] << 8) | state.ReadBuffer[0]);
            }
            else
            {
                bodyLength = BitConverter.ToInt16(state.ReadBuffer, 0);
            }
            
            // 如果当前socket的缓冲区中的数据量大于2字节, 但是根据这两字节的数据转成的消息体长度 比实际的bufferCount要长 说明这个消息不完整
            if (state.bufferCount < 2 + bodyLength)
            {
                return;
            }
            
            var singleProto = System.Text.Encoding.UTF8.GetString(state.ReadBuffer, 2, bodyLength);
            var split = singleProto.Split('|');
            Console.WriteLine("[ReceiveMsg] IP:" + clientfd.RemoteEndPoint + " msg: " + singleProto);
            
            var msgName = split[0];
            var msgArgs = split[1];
            var funcName = "Msg" + msgName;
            var mi = typeof(MsgHandler).GetMethod(funcName);
            object[] o = [state, msgArgs];
            mi.Invoke(null, o);

            var start = 2 + bodyLength;
            state.bufferCount -= start;
            Array.Copy(state.ReadBuffer, start, state.ReadBuffer, 0, state.bufferCount);
            OnReceiveClientData(clientfd);
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
            var bodyBytes = System.Text.Encoding.UTF8.GetBytes(msg);
            // 在这里标识一下发送数据的长度
            var sendBodyLength = (short)bodyBytes.Length;
            var lenBytes = BitConverter.GetBytes(sendBodyLength);
            if (!BitConverter.IsLittleEndian)
            {
                Console.WriteLine("[Send] Reverse lenBytes");
                lenBytes.Reverse();
            }
            
            var sendBytes = lenBytes.Concat(bodyBytes).ToArray();
            var sendBa = new ByteArray(sendBytes);
            cs.sendQueue.Enqueue(sendBa);
            
            // 如果当前发送队列里面只有一个待发送消息, 就直接把这个消息发送出去
            if (cs.sendQueue.Count == 1)
            {
                cs.Socket.BeginSend(sendBa.bytes, sendBa.readIdx, sendBa.length, 0, SendCallback, cs);
            }
            
            // cs.Socket.Send(sendBytes);
            Console.WriteLine("[SendMsg] IP: " + cs.Socket.RemoteEndPoint + " msg: " + msg);
        }

        /// <summary>
        /// 这我该怎么知道是哪个Socket啊?
        /// </summary>
        /// <param name="ar"></param>
        private static void SendCallback(IAsyncResult ar)
        {
            var clientState = (ClientState)ar.AsyncState;
            
            var sendQueue = clientState.sendQueue; 
            // 拿到了发送数据的长度
            var sendCount = clientState.Socket.EndSend(ar);

            // 首先拿到第一个
            var sendArray = sendQueue.Peek();
            sendArray.readIdx += sendCount;
            if (sendArray.length == 0)
            {
                sendQueue.Dequeue();
                sendQueue.TryPeek(out sendArray);
            }

            // 如果 上一条数据发送不完整, 或者上一条数据发送完整, 队列中有残留的待发送数据
            if (sendArray != null)
            {
                clientState.Socket.BeginSend(sendArray.bytes, sendArray.readIdx, sendArray.length, 0, SendCallback, clientState);
            }

        }
    }    
}