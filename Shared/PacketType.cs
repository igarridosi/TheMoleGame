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

        // Jokoaren logika
        GameInfo,     // Zure rola, hitza, etab.
        WordSuggestion, // Jokalariak idatzitako hitza
        Vote,         // Bozketa bat
        RoundEnd,     // Ronda amaitu da
        GameEnd,      // Partida amaitu da

        // Txata
        ChatMessage
    }
}
