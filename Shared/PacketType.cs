using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    // Komunikazioan egon daitezkeen mezu mota guztiak
    public enum PacketType
    {
        // Konexioa eta Login
        LoginRequest,
        LoginResponse,
        RegisterRequest,
        RegisterResponse,

        // Gela (Lobby) kudeaketa
        JoinGame,     // Jolastera sartu
        GameStart,    // Partida hasi da (Adminak emanda)
        PlayerList,   // Nor dagoen konektatuta
        SubmitGameWord,   // Jokalariak bere hitza bidaltzen duenean

        // Jokoaren logika
        GameInfo,     // Zure rola, hitza, etab.
        WordSuggestion, // Jokalariak idatzitako hitza
        Vote,         // Bozketa bat
        RoundEnd,     // Ronda amaitu da
        GameEnd,      // Partida amaitu da
        YourTurn,
        RoundUpdate,
        RestartGameRequest, // Admin -> Server: "Hasi berriro"
        RestartGameInvite,   // Server -> Client: "Nahi duzu jolastu?"
        TimeUpdate,

        AddWordRequest, // Admin -> Server
        AddWordResponse, // Server -> Client (Ondo joan den esateko)
        GetCategoriesRequest,  // Admin -> Server: "Ze kategoria daude?"
        GetCategoriesResponse,  // Server -> Client: ["Animaliak", "Lekuak", ...]

        // Txata
        ChatMessage,

        // Super Admin tools
        AdminPause,
        AdminAnnounce, // Pantaila erdian mezu handia
        AdminSkip,

        GetUserListRequest, // Admin -> Server
        GetUserListResponse, // Server -> Admin
        BanUserRequest,      // Admin -> Server (Ban/Unban)
        DeleteUserRequest,   // Admin -> Server (Ezabatu erabiltzailea)
        DeleteUserResponse,  // Server -> Admin (Konfirmazioa)
        KickUser,     // Admin -> Server (Bota hau)
        YouAreKicked, // Server -> Client (Bota zaituzte)

        GetStatsRequest, 
        GetStatsResponse,

        GetRankingRequest,
        GetRankingResponse,

        CreateUserRequest, // Moderatzailea -> Server
        CreateUserResponse, // Server -> Moderatzailea
        UpdateUserRoleRequest,

        // GELA KUDEAKETA
        CreateRoomRequest,  // Client -> Server: "Gela bat nahi dut"
        CreateRoomResponse, // Server -> Client: "Hau da kodea: X5J9P"

        JoinRoomRequest,    // Client -> Server: "X5J9P gelan sartu nahi dut"
        JoinRoomResponse,    // Server -> Client: "OK" edo "Ez da existitzen"

        RequestPlayerList, // Client -> Server: "Bidali zerrenda"

        GetRoomsRequest,  // Mod -> Server
        GetRoomsResponse, // Server -> Mod (List<string> Codes)
        LeaveRoomRequest  // Mod -> Server (Gelatik atera menura itzultzeko)
    }
}
