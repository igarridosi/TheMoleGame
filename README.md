# 🕵️‍♂️ The Mole Game

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT) [![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/) [![Status](https://img.shields.io/badge/Status-Completed-success)]()

> **Hizkuntza aukeratu / Choose language:**
> 
> [🇪🇺 EUSKARA](#-euskara) | [🇬🇧 ENGLISH](#-english)

---

# <a name="euskara"></a>🇪🇺 EUSKARA

**The Mole Game** denbora errealeko joko anitzeko (multiplayer) mahaigaineko aplikazioa da, "Social Deduction" (dedukzio soziala) generoan oinarrituta.

Jokalari talde batek hitz sekretu bat partekatzen du, baina horietako bat **"Inpostorea"** da eta ez daki hitza zein den. Txandaka pistak eman ondoren, herritarrek Inpostorea nor den asmatu eta kanporatu behar dute bozketa bidez.

### 🚀 Ezaugarri Nagusiak

*   **Bezero-Zerbitzari Arkitektura:** TCP Socket-ak erabiliz komunikazioa.
*   **Denbora Erreala:** Chat-a, jokoaren egoera eta bozketak berehala sinkronizatzen dira.
*   **Rolak:** Herritarra, Inpostorea (kategoriarekin) eta **Moderatzailea** (God Mode panelarekin).
*   **Dinamismoa:** Erronda kopurua jokalari kopuruaren arabera egokitzen da automatikoki.
*   **Segurtasuna:** BAN sistema, Kick, eta pasahitz zifratuak (SHA256).
*   **Estatistikak:** Jokalari bakoitzak bere profil pertsonalizatua du (Dashboard).
*   **Interfaze Modernoa:** WPF Dark Mode diseinua, Pop-up leiho ez-blokeatzaileekin.

### 🛠️ Teknologia

*   **Lengoaia:** C# (.NET 8.0)
*   **UI:** WPF (Windows Presentation Foundation)
*   **Datu-basea:** SQLite
*   **Sarea:** `System.Net.Sockets` (TCP)

### 📥 Instalazioa eta Erabilera

Ez da beharrezkoa kodea konpilatzea jolasteko.

1.  Joan **Releases** atalera eta deskargatu azken bertsioa.
2.  Deskonprimitu karpeta.
3.  **Zerbitzaria:** Exekutatu `Server/TheMoleGame.Server.exe`. (Sareko IPa erakutsiko du).
4.  **Bezeroa:** Exekutatu `Client/TheMoleGame.Client.exe` eta sartu Zerbitzariaren IPa.
5.  Erregistratu erabiltzaile berri bat eta jolastu!

> **Admin kontua:** `admin` / `admin123`
> **Moderatzaile kontua:** `moderator` / `masterkey`

---

### 📸 Pantaila-argazkiak

| Login Leihoa | Lobby & Chat | Bozketa Fasea |
| :---: | :---: | :---: |
| ![Login Screen](https://via.placeholder.com/250x150?text=Login) | ![Lobby](https://via.placeholder.com/250x150?text=Lobby) | ![Voting](https://via.placeholder.com/250x150?text=Voting) |

---

[🔼 Igo gora / Back to top](#the-mole-game)

---

# <a name="english"></a>🇬🇧 ENGLISH

**The Mole Game** is a real-time multiplayer desktop application based on the "Social Deduction" genre.

A group of players shares a secret word, except for one person: the **"Impostor"**. After giving clues turn by turn, civilians must deduce who the Impostor is and eliminate them through a voting system.

### 🚀 Key Features

*   **Client-Server Architecture:** Communication via TCP Sockets.
*   **Real-Time:** Chat, game state, and voting are instantly synchronized.
*   **Roles:** Civilian, Impostor (with category hint), and **Moderator** (God Mode panel).
*   **Dynamic Gameplay:** Round count automatically adjusts based on player count.
*   **Security:** BAN system, Kick feature, and encrypted passwords (SHA256).
*   **Statistics:** Personalized Dashboard for each player.
*   **Modern Interface:** WPF Dark Mode design with non-blocking pop-ups.

### 🛠️ Technology Stack

*   **Language:** C# (.NET 8.0)
*   **UI:** WPF (Windows Presentation Foundation)
*   **Database:** SQLite
*   **Networking:** `System.Net.Sockets` (TCP)

### 📥 Installation & Usage

No compilation required to play.

1.  Go to the **Releases** section and download the latest version.
2.  Unzip the folder.
3.  **Server:** Run `Server/TheMoleGame.Server.exe`.
4.  **Client:** Run `Client/TheMoleGame.Client.exe` and enter the Server IP.
5.  Register a new user and start playing!

> **Default Admin:** `admin` / `admin123`
> **Default Moderator:** `moderator` / `masterkey`

---

### 📸 Screenshots

*(Screenshots will be added here)*

---

[🔼 Igo gora / Back to top](#the-mole-game)