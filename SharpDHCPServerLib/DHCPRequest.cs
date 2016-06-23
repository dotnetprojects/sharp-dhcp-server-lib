using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DotNetProjects.DhcpServer
{
    /// <summary>
    /// DHCP request
    /// </summary>
    public class DHCPRequest
    {
        private readonly DHCPServer dhcpServer;
        private readonly DHCPPacket requestData;
        private Socket requestSocket;
        private const int OPTION_OFFSET = 240;
        private const int PORT_TO_SEND_TO_CLIENT = 68;
        private const int PORT_TO_SEND_TO_RELAY = 67;

        /// <summary>
        /// Raw DHCP packet
        /// </summary>
        public struct DHCPPacket
        {
            /// <summary>Op code:   1 = boot request, 2 = boot reply</summary>
            public byte op;
            /// <summary>Hardware address type</summary>
            public byte htype;
            /// <summary>Hardware address length: length of MACID</summary>
            public byte hlen;
            /// <summary>Hardware options</summary>
            public byte hops;
            /// <summary>Transaction id</summary>
            public byte[] xid;
            /// <summary>Elapsed time from trying to boot</summary>
            public byte[] secs;
            /// <summary>Flags</summary>
            public byte[] flags;
            /// <summary>Client IP</summary>
            public byte[] ciaddr;
            /// <summary>Your client IP</summary>
            public byte[] yiaddr;
            /// <summary>Server IP</summary>
            public byte[] siaddr;
            /// <summary>Relay agent IP</summary>
            public byte[] giaddr;
            /// <summary>Client HW address</summary>
            public byte[] chaddr;
            /// <summary>Optional server host name</summary>
            public byte[] sname;
            /// <summary>Boot file name</summary>
            public byte[] file;
            /// <summary>Magic cookie</summary>
            public byte[] mcookie;
            /// <summary>Options (rest)</summary>
            public byte[] options;
        }

        internal DHCPRequest(byte[] data, Socket socket, DHCPServer server)
        {
            dhcpServer = server;
            System.IO.BinaryReader rdr;
            System.IO.MemoryStream stm = new System.IO.MemoryStream(data, 0, data.Length);
            rdr = new System.IO.BinaryReader(stm);
            // Reading data
            requestData.op = rdr.ReadByte();
            requestData.htype = rdr.ReadByte();
            requestData.hlen = rdr.ReadByte();
            requestData.hops = rdr.ReadByte();
            requestData.xid = rdr.ReadBytes(4);
            requestData.secs = rdr.ReadBytes(2);
            requestData.flags = rdr.ReadBytes(2);
            requestData.ciaddr = rdr.ReadBytes(4);
            requestData.yiaddr = rdr.ReadBytes(4);
            requestData.siaddr = rdr.ReadBytes(4);
            requestData.giaddr = rdr.ReadBytes(4);
            requestData.chaddr = rdr.ReadBytes(16);
            requestData.sname = rdr.ReadBytes(64);
            requestData.file = rdr.ReadBytes(128);
            requestData.mcookie = rdr.ReadBytes(4);
            requestData.options = rdr.ReadBytes(data.Length - OPTION_OFFSET);
            requestSocket = socket;
        }

        /// <summary>
        /// Returns array of requested by client options
        /// </summary>
        /// <returns>Array of requested by client options</returns>
        public DHCPOption[] GetRequestedOptionsList()
        {
            var reqList = this.GetOptionData(DHCPOption.ParameterRequestList);
            var optList = new List<DHCPOption>();
            if (reqList != null) foreach (var option in reqList) optList.Add((DHCPOption)option); else return null;
            return optList.ToArray();
        }

        private byte[] CreateOptionStruct(DHCPMsgType msgType, DHCPReplyOptions replyOptions, Dictionary<DHCPOption, byte[]> otherForceOptions, IEnumerable<DHCPOption> forceOptions)
        {
            Dictionary<DHCPOption, byte[]> options = new Dictionary<DHCPOption, byte[]>();

            byte[] resultOptions = null;
            // Requested options
            var reqList = GetRequestedOptionsList();
            // Option82?
            var relayInfo = this.GetOptionData(DHCPOption.RelayInfo);
            CreateOptionElement(ref resultOptions, DHCPOption.DHCPMessageTYPE, new byte[] { (byte)msgType });
            // Server identifier - our IP address
            if ((replyOptions != null) && (replyOptions.ServerIdentifier != null))
                options.Add(DHCPOption.ServerIdentifier, replyOptions.ServerIdentifier.GetAddressBytes());

            if (reqList == null && forceOptions != null)
                reqList = new DHCPOption[0];

            // Requested options
            if ((reqList != null) && (replyOptions != null))
                foreach (DHCPOption i in reqList.Union(forceOptions).Distinct().OrderBy(x=>(int)x))
                {
                    byte[] optionData = null;
                    // If it's force option - ignore it. We'll send it later.
                    if ((otherForceOptions != null) && (otherForceOptions.TryGetValue(i, out optionData)))
                        continue;
                    switch (i)
                    {
                        case DHCPOption.SubnetMask:
                            if (replyOptions.SubnetMask != null)
                                optionData = replyOptions.SubnetMask.GetAddressBytes();
                            break;
                        case DHCPOption.Router:
                            if (replyOptions.RouterIP != null)
                                optionData = replyOptions.RouterIP.GetAddressBytes();
                            break;
                        case DHCPOption.DomainNameServers:
                            if (replyOptions.DomainNameServers != null)
                            {
                                optionData = new byte[] { };
                                foreach (var dns in replyOptions.DomainNameServers)
                                {
                                    var dnsserv = dns.GetAddressBytes();
                                    Array.Resize(ref optionData, optionData.Length + 4);
                                    Array.Copy(dnsserv, 0, optionData, optionData.Length - 4, 4);
                                }
                            }
                            break;
                        case DHCPOption.DomainName:
                            if (!string.IsNullOrEmpty(replyOptions.DomainName))
                                optionData = System.Text.Encoding.ASCII.GetBytes(replyOptions.DomainName);
                            break;
                        case DHCPOption.ServerIdentifier:
                            if (replyOptions.ServerIdentifier != null)
                                optionData = replyOptions.ServerIdentifier.GetAddressBytes();
                            break;
                        case DHCPOption.LogServer:
                            if (replyOptions.LogServerIP != null)
                                optionData = replyOptions.LogServerIP.GetAddressBytes();
                            break;
                        case DHCPOption.StaticRoutes:
                        case DHCPOption.StaticRoutesWin:
                            if (replyOptions.StaticRoutes != null)
                            {
                                optionData = new byte[] { };
                                foreach (var route in replyOptions.StaticRoutes)
                                {
                                    var routeData = route.BuildRouteData();
                                    Array.Resize(ref optionData, optionData.Length + routeData.Length);
                                    Array.Copy(routeData, 0, optionData, optionData.Length - routeData.Length, routeData.Length);
                                }
                            }
                            break;
                        default:
                            replyOptions.OtherRequestedOptions.TryGetValue(i, out optionData);
                            break;
                    }
                    if (optionData != null)
                    {
                        options.Add(i, optionData);
                    }
                }

            if (GetMsgType() != DHCPMsgType.DHCPINFORM)
            {
                // Lease time
                if (replyOptions != null)
                {
                    var leaseTime = new byte[4];
                    leaseTime[3] = (byte)(replyOptions.IPAddressLeaseTime);
                    leaseTime[2] = (byte)(replyOptions.IPAddressLeaseTime >> 8);
                    leaseTime[1] = (byte)(replyOptions.IPAddressLeaseTime >> 16);
                    leaseTime[0] = (byte)(replyOptions.IPAddressLeaseTime >> 24);
                    options.Add(DHCPOption.IPAddressLeaseTime, leaseTime);
                    if (replyOptions.RenewalTimeValue_T1.HasValue)
                    {
                        leaseTime[3] = (byte) (replyOptions.RenewalTimeValue_T1);
                        leaseTime[2] = (byte) (replyOptions.RenewalTimeValue_T1 >> 8);
                        leaseTime[1] = (byte) (replyOptions.RenewalTimeValue_T1 >> 16);
                        leaseTime[0] = (byte) (replyOptions.RenewalTimeValue_T1 >> 24);
                        options.Add(DHCPOption.RenewalTimeValue_T1, leaseTime);
                    }
                    if (replyOptions.RebindingTimeValue_T2.HasValue)
                    {
                        leaseTime[3] = (byte) (replyOptions.RebindingTimeValue_T2);
                        leaseTime[2] = (byte) (replyOptions.RebindingTimeValue_T2 >> 8);
                        leaseTime[1] = (byte) (replyOptions.RebindingTimeValue_T2 >> 16);
                        leaseTime[0] = (byte) (replyOptions.RebindingTimeValue_T2 >> 24);
                        options.Add(DHCPOption.RebindingTimeValue_T2, leaseTime);
                    }
                }
            }
            // Other requested options
            if (otherForceOptions != null)
                foreach (var option in otherForceOptions.Keys)
                {
                    options.Add(option, otherForceOptions[option]);
                    if (option == DHCPOption.RelayInfo) relayInfo = null;
                }

            // Option 82? Send it back!
            if (relayInfo != null)
            {
                options.Add(DHCPOption.RelayInfo, relayInfo);
            }

            foreach (var option in options.OrderBy(x=>(int)x.Key))
            {
                CreateOptionElement(ref resultOptions, option.Key, option.Value);
            }

            // Create the end option
            Array.Resize(ref resultOptions, resultOptions.Length + 1);
            Array.Copy(new byte[] { 255 }, 0, resultOptions, resultOptions.Length - 1, 1);
            return resultOptions;
        }

        static private void CreateOptionElement(ref byte[] options, DHCPOption option, byte[] data)
        {
            byte[] optionData;

            optionData = new byte[data.Length + 2];
            optionData[0] = (byte)option;
            optionData[1] = (byte)data.Length;
            Array.Copy(data, 0, optionData, 2, data.Length);
            if (options == null)
                Array.Resize(ref options, (int)optionData.Length);
            else
                Array.Resize(ref options, options.Length + optionData.Length);
            Array.Copy(optionData, 0, options, options.Length - optionData.Length, optionData.Length);
        }

        /// <summary>
        /// Sends DHCP reply
        /// </summary>
        /// <param name="msgType">Type of DHCP message to send</param>
        /// <param name="ip">IP for client</param>
        /// <param name="replyData">Reply options (will be sent if requested)</param>
        public void SendDHCPReply(DHCPMsgType msgType, IPAddress ip, DHCPReplyOptions replyData)
        {
            SendDHCPReply(msgType, ip, replyData, null, null);
        }



        /// <summary>
        /// Sends DHCP reply
        /// </summary>
        /// <param name="msgType">Type of DHCP message to send</param>
        /// <param name="ip">IP for client</param>
        /// <param name="replyData">Reply options (will be sent if requested)</param>
        /// <param name="otherForceOptions">Force reply options (will be sent anyway)</param>
        public void SendDHCPReply(DHCPMsgType msgType, IPAddress ip, DHCPReplyOptions replyData,
            Dictionary<DHCPOption, byte[]> otherForceOptions)
        {
            SendDHCPReply(msgType, ip, replyData, otherForceOptions, null);
        }

        /// <summary>
        /// Sends DHCP reply
        /// </summary>
        /// <param name="msgType">Type of DHCP message to send</param>
        /// <param name="ip">IP for client</param>
        /// <param name="replyData">Reply options (will be sent if requested)</param>
        /// <param name="forceOptions">Force reply options (will be sent anyway)</param>
        public void SendDHCPReply(DHCPMsgType msgType, IPAddress ip, DHCPReplyOptions replyData,
            IEnumerable<DHCPOption> forceOptions)
        {
            SendDHCPReply(msgType, ip, replyData, null, forceOptions);
        }

        /// <summary>
        /// Sends DHCP reply
        /// </summary>
        /// <param name="msgType">Type of DHCP message to send</param>
        /// <param name="ip">IP for client</param>
        /// <param name="replyData">Reply options (will be sent if requested)</param>
        /// <param name="otherForceOptions">Force reply options (will be sent anyway)</param>
        private void SendDHCPReply(DHCPMsgType msgType, IPAddress ip, DHCPReplyOptions replyData, Dictionary<DHCPOption, byte[]> otherForceOptions, IEnumerable<DHCPOption> forceOptions)
        {
            var replyBuffer = requestData;
            replyBuffer.op = 2; // Reply
            replyBuffer.yiaddr = ip.GetAddressBytes(); // Client's IP
            if (replyData.ServerIpAddress != null)
            {
                replyBuffer.siaddr = replyData.ServerIpAddress.GetAddressBytes();
            }
            replyBuffer.options = CreateOptionStruct(msgType, replyData, otherForceOptions, forceOptions); // Options
            if (!string.IsNullOrEmpty(dhcpServer.ServerName))
            {
                var serverNameBytes = Encoding.ASCII.GetBytes(dhcpServer.ServerName);
                int len = (serverNameBytes.Length > 63) ? 63 : serverNameBytes.Length;
                Array.Copy(serverNameBytes, replyBuffer.sname, len);
                replyBuffer.sname[len] = 0;
            }
            lock (requestSocket)
            {
                var DataToSend = BuildDataStructure(replyBuffer);
                if (DataToSend.Length < 300)
                {
                    var sendArray = new byte[300];
                    Array.Copy(DataToSend, 0, sendArray, 0, DataToSend.Length);
                    DataToSend = sendArray;
                }

                IPEndPoint endPoint;
                if ((replyBuffer.giaddr[0] == 0) && (replyBuffer.giaddr[1] == 0) &&
                    (replyBuffer.giaddr[2] == 0) && (replyBuffer.giaddr[3] == 0))
                {
                    //requestSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                    //endPoint = new IPEndPoint(dhcpServer.BroadcastAddress, PORT_TO_SEND_TO_CLIENT);

                    var udp = new UdpClient();
                    udp.EnableBroadcast = true;
                    udp.Send(DataToSend, DataToSend.Length, new IPEndPoint(dhcpServer.BroadcastAddress, 68));
                    udp.Close();
                }
                else
                {
                    requestSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, false);
                    endPoint = new IPEndPoint(new IPAddress(replyBuffer.giaddr), PORT_TO_SEND_TO_RELAY);
                    requestSocket.SendTo(DataToSend, endPoint);
                }
            }
        }

        private static byte[] BuildDataStructure(DHCPPacket packet)
        {
            byte[] mArray;

            try
            {
                mArray = new byte[0];
                AddOptionElement(new byte[] { packet.op }, ref mArray);
                AddOptionElement(new byte[] { packet.htype }, ref mArray);
                AddOptionElement(new byte[] { packet.hlen }, ref mArray);
                AddOptionElement(new byte[] { packet.hops }, ref mArray);
                AddOptionElement(packet.xid, ref mArray);
                AddOptionElement(packet.secs, ref mArray);
                AddOptionElement(packet.flags, ref mArray);
                AddOptionElement(packet.ciaddr, ref mArray);
                AddOptionElement(packet.yiaddr, ref mArray);
                AddOptionElement(packet.siaddr, ref mArray);
                AddOptionElement(packet.giaddr, ref mArray);
                AddOptionElement(packet.chaddr, ref mArray);
                AddOptionElement(packet.sname, ref mArray);
                AddOptionElement(packet.file, ref mArray);

                AddOptionElement(packet.mcookie, ref mArray);
                AddOptionElement(packet.options, ref mArray);
                return mArray;
            }
            finally
            {
                mArray = null;
            }
        }

        private static void AddOptionElement(byte[] fromValue, ref byte[] targetArray)
        {
            if (targetArray != null)
                Array.Resize(ref targetArray, targetArray.Length + fromValue.Length);
            else
                Array.Resize(ref targetArray, fromValue.Length);
            Array.Copy(fromValue, 0, targetArray, targetArray.Length - fromValue.Length, fromValue.Length);
        }

        /// <summary>
        /// Returns option content
        /// </summary>
        /// <param name="option">Option to retrieve</param>
        /// <returns>Option content</returns>
        public byte[] GetOptionData(DHCPOption option)
        {
            int DHCPId = 0;
            byte DDataID, DataLength = 0;
            byte[] dumpData;

            DHCPId = (int)option;
            for (int i = 0; i < requestData.options.Length; i++)
            {
                DDataID = requestData.options[i];
                if (DDataID == (byte)DHCPOption.END_Option) break;
                if (DDataID == DHCPId)
                {
                    DataLength = requestData.options[i + 1];
                    dumpData = new byte[DataLength];
                    Array.Copy(requestData.options, i + 2, dumpData, 0, DataLength);
                    return dumpData;
                }
                else if (DDataID == 0)
                {
                }
                else
                {
                    DataLength = requestData.options[i + 1];
                    i += 1 + DataLength;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns all options
        /// </summary>
        /// <returns>Options dictionary</returns>
        public Dictionary<DHCPOption, byte[]> GetAllOptions()
        {
            var result = new Dictionary<DHCPOption, byte[]>();
            DHCPOption DDataID;
            byte DataLength = 0;

            for (int i = 0; i < requestData.options.Length; i++)
            {
                DDataID = (DHCPOption)requestData.options[i];
                if (DDataID == DHCPOption.END_Option) break;
                DataLength = requestData.options[i + 1];
                byte[] dumpData = new byte[DataLength];
                Array.Copy(requestData.options, i + 2, dumpData, 0, DataLength);
                result[DDataID] = dumpData;

                DataLength = requestData.options[i + 1];
                i += 1 + DataLength;
            }

            return result;
        }

        /// <summary>
        /// Returns ciaddr (client IP address)
        /// </summary>
        /// <returns>ciaddr</returns>
        public IPAddress GetCiaddr()
        {
            if ((requestData.ciaddr[0] == 0) &&
                (requestData.ciaddr[1] == 0) &&
                (requestData.ciaddr[2] == 0) &&
                (requestData.ciaddr[3] == 0)
                ) return null;
            return new IPAddress(requestData.ciaddr);
        }
        /// <summary>
        /// Returns giaddr (gateway IP address switched by relay)
        /// </summary>
        /// <returns>giaddr</returns>
        public IPAddress GetGiaddr()
        {
            if ((requestData.giaddr[0] == 0) &&
                (requestData.giaddr[1] == 0) &&
                (requestData.giaddr[2] == 0) &&
                (requestData.giaddr[3] == 0)
                ) return null;
            return new IPAddress(requestData.giaddr);
        }
        /// <summary>
        /// Returns chaddr (client hardware address)
        /// </summary>
        /// <returns>chaddr</returns>
        public byte[] GetChaddr()
        {
            var res = new byte[requestData.hlen];
            Array.Copy(requestData.chaddr, res, requestData.hlen);
            return res;
        }
        /// <summary>
        /// Returns requested IP (option 50)
        /// </summary>
        /// <returns>Requested IP</returns>
        public IPAddress GetRequestedIP()
        {
            var ipBytes = GetOptionData(DHCPOption.RequestedIPAddress);
            if (ipBytes == null) return null;
            return new IPAddress(ipBytes);
        }
        /// <summary>
        /// Returns type of DHCP request
        /// </summary>
        /// <returns>DHCP message type</returns>
        public DHCPMsgType GetMsgType()
        {
            byte[] DData;
            DData = GetOptionData(DHCPOption.DHCPMessageTYPE);
            if (DData != null)
                return (DHCPMsgType)DData[0];
            return 0;
        }
        /// <summary>
        /// Returns entire content of DHCP packet
        /// </summary>
        /// <returns>DHCP packet</returns>
        public DHCPPacket GetRawPacket()
        {
            return requestData;
        }
        /// <summary>
        /// Returns relay info (option 82)
        /// </summary>
        /// <returns>Relay info</returns>
        public RelayInfo? GetRelayInfo()
        {
            var result = new RelayInfo();
            var relayInfo = GetOptionData(DHCPOption.RelayInfo);
            if (relayInfo != null)
            {
                int i = 0;
                while (i < relayInfo.Length)
                {
                    var subOptID = relayInfo[i];
                    if (subOptID == 1)
                    {
                        result.AgentCircuitID = new byte[relayInfo[i + 1]];
                        Array.Copy(relayInfo, i + 2, result.AgentCircuitID, 0, relayInfo[i + 1]);
                    }
                    else if (subOptID == 2)
                    {
                        result.AgentRemoteID = new byte[relayInfo[i + 1]];
                        Array.Copy(relayInfo, i + 2, result.AgentRemoteID, 0, relayInfo[i + 1]);
                    }
                    i += 2 + relayInfo[i + 1];
                }
                return result;
            }
            return null;            
        }
    }
}