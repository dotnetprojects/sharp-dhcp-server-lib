/*
 * ShapDHCPServer (C) 2010, Cluster
 * http://clusterrr.com
 * http://code.google.com/p/sharpdhcpserver/
 * 
 *           DO WHAT THE FUCK YOU WANT TO PUBLIC LICENSE
 *                   Version 2, December 2004
 *
 * Copyright (C) 2004 Sam Hocevar
 * 14 rue de Plaisance, 75014 Paris, France
 * Everyone is permitted to copy and distribute verbatim or modified
 * copies of this license document, and changing it is allowed as long
 * as the name is changed.
 *
 *           DO WHAT THE FUCK YOU WANT TO PUBLIC LICENSE
 *  TERMS AND CONDITIONS FOR COPYING, DISTRIBUTION AND MODIFICATION
 *
 * 0. You just DO WHAT THE FUCK YOU WANT TO.
 * 
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using DotNetProjects.DhcpServer;

namespace Cluster.SharpDHCPServer_Sample
{
    class Program
    {
        const string LOCAL_INTERFACE = "0.0.0.0";
        static byte nextIP = 10;
        static Dictionary<string, IPAddress> leases = new Dictionary<string, IPAddress>();
        static void Main(string[] args)
        {
            var server = new DHCPServer();
            server.ServerName = "SharpDHCPServer";
            server.OnDataReceived += Request;
            Console.WriteLine("Running DHCP server. Press enter to stop it.");
            Console.ReadLine();
            server.Dispose();
        }

        static void Request(DHCPRequest dhcpRequest)
        {
            var type = dhcpRequest.GetMsgType();
            var mac = ByteArrayToString(dhcpRequest.GetChaddr());
            // IP for client
            IPAddress ip;
            if (!leases.TryGetValue(mac, out ip))
            {
                ip = new IPAddress(new byte[] { 10, 0, 0, nextIP++ });
                leases[mac] = ip;
            }
            Console.WriteLine(type.ToString() + " request from " + mac + ", it will be " + ip.ToString());

            var options = dhcpRequest.GetAllOptions();
            Console.Write("Options:");
            foreach (DHCPOption option in options.Keys)
            {
                Console.WriteLine(option.ToString() + ": " + ByteArrayToString(options[option]));
            }
            // Lets show some request info
            var requestedOptions = dhcpRequest.GetRequestedOptionsList();
            if (requestedOptions != null)
            {
                Console.Write("Requested options:");
                foreach (DHCPOption option in requestedOptions) Console.Write(" " + option.ToString());
                Console.WriteLine();
            }
            // Option 82 info
            var relayInfoN = dhcpRequest.GetRelayInfo();
            if (relayInfoN != null)
            {
                var relayInfo = (RelayInfo)relayInfoN;
                if (relayInfo.AgentCircuitID != null) Console.WriteLine("Relay agent circuit ID: " + ByteArrayToString(relayInfo.AgentCircuitID));
                if (relayInfo.AgentRemoteID != null) Console.WriteLine("Relay agent remote ID: " + ByteArrayToString(relayInfo.AgentRemoteID));
            }
            Console.WriteLine();

            var replyOptions = new DHCPReplyOptions();
            // Options should be filled with valid data. Only requested options will be sent.
            replyOptions.SubnetMask = IPAddress.Parse("255.255.255.0");
            replyOptions.DomainName = "SharpDHCPServer";
            replyOptions.ServerIdentifier = IPAddress.Parse("10.0.0.1");
            replyOptions.RouterIP = IPAddress.Parse("10.0.0.1");
            replyOptions.DomainNameServers = new IPAddress[] { IPAddress.Parse("192.168.100.2"), IPAddress.Parse("192.168.100.3") };
            // Some static routes
            replyOptions.StaticRoutes = new NetworkRoute[] { 
                new NetworkRoute(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("255.0.0.0"), IPAddress.Parse("10.0.0.1")),
                new NetworkRoute(IPAddress.Parse("192.168.0.0"), IPAddress.Parse("255.255.0.0"), IPAddress.Parse("10.0.0.1")),
                new NetworkRoute(IPAddress.Parse("172.16.0.0"), IPAddress.Parse("255.240.0.0"), IPAddress.Parse("10.0.0.1")),
                new NetworkRoute(IPAddress.Parse("80.252.130.248"), IPAddress.Parse("255.255.255.248"), IPAddress.Parse("10.0.0.1")),
                new NetworkRoute(IPAddress.Parse("80.252.128.88"), IPAddress.Parse("255.255.255.248"), IPAddress.Parse("10.0.0.1")),
            };

            // Lets send reply to client!
            if (type == DHCPMsgType.DHCPDISCOVER)
                dhcpRequest.SendDHCPReply(DHCPMsgType.DHCPOFFER, ip, replyOptions);
            if (type == DHCPMsgType.DHCPREQUEST)
                dhcpRequest.SendDHCPReply(DHCPMsgType.DHCPACK, ip, replyOptions);
        }

        static string ByteArrayToString(byte[] ar)
        {
            var res = new StringBuilder();
            foreach (var b in ar)
            {
                res.Append(b.ToString("X2"));
            }
            res.Append(" (");
            foreach (var b in ar)
            {
                if ((b >= 32) && (b <127))
                    res.Append(Encoding.ASCII.GetString(new byte[] { b }));
                else res.Append(" ");
            }
            res.Append(")");
            return res.ToString();
        }
    }
}
