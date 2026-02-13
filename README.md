# 🕵️‍♂️ The Mole Game

[![.NET Core Desktop](https://github.com/igarridosi/TheMoleGame/actions/workflows/dotnet.yml/badge.svg)](https://github.com/igarridosi/TheMoleGame/actions/workflows/dotnet.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

> **Hizkuntza aukeratu / Choose language:**
> 
> [🇪🇺 EUSKARA](#-euskara) | [🇬🇧 ENGLISH](#-english)

---

# <a name="euskara"></a>🇪🇺 EUSKARA

**The Mole Game** denbora errealeko joko anitzeko (multiplayer) mahaigaineko aplikazioa da, "Social Deduction" (dedukzio soziala) generoan oinarrituta.

Jokalari talde batek hitz sekretu bat partekatzen du, baina horietako bat **"Inpostorea"** da eta ez daki hitza zein den. Txandaka pistak eman ondoren, herritarrek Inpostorea nor den asmatu eta kanporatu behar dute bozketa bidez.

### 🚀 Ezaugarri Nagusiak

#### 🎮 Jokoaren Mekanika
*   **Multi-Room Sistema:** Hainbat partida paraleloan jokatu daitezke zerbitzari berean. Jokalariek gelak sor ditzakete edo kode bidez sartu.
*   **Rolak:** Herritarra, Inpostorea (kategoriarekin) eta **Moderatzailea** (God Mode).
*   **Tenporizadoreak:** 20 segunduko muga txandetan hitza idazteko eta 60 segundu bozketarako. Denbora agortzean sistema automatikoak jokatzen du.
*   **Dinamismoa:** Erronda kopurua jokalari kopuruaren arabera egokitzen da automatikoki.

#### ⚙️ Alderdi Teknikoak
*   **Bezero-Zerbitzari Arkitektura:** TCP Socket-ak erabiliz komunikazio asinkronoa.
*   **Konkurrentzia:** Hari anitzeko (Multi-threading) kudeaketa eta *Thread-Safety* (ConcurrentDictionary, Locks, Semaphores).
*   **Datuen Iraunkortasuna:** SQLite datu-basea erabiltzaileak, hitzak eta estatistikak gordetzeko.
*   **Reporting:** Estatistika aurreratuak (Win Rate, Detektibe Sen, Martiria...) eta **PDF Esportazioa** (QuestPDF).
*   **CI/CD:** GitHub Actions bidezko test automatizatuak (Unitarioak eta Integraziozkoak).

### 🛠️ Moderatzaile Modua (God Mode)
Moderatzaileak panel berezi bat dauka jokoa kudeatzeko jokatzen ez duen bitartean:
*   **Erabiltzaile Kudeaketa:** Erabiltzaileak sortu, rolak aldatu eta **BAN/UNBAN** sistema.
*   **Edukien Kudeaketa:** Hitz eta Kategoria berriak gehitu jokotik irten gabe.
*   **Partiden Kontrola:** Gela aktiboak ikusi, edozein partidatan sartu, `SKIP ROUND`, `ANNOUNCE` mezuak bidali.

### 📥 Instalazioa eta Erabilera

Ez da beharrezkoa kodea konpilatzea jolasteko ("Self-Contained" moduan dago).

1.  Joan **Releases** atalera eta deskargatu azken bertsioa.
2.  Deskonprimitu karpeta.
3.  **Zerbitzaria:** Exekutatu `Server/TheMoleGame.Server.exe`.
4.  **Bezeroa:** Exekutatu `Client/TheMoleGame.Client.exe` eta sartu Zerbitzariaren IPa.
5.  Erregistratu erabiltzaile berri bat eta jolastu!

> **Moderatzaile kontu lehenetsia:** `moderator` / `masterkey`

---

[🔼 Igo gora / Back to top](#the-mole-game)

---

# <a name="english"></a>🇬🇧 ENGLISH

**The Mole Game** is a real-time multiplayer desktop application based on the "Social Deduction" genre.

A group of players shares a secret word, except for one person: the **"Impostor"**. After giving clues turn by turn, civilians must deduce who the Impostor is and eliminate them through a voting system.

### 🚀 Key Features

#### 🎮 Gameplay
*   **Multi-Room System:** Support for parallel games on the same server. Players can create rooms or join via code.
*   **Roles:** Civilian, Impostor (with category hint), and **Moderator** (God Mode).
*   **Timers:** 20s turn timer and 60s voting timer. Automatic handling of timeouts/AFK players.
*   **Dynamic Logic:** Round count automatically adjusts based on player count.

#### ⚙️ Technical Highlights
*   **Client-Server Architecture:** Asynchronous communication via TCP Sockets.
*   **Concurrency:** Multi-threading and Thread-Safety (ConcurrentDictionary, Locks, Semaphores).
*   **Persistence:** SQLite database for users, game content, and historical stats.
*   **Reporting:** Advanced Analytics (Win Rate, Detective Sense, Martyrdom...) and **PDF Export** using QuestPDF.
*   **CI/CD:** Automated testing pipeline via GitHub Actions.

### 🛠️ Moderator Mode (God Mode)
Moderators have a dedicated dashboard to manage the game without playing:
*   **User Management:** Create users, change roles, and a complete **BAN/UNBAN** system.
*   **Content Management:** Add new Words and Categories on the fly.
*   **Game Control:** View active rooms, join any game, send `ANNOUNCE` messages or `SKIP ROUND`.

### 📥 Installation & Usage

No compilation required ("Self-Contained" deployment).

1.  Go to the **Releases** section and download the latest version.
2.  Unzip the folder.
3.  **Server:** Run `Server/TheMoleGame.Server.exe`.
4.  **Client:** Run `Client/TheMoleGame.Client.exe` and enter the Server IP.
5.  Register a new user and start playing!

> **Default Moderator Credentials:** `moderator` / `masterkey`

---

[🔼 Igo gora / Back to top](#the-mole-game)