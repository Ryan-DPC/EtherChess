# EtherChess

EtherChess is a WPF chess client for the Ether platform.

## Current features

- Play against a local AI with selectable difficulty
- Full legal move filtering, including:
  - check detection
  - castling
  - en passant
  - pawn promotion
- Ether user bootstrap through `ETHER_USER` and `ETHER_TOKEN`
- Local development mode with `--dev`

## Multiplayer status

Multiplayer networking is not enabled in the current release build yet. The UI keeps the entry point visible, but it shows an informational placeholder instead of attempting a broken connection flow.

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

## Project structure

- `Models/`: chess board state and move model
- `Engine/`: move generation and AI
- `ViewModels/`: MVVM presentation logic
- `Views/`: WPF views
- `Network/`: future multiplayer client
