using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace localChess.Networking.Communications
{
    internal interface ICommunication
    {
        public void StartServer(int port);
        public void ConnectToServer(string ipAddress, int port);
        public void SendData(byte[] dataToSend);
        public bool IsConnected();
        public byte[]? ReceiveData();
        public void Stop();
    }
}
