using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetProjects.DhcpServer
{
    /// <summary>
    /// DHCP Server
    /// </summary>
    public class DHCPServer : IDisposable
    {
        /// <summary>Delegate for DHCP message</summary>
        public delegate void DHCPDataReceivedEventHandler(DHCPRequest dhcpRequest);

        /// <summary>Will be called on any DHCP message</summary>
        public event DHCPDataReceivedEventHandler OnDataReceived = delegate { };
        /// <summary>Will be called on any DISCOVER message</summary>
        public event DHCPDataReceivedEventHandler OnDiscover = delegate { };
        /// <summary>Will be called on any REQUEST message</summary>
        public event DHCPDataReceivedEventHandler OnRequest = delegate { };
        /// <summary>Will be called on any DECLINE message</summary>
        public event DHCPDataReceivedEventHandler OnDecline = delegate { };
        /// <summary>Will be called on any DECLINE released</summary>
        public event DHCPDataReceivedEventHandler OnReleased = delegate { };
        /// <summary>Will be called on any DECLINE inform</summary>
        public event DHCPDataReceivedEventHandler OnInform = delegate { };

        /// <summary>Server name (optional)</summary>
        public string ServerName { get; set; }

        private Socket socket = null;
        private Task receiveDataTask = null;
        private const int PORT_TO_LISTEN_TO = 67;
        private IPAddress _bindIp;
        private CancellationTokenSource _cancellationTokenSource;

        public event Action<Exception> UnhandledException;

        public IPAddress BroadcastAddress { get; set; }

		public NetworkInterface SendDhcpAnswerNetworkInterface { get; set; }

		/// <summary>
		/// Creates DHCP server, it will be started instantly
		/// </summary>
		/// <param name="bindIp">IP address to bind</param>
		public DHCPServer(IPAddress bindIp)
        {
            _bindIp = bindIp;
        }

        /// <summary>Creates DHCP server, it will be started instantly</summary>
        public DHCPServer() : this(IPAddress.Any)
        {
            BroadcastAddress = IPAddress.Broadcast;
        }

        /// <summary>Starts the DHCP server.</summary>
        /// <exception cref="SocketException">Invalid Socket or error while accessing the socket.</exception>
        /// <exception cref="System.Security.SecurityException">No permission for requested operation.</exception>
        public void Start()
        {
            var ipLocalEndPoint = new IPEndPoint(_bindIp, PORT_TO_LISTEN_TO);
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(ipLocalEndPoint);
            _cancellationTokenSource = new CancellationTokenSource();
            receiveDataTask = new Task(ReceiveDataThread, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning);
            receiveDataTask.Start();
        }

        /// <summary>Disposes DHCP server</summary>
        public void Dispose()
        {
            if (socket != null)
            {
                socket.Close();
                socket = null;
            }
            if (receiveDataTask != null)
            {
                _cancellationTokenSource.Cancel();
                receiveDataTask.Wait();
                receiveDataTask = null;
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void ReceiveDataThread()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    EndPoint remote = (EndPoint)(sender); var buffer = new byte[1024];
                    int len = socket.ReceiveFrom(buffer, ref remote);
                    if (len > 0)
                    {
                        Array.Resize(ref buffer, len);
                        Task.Factory.StartNew(() => DataReceived(buffer));
                    }
                }
                catch (Exception ex)
                {
                    if (UnhandledException != null)
                        UnhandledException(ex);
                }
            }
        }

        private void DataReceived(object o)
        {
            var data = (byte[])o;
            try
            {
                var dhcpRequest = new DHCPRequest(data, socket, this);
                //ccDHCP = new clsDHCP();


                //data is now in the structure
                //get the msg type
                OnDataReceived(dhcpRequest);
                var msgType = dhcpRequest.GetMsgType();
                switch (msgType)
                {
                    case DHCPMsgType.DHCPDISCOVER:
                        OnDiscover(dhcpRequest);
                        break;
                    case DHCPMsgType.DHCPREQUEST:
                        OnRequest(dhcpRequest);
                        break;
                    case DHCPMsgType.DHCPDECLINE:
                        OnDecline(dhcpRequest);
                        break;
                    case DHCPMsgType.DHCPRELEASE:
                        OnReleased(dhcpRequest);
                        break;
                    case DHCPMsgType.DHCPINFORM:
                        OnInform(dhcpRequest);
                        break;
                    //default:
                    //    Console.WriteLine("Unknown DHCP message: " + (int)MsgTyp + " (" + MsgTyp.ToString() + ")");
                    //    break;
                }
            }
            catch (Exception ex)
            {
                if (UnhandledException != null)
                    UnhandledException(ex);
            }
        }
    }
}