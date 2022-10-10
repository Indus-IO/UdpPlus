using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace IndusLib
{
    public delegate void PacketHandler(Packet packet, Node senderNode);
    public delegate void NodeHandler(Node node);
    public delegate void TransactionHandler(Transaction transaction);
    public delegate void BlockHandler(Block block);

    public class UdpPlus
    {
        readonly UdpClient client;
        
        List<Node> allNodes = new List<Node>();
        public Node self = new Node("0", 0);

        public List<Node> GetAllNodes => allNodes;
       
        static readonly byte[] empty = new byte[0];

        #region Event
        #region PacketReceived
        /// <summary>
        /// 패킷 수신시 호출됩니다.
        /// </summary>
        event PacketHandler _PacketReceived;
        public event PacketHandler PacketReceived;

        void OnPacketReceived(Packet packet, Node senderNode)
        {
            if (packet.type == Packet.Type.GETNODES)
            {
                List<Node> existingNodes = packet.data.FromJson<List<Node>>();

                foreach (Node existingNode in existingNodes)
                    client.Send(empty, empty.Length, existingNode.GetIPEndPoint());
            }

            if(packet.type == Packet.Type.IP)
            {
                if(self == null)
                    self = packet.data.FromJson<Node>();
            }

            if (packet.type == Packet.Type.WALLETADDRESS)
            {
                allNodes[allNodes.IndexOf(senderNode)].walletAddress = packet.data;
            }
        }
        #endregion

        #region ServerConnected
        /// <summary>
        /// 서버 연결시 호출됩니다.
        /// </summary>
        event NodeHandler _ServerConnected;
        public event NodeHandler ServerConnected;

        public bool serverConnected;
        #endregion

        #region ServerDisconnected
        /// <summary>
        /// 서버 연결 종료시 호출됩니다.
        /// </summary>
        event NodeHandler _ServerDisconnected;
        public event NodeHandler ServerDisconnected;
        #endregion

        #region NodeConnected
        /// <summary>
        /// 노드 연결시 호출됩니다.
        /// </summary>
        event NodeHandler _NodeConnected;
        public event NodeHandler NodeConnected;

        void OnNodeConnected(Node node)
        {
            //node에게 모든 노드의 정보를 보냅니다.
            SendPacket(new Packet(Packet.Type.GETNODES, allNodes.ToJson()), node.GetIPEndPoint());

            allNodes.Add(node);

            if (node.GetIPEndPoint().Equals(Constants.GetHolePunchingServer()))
            {
                serverConnected = true;
                _ServerConnected?.Invoke(node);
                ServerConnected?.Invoke(node);
            }
        }
        #endregion

        #region NodeDisconnected
        /// <summary>
        /// 노드 연결 종료시 호출됩니다.
        /// </summary>
        event NodeHandler _NodeDisconnected;
        public event NodeHandler NodeDisconnected;

        void OnNodeDisconnected(Node node)
        {
            allNodes.Remove(node);

            if (node.GetIPEndPoint().Equals(Constants.GetHolePunchingServer()))
            {
                serverConnected = false;
                _ServerDisconnected?.Invoke(node);
                ServerDisconnected?.Invoke(node);
            }
        }
        #endregion
        #endregion

        #region Constructor
        public UdpPlus(int port)
        {
            client = new UdpClient(port);
            self = new Node(Constants.GetHolePunchingServer());
            new Thread(Run).Start();
        }

        public UdpPlus()
        {
            client = new UdpClient();
            new Thread(Run).Start();
        }
        #endregion

        void Run()
        {
            _NodeConnected += OnNodeConnected;
            _PacketReceived += OnPacketReceived;
            _NodeDisconnected += OnNodeDisconnected;

            new Thread(HeartBeat).Start();

            while (true)
            {
                Receive();
                Thread.Sleep(10);
            }
        }

        #region 2
        List<Node> aliveCheck = new List<Node>();

        void HeartBeat()
        {
            while (true)
            {
                foreach (Node node in aliveCheck)
                {
                    _NodeDisconnected?.Invoke(node);
                    NodeDisconnected?.Invoke(node);
                }

                Thread.Sleep(2000);
                aliveCheck = new List<Node>();

                foreach (Node node in allNodes)
                {
                    SendEmpty(node.GetIPEndPoint());
                    aliveCheck.Add(node);
                }

                //1초간 클라이언트의 응답 대기
                Thread.Sleep(1 * 1000);
            }
        }
        #endregion

        void Receive()
        {
            IPEndPoint remoteEndPoint = null;
            byte[] receiveBytes = null;
            try
            {
                receiveBytes = client.Receive(ref remoteEndPoint);
            }
            catch (Exception)
            {

            }

            if (receiveBytes == null)
                return;

            Node node = new Node(remoteEndPoint);

            if (aliveCheck.Contains(node))
                aliveCheck.Remove(node);

            if (!allNodes.Contains(node))
            {
                _NodeConnected?.Invoke(node);
                NodeConnected?.Invoke(node);

                SendPacket(new Packet(Packet.Type.IP, node.ToJson()), remoteEndPoint);
            }

            client.Send(empty, empty.Length, node.GetIPEndPoint());
            
            Packet packet = Encoding.UTF8.GetString(receiveBytes).FromJson<Packet>();

            if (packet != null)
            {
                _PacketReceived?.Invoke(packet, node);
                PacketReceived?.Invoke(packet, node);
            }
        }

        #region Send
        public int Send(byte[] dgram, int bytes, IPEndPoint endPoint)
        {
            return client.Send(dgram, bytes, endPoint);
        }

        public void SendEmpty(IPEndPoint endPoint)
        {
            Send(empty, empty.Length, endPoint);
        }

        public void SendPacket(Packet packet)
        {
            byte[] data = Encoding.UTF8.GetBytes(packet.ToJson());
            foreach (Node node in allNodes)
                Send(data, data.Length, node.GetIPEndPoint());
        }

        public void SendPacket(Packet packet, IPEndPoint endPoint)
        {
            byte[] data = Encoding.UTF8.GetBytes(packet.ToJson());
            Send(data, data.Length, endPoint);
        }
        #endregion
    }
}
