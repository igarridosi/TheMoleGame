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

        // Txata
        ChatMessage
    }
}
