# EtherChess

EtherChess is a WPF chess client for the Ether platform.

## Current features

- Play against a local AI with selectable difficulty
- Full legal move filtering, including:
  - check detection
  - castling
  - en passant
  - pawn promotion
- Peer-to-peer multiplayer over TCP
- Ether user bootstrap through `ETHER_USER` and `ETHER_TOKEN`
- Local development mode with `--dev`

## Multiplayer

Two players can start a direct TCP game without a central server.

- **Host**: choose a port and wait for an opponent
- **Join**: enter the host IP and port

The host plays White. Moves are sent as newline-delimited JSON, acknowledged for round-trip timing, and rejected if they are illegal on the receiver board.

### Headless P2P check

```bash
dotnet run --project EtherChess.P2PTest/EtherChess.P2PTest.csproj
```

This launches two local peers, plays a live opening including castling, then a Scholar's mate, and asserts that both boards stay in sync.

## Running locally

### Requirements

- .NET 9 SDK
- Windows for the WPF desktop client

### Launch in development mode

```powershell
dotnet run -- --dev
```

### Launch with Ether context

Set these environment variables before starting the app:

- `ETHER_USER`: JSON string containing at least `username` and optionally `elo`
- `ETHER_TOKEN`: session token provided by Ether

Example:

```powershell
$env:ETHER_USER = '{"username":"Alice","elo":1520}'
$env:ETHER_TOKEN = 'example-token'
dotnet run
```

## Build

```powershell
./build.ps1
```

## Install on Windows at D:\ProjetsLocaux

From PowerShell on your PC:

```powershell
git clone --branch cursor/audit-remediation-bea0 https://github.com/Ryan-DPC/EtherChess.git D:\ProjetsLocaux\EtherChess
cd D:\ProjetsLocaux\EtherChess
dotnet run -- --dev
```

Or use the helper script:

```powershell
.\scripts\install-to-projets-locaux.ps1
```

## Project structure

- `EtherChess.Core/`: chess rules, AI, and P2P transport
- `ViewModels/`: MVVM presentation logic
- `Views/`: WPF views
- `EtherChess.P2PTest/`: headless two-peer live match and visual TCP demo
- `EngineSmokeTests/`: rule-level smoke tests

### Visual P2P demo

```bash
dotnet run --project EtherChess.P2PTest/EtherChess.P2PTest.csproj -- --visual
```

Then open `http://127.0.0.1:5088` to watch host and guest boards stay in sync.
