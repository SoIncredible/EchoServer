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
        public const int DefaultBufferSize = 1024;
        
        // 初始长度
        public int initSize;
        public int capacity; // 实际的容量
        
        public byte[] bytes; // 缓冲区长度

        public int readIdx; // 读取的idx
        public int writeIdx; // 写入的idx
        
        /// <summary>
        /// buffer中有效的数据长度
        /// </summary>
        public int ValidDataLength => writeIdx - readIdx;
        public int remain => capacity - writeIdx;

        public ByteArray(byte[] bytes)
        {
            this.bytes = bytes;
            readIdx = 0;
            writeIdx = bytes.Length;
            capacity = bytes.Length;
        }

        public ByteArray(int bufferLength = DefaultBufferSize)
        {
            initSize = bufferLength;
            capacity = bufferLength;
            bytes = new byte[bufferLength];
            readIdx = 0;
            writeIdx = 0;
        }

        /// <summary>
        /// 给ByteArray扩容
        /// </summary>
        /// <param name="newSize"></param>
        public void Resize(int newSize)
        {
            if (newSize < initSize) return; // 不合法操作
            if (newSize < ValidDataLength) return; // 不能比当前buffer中有效数据长度还要小
            var n = 1;
            while(n < newSize) n *= 2; // 以 2的n次幂扩张数组
            capacity = n;
            var newBytes = new byte[capacity];
            Array.Copy(bytes, readIdx, newBytes, 0, ValidDataLength);
            bytes = newBytes;
            writeIdx = ValidDataLength;
            readIdx = 0;
        }

        public void CheckAndMoveBytes()
        {
            if (ValidDataLength < 8)
            {
                MoveBytes();
            }
        }
        
        /// <summary>
        /// 移动数组内容
        /// </summary>
        public void MoveBytes()
        {
            Array.Copy(bytes, readIdx, bytes, 0, ValidDataLength);
            writeIdx = ValidDataLength;
            readIdx = 0;
        }

        /// <summary>
        /// 写入操作
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public int Write(byte[] buffer, int offset, int count)
        {
            CheckAndMoveBytes();
            // 需要检查扩容
            if (count > remain)
            {
                Resize(ValidDataLength + count);
            }
            Array.Copy(buffer, offset, bytes, writeIdx, count);
            writeIdx += count;
            return count;
        }

        /// <summary>
        /// 读取操作
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public int Read(byte[] buffer, int offset, int count)
        {
            count = Math.Min(ValidDataLength, count);
            Array.Copy(bytes, readIdx, buffer, offset, count);
            readIdx += count;   
            CheckAndMoveBytes();
            return count;
        }
        
        /// <summary>
        /// 读一个 Int16
        /// </summary>
        /// <returns></returns>
        public short ReadInt16()
        {
            short bodyLength;
            // 大小端问题处理
            if (!BitConverter.IsLittleEndian)
            {
                // 自己处理
                bodyLength = (short)(bytes[readIdx + 1] << 8 | bytes[readIdx]);
            }
            else
            {
                bodyLength = BitConverter.ToInt16(bytes, readIdx);
            }
            readIdx += 2;
            CheckAndMoveBytes();
            return bodyLength;
        }

        /// <summary>
        /// 读一个 Int32
        /// </summary>
        /// <returns></returns>
        public int ReadInt32()
        {
            int bodyLength;

            if (!BitConverter.IsLittleEndian)
            {
                bodyLength = bytes[readIdx + 3] << 24 | bytes[readIdx + 2] << 16 | bytes[readIdx + 1] << 8 | bytes[readIdx];
            }
            else
            {
                bodyLength = BitConverter.ToInt32(bytes, readIdx);
            }
            
            readIdx += 4;
            CheckAndMoveBytes();
            return bodyLength;
        }
    }
    
    public class ClientState
    {
        public const int BUFFER_SIZE = 1024;
        public Socket Socket;
        // public int bufferCount; // 像客户端一样, 有一个表示下标的角色
        // public byte[] ReadBuffer = new byte[BUFFER_SIZE];
        public ByteArray ReadBuffer = new ByteArray();
        
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
                count = clientfd.Receive(state.ReadBuffer.bytes, state.ReadBuffer.writeIdx, state.ReadBuffer.remain, SocketFlags.None);
                state.ReadBuffer.writeIdx += count;
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

            if (state.ReadBuffer.remain < 8)
            {
                state.ReadBuffer.MoveBytes();
                state.ReadBuffer.Resize(state.ReadBuffer.capacity * 2);
            }
            
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
            if (state.ReadBuffer.ValidDataLength < 2) // 这时候什么也不做
            {
                return;
            }
            
            // 添加字节序处理
            short bodyLength = state.ReadBuffer.ReadInt16();
            
            // 如果当前socket的缓冲区中的数据量大于2字节, 但是根据这两字节的数据转成的消息体长度 比实际的bufferCount要长 说明这个消息不完整
            if (state.ReadBuffer.ValidDataLength < bodyLength)
            {
                return;
            }
            
            var readBuffer = new byte[bodyLength];
            state.ReadBuffer.Read(readBuffer, 0, bodyLength);
            
            var singleProto = System.Text.Encoding.UTF8.GetString(readBuffer);
            var split = singleProto.Split('|');
            Console.WriteLine("[ReceiveMsg] IP:" + clientfd.RemoteEndPoint + " msg: " + singleProto);
            
            var msgName = split[0];
            var msgArgs = split[1];
            var funcName = "Msg" + msgName;
            var mi = typeof(MsgHandler).GetMethod(funcName);
            object[] o = [state, msgArgs];
            mi.Invoke(null, o);
            
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
            
            // var sendBa = new ByteArray(sendBytes);
            var sendBa = new ByteArray();
            sendBa.Write(sendBytes, 0, sendBytes.Length);
            
            int count;

            lock (cs.sendQueue)
            {
                cs.sendQueue.Enqueue(sendBa);
                count = cs.sendQueue.Count;
            }
            
            // 如果当前发送队列里面只有一个待发送消息, 就直接把这个消息发送出去
            if (count == 1)
            {
                cs.Socket.BeginSend(sendBa.bytes, sendBa.readIdx, sendBa.ValidDataLength, 0, SendCallback, cs);
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
            ByteArray sendArray;
            lock (sendQueue)
            {
                sendArray = sendQueue.Peek();
            }
            sendArray.readIdx += sendCount;
            if (sendArray.ValidDataLength == 0)
            {
                lock (sendQueue)
                {
                    sendQueue.Dequeue();
                    sendQueue.TryPeek(out sendArray);
                }
            }

            // 如果 上一条数据发送不完整, 或者上一条数据发送完整, 队列中有残留的待发送数据
            if (sendArray != null)
            {
                clientState.Socket.BeginSend(sendArray.bytes, sendArray.readIdx, sendArray.ValidDataLength, 0, SendCallback, clientState);
            }

        }
    }    
}