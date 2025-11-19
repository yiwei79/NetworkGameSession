# Network Game Session - Educational Project

**Course:** Network Game Programming
**Project:** "Loving Away" - Multiplayer Physics-Based Arena Shooter
**Unity Version:** 6000.2.6f1 (Unity 6)
**Current Status:** Deliverable 3 (Serialization) - ✅ COMPLETE + ENHANCED

## Project Overview

This repository contains lab exercises and a final project for learning network game programming concepts, including:
- Threading and parallelization
- TCP/UDP networking fundamentals
- Binary serialization and protocol design
- Client-server architecture with prediction and interpolation

## Quick Links

### 📚 Documentation
- **[CLAUDE.md](CLAUDE.md)** - Comprehensive project guide for AI assistants
- **[Technical Implementation Plan](Docs/Final%20Project/Technical_Implementation_Plan.md)** - Complete roadmap and architecture
- **[Deliverable 3 Docs](Docs/Deliverable%203/)** - Current implementation documentation

### 🎮 Main Project
- **Location:** `Loving Away/Loving Away(Network Game)/`
- **Unity Scene:** Open project in Unity 6 and load the multiplayer test scene
- **Scripts:** `Assets/Scripts/` (Network/ and Gameplay/ folders)

### 🔧 Lab Exercises
- **Lab 1:** Threading fundamentals (`Pre-NetNet/` folder)
- **Lab 2-4:** Documentation in `Docs/Lab Session X/`

## Recent Updates (Nov 2025)

🎉 **Major Enhancement: Input Delay Resolution**
- Implemented input rate limiting (30 Hz)
- Added client-side prediction for instant local response
- Added sequence numbers to network protocol
- See [INPUT_DELAY_FIXES.md](Docs/Deliverable%203/INPUT_DELAY_FIXES.md) for details

## Getting Started

1. **Clone Repository:**
   ```bash
   git clone <repository-url>
   ```

2. **Open in Unity:**
   - Open Unity Hub
   - Add project: `Loving Away/Loving Away(Network Game)/`
   - Open with Unity 6000.2.6f1

3. **Run Demo:**
   - Open multiplayer test scene
   - Configure GameNetworkManager (set "Is Server" checkbox)
   - Press Play
   - Use WASD to move, Spacebar to charge shot

## Architecture Highlights

- **Server-Authoritative:** Server is single source of truth
- **Client-Side Prediction:** Local player moves instantly, reconciles with server
- **UDP Networking:** Low-latency gameplay state synchronization
- **Binary Serialization:** Efficient 18-byte input messages, 34-byte state updates

## Project Structure

```
NetworkGameSession/
├── CLAUDE.md                       # AI assistant guide
├── Docs/                           # All documentation
│   ├── Deliverable 3/              # Current work
│   └── Final Project/              # Vision & roadmap
├── Pre-NetNet/                     # Lab 1: Threading
└── Loving Away/                    # Main project (Unity)
    └── Loving Away(Network Game)/
        └── Assets/Scripts/
            ├── Network/            # UDP, serialization
            └── Gameplay/           # Game logic, player control
```

## Current Phase

**Phase 2-3:** Basic networking complete, full gameplay sync in progress
**Phase 4 (Partial):** Client-side prediction implemented early
**Next:** Projectile system, interpolation, server reconciliation

## Resources

- [Unity Documentation](https://docs.unity3d.com/)
- [Gabriel Gambetta's Multiplayer Networking](https://www.gabrielgambetta.com/client-server-game-architecture.html)
- [Valve's Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking)

---

**For detailed development guidance, start with [CLAUDE.md](CLAUDE.md)**
