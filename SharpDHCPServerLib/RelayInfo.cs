namespace DotNetProjects.DhcpServer
{
    /// <summary>DHCP relay information (option 82)</summary>
    public struct RelayInfo
    {
        /// <summary>Agent circuit ID</summary>
        public byte[] AgentCircuitID;
        /// <summary>Agent remote ID</summary>
        public byte[] AgentRemoteID;
    }
}