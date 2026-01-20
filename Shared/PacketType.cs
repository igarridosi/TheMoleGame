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
        KickUser,     // Admin -> Server (Bota hau)
        YouAreKicked, // Server -> Client (Bota zaituzte)

        GetStatsRequest, 
        GetStatsResponse,

        GetRankingRequest,
        GetRankingResponse,

        CreateUserRequest, // Moderatzailea -> Server
        CreateUserResponse, // Server -> Moderatzailea
        UpdateUserRoleRequest,
    }
}
