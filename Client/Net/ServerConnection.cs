using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Client.Net
{
    public class ServerConnection
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private StreamReader _reader;
        private StreamWriter _writer;

        public bool IsConnected => _client != null && _client.Connected;

        // Zerbitzariarekin konektatu
        public async Task<bool> ConnectAsync(string ip, int port)
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(ip, port); // Asinkronoa interfazea ez blokeatzeko
                _stream = _client.GetStream();
                _reader = new StreamReader(_stream, Encoding.UTF8);
                _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Mezu bat bidali zerbitzariari
        public async Task SendPacketAsync(Packet packet)
        {
            if (!IsConnected) return;

            string json = PacketSerializer.Serialize(packet);
            await _writer.WriteLineAsync(json);
        }

        // Mezu bat irakurri (Login erantzuna jasotzeko adibidez)
        public async Task<Packet> ReadPacketAsync()
        {
            if (!IsConnected) return null;

            string line = await _reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line)) return null;

            return PacketSerializer.Deserialize(line);
        }
    }
}
