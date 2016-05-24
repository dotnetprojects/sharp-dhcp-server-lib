namespace DotNetProjects.DhcpServer
{
    /// <summary>DHCP message type</summary>
    public enum DHCPMsgType
    {
        /// <summary>DHCP DISCOVER message</summary>
        DHCPDISCOVER = 1,
        /// <summary>DHCP OFFER message</summary>
        DHCPOFFER = 2,
        /// <summary>DHCP REQUEST message</summary>
        DHCPREQUEST = 3,
        /// <summary>DHCP DECLINE message</summary>
        DHCPDECLINE = 4,
        /// <summary>DHCP ACK message</summary>
        DHCPACK = 5,
        /// <summary>DHCP NAK message</summary>
        DHCPNAK = 6,
        /// <summary>DHCP RELEASE message</summary>
        DHCPRELEASE = 7,
        /// <summary>DHCP INFORM message</summary>
        DHCPINFORM = 8
    }
}