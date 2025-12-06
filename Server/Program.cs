using Server.Data;
using Shared;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

class Program
{
    private static TcpListener _listener;
    private static bool _isRunning = false;
    private static DatabaseManager _dbManager;
    private static ConcurrentDictionary<int, StreamWriter> _clients = new ConcurrentDictionary<int, StreamWriter>();

    static void Main(string[] args)
    {
        SQLitePCL.Batteries.Init();

        Console.Title = "The Mole Game - Server";

        // 1. Datu-basea hasieratu
        _dbManager = new DatabaseManager();
        Console.WriteLine("[DB] Datu-basea prest.");

        // 2. Zerbitzaria martxan jarri
        StartServer(8080); // 8080 portuan entzungo du
    }

    private static void StartServer(int port)
    {
        try
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _isRunning = true;

            Console.WriteLine($"[SERVER] Zerbitzaria entzuten portuan: {port}");
            Console.WriteLine("[SERVER] Bezeroen zain...");

            // Begizta infinitua konexioak onartzeko
            while (_isRunning)
            {
                // Honek programa gelditzen du bezero bat konektatu arte (Blocking)
                TcpClient client = _listener.AcceptTcpClient();

                Console.WriteLine("[SERVER] Bezero berri bat konektatu da!");

                // Hari (Thread) berri bat sortu bezero hau kudeatzeko
                // Horrela zerbitzariak beste bezero batzuk onartzen jarraitu dezake
                Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClient));
                clientThread.Start(client);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Zerbitzariaren errorea: {ex.Message}");
        }
    }

    // Metodo hau hari bereizi batean exekutatuko da bezero bakoitzarentzat
    private static void HandleClient(object obj)
    {
        TcpClient client = (TcpClient)obj;
        NetworkStream stream = client.GetStream();
        StreamReader reader = new StreamReader(stream);
        StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };

        // Bezeroari ID bat esleitu (Hari ID-a erabiliko dugu sinpletasunagatik)
        int clientId = Thread.CurrentThread.ManagedThreadId;

        // ZERRENDARA GEHITU
        _clients.TryAdd(clientId, writer);
        Console.WriteLine($"[SERVER] Bezeroa gehituta zerrendara. Totala: {_clients.Count}");

        try
        {
            string line;
            // Loop honek irakurtzen jarraituko du bezeroa deskonektatu arte
            while ((line = reader.ReadLine()) != null)
            {
                // 1. JSONa Packet bihurtu
                Packet packet = PacketSerializer.Deserialize(line);

                // 2. Zer mezu mota da?
                switch (packet.Type)
                {
                    case PacketType.LoginRequest:
                        HandleLogin(packet.Message, writer);
                        break;
                    case PacketType.ChatMessage:
                        // Norbaitek hitz egiten duenean, DENEI bidali
                        Console.WriteLine($"[CHAT] Mezu berria zabaltzen...");
                        BroadcastPacket(packet);
                        break;

                    case PacketType.GameStart:
                        // Adminak partida hasi du -> DENEI abisatu
                        Console.WriteLine($"[GAME] Partida hasi da!");
                        BroadcastPacket(packet);
                        break;
                    case PacketType.RegisterRequest:
                        HandleRegister(packet.Message, writer);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Konexioa galdu da: {ex.Message}");
        }
        finally
        {
            // Deskonektatzean, zerrendatik kendu
            _clients.TryRemove(clientId, out _);
            client.Close();
            Console.WriteLine("[THREAD] Bezeroa deskonektatu da.");
        }
    }

    // Mezu bat denei bidaltzeko metodoa
    public static void BroadcastPacket(Packet packet)
    {
        string json = PacketSerializer.Serialize(packet);

        foreach (var clientWriter in _clients.Values)
        {
            try
            {
                clientWriter.WriteLine(json);
            }
            catch
            {
                // Bezero hau agian deskonektatu da bidaltzen ari ginen bitartean
            }
        }
    }

    private static void HandleLogin(string jsonMessage, StreamWriter writer)
    {
        // Login datuak atera
        var loginReq = PacketSerializer.DeserializeData<LoginRequest>(jsonMessage);
        Console.WriteLine($"[LOGIN] Saiakera: {loginReq.Username}");

        // DBn begiratu
        User user = _dbManager.ValidateUser(loginReq.Username, loginReq.Password);

        Packet responsePacket = new Packet();

        if (user != null)
        {
            // ONGI: Login zuzena
            responsePacket.Type = PacketType.LoginResponse;
            responsePacket.Message = PacketSerializer.SerializeData(user); // Erabiltzailearen datuak itzuli
            Console.WriteLine($"[LOGIN] ONARTUA: {user.Username}");
        }
        else
        {
            // GAIZKI: Login okerra
            responsePacket.Type = PacketType.LoginResponse;
            responsePacket.Message = null; // Edo errore mezu bat
            Console.WriteLine($"[LOGIN] UKATUA: {loginReq.Username}");
        }

        // Erantzuna bidali bezeroari
        writer.WriteLine(PacketSerializer.Serialize(responsePacket));
    }

    private static void HandleRegister(string jsonMessage, StreamWriter writer)
    {
        var regReq = PacketSerializer.DeserializeData<RegisterRequest>(jsonMessage);
        Console.WriteLine($"[REGISTER] Saiakera: {regReq.Username}");

        bool success = _dbManager.RegisterUser(regReq.Username, regReq.Password);

        Packet response = new Packet
        {
            Type = PacketType.RegisterResponse,
            Message = success ? "OK" : "FAIL" // Sinplea: OK edo FAIL testua
        };

        writer.WriteLine(PacketSerializer.Serialize(response));
        Console.WriteLine($"[REGISTER] Emaitza: {(success ? "Sortua" : "Errorea/Existitzen da")}");
    }
}