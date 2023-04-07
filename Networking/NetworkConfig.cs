namespace localChess.Networking
{
    internal class NetworkConfig
    {
        public string Name { get; set; } = "A localChess user.";
        public bool PrefersBlack { get; set; } = false;
        public TcpCommunication Communication = new();
        public string? PlayingAgainst { get; set; } = null;
    }
}
