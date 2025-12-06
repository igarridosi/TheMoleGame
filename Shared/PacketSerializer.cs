using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Shared
{
    public static class PacketSerializer
    {
        // Objektua -> Testua (JSON)
        public static string Serialize(Packet packet)
        {
            return JsonSerializer.Serialize(packet);
        }

        // Testua (JSON) -> Objektua
        public static Packet Deserialize(string json)
        {
            return JsonSerializer.Deserialize<Packet>(json);
        }

        // Generikoa: Edozein datu mota deserializatzeko (adib: User, LoginData...)
        public static T DeserializeData<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json);
        }

        public static string SerializeData<T>(T data)
        {
            return JsonSerializer.Serialize(data);
        }
    }
}
