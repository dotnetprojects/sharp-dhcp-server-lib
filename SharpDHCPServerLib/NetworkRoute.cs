using System;
using System.Net;

namespace DotNetProjects.DhcpServer
{
    /// <summary>Network route</summary>
    public struct NetworkRoute
    {
        /// <summary>IP address of destination network</summary>
        public IPAddress Network;
        /// <summary>Subnet mask length</summary>
        public byte NetMaskLength;
        /// <summary>Gateway</summary>
        public IPAddress Gateway;

        /// <summary>Creates network route</summary>
        /// <param name="network">IP address to bind</param>
        /// <param name="netMaskLength">Subnet mask length</param>
        /// <param name="gateway">Gateway</param>
        public NetworkRoute(IPAddress network, byte netMaskLength, IPAddress gateway)
        {
            Network = network;
            NetMaskLength = netMaskLength;
            Gateway = gateway;
        }

        /// <summary>Creates network route</summary>
        /// <param name="network">IP address to bind</param>
        /// <param name="netMask">Subnet mask</param>
        /// <param name="gateway">Gateway</param>
        public NetworkRoute(IPAddress network, IPAddress netMask, IPAddress gateway)
        {
            byte length = 0;
            var mask = netMask.GetAddressBytes();
            for (byte x = 0; x < 4; x++)
            {
                for (byte b = 0; b < 8; b++)
                    if (((mask[x] >> (7 - b)) & 1) == 1)
                        length++;
                    else break;
            }
            Network = network;
            NetMaskLength = length;
            Gateway = gateway;
        }

        internal byte[] BuildRouteData()
        {
            int ipLength;
            if (NetMaskLength <= 8) ipLength = 1;
            else if (NetMaskLength <= 16) ipLength = 2;
            else if (NetMaskLength <= 24) ipLength = 3;
            else ipLength = 4;
            var res = new byte[1 + ipLength + 4];
            res[0] = NetMaskLength;
            Array.Copy(Network.GetAddressBytes(), 0, res, 1, ipLength);
            Array.Copy(Gateway.GetAddressBytes(), 0, res, 1 + ipLength, 4);
            return res;
        }
    }
}