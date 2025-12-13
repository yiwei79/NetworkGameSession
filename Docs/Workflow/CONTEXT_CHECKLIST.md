# Context Checklist for New Sessions

> **Purpose:** Minimum reading required for any Claude Code session working on this project.

---

## Quick Start (Run This Command)

```
/session-start
```

This command automatically reads context and provides a status summary.

---

## Manual Context Loading

If not using `/session-start`, read these files in order:

### Tier 1: Essential (Always Read)
| File | Purpose | Location |
|------|---------|----------|
| PROJECT_STATUS.md | Current phase, last session, next steps | `Docs/Workflow/` |
| CLAUDE.md | Master project context, patterns, constraints | Root `/` |

### Tier 2: Session-Specific (Read If Relevant)
| File | Purpose | Location |
|------|---------|----------|
| Latest SESSION_X_SUMMARY.md | Previous session handoff | `Docs/Deliverable X/` |
| SESSION_X_PLAN.md | Current session plan (if exists) | `Docs/Deliverable X/` |

### Tier 3: Deep Dive (Read When Needed)
| File | Purpose | Location |
|------|---------|----------|
| Technical_Implementation_Plan.md | Full phase breakdown | `Docs/Final Project/` |
| Lab Session PDFs | Course concepts | `Docs/Materials/Lab Session X/` |
| INPUT_DELAY_FIXES.md | Phase 4 early work | `Docs/Deliverable 3/` |

---

## Available Slash Commands

| Command | Description | When to Use |
|---------|-------------|-------------|
| `/session-start` | Load context, show status | Beginning of session |
| `/status` | Quick status check | Anytime |
| `/plan` | Planning agent - create session plan | Before implementation |
| `/implement` | Implementation agent - write code | After plan approved |
| `/document` | Documentation agent - update docs | After implementation |
| `/test` | Testing agent - validate code | After implementation |
| `/session-end` | Back-propagate context, prepare commit | End of session |

---

## Workflow Pattern

```
[New Session]
     │
     ▼
/session-start  ──▶  Load context, show status
     │
     ▼
/plan  ──────────▶  Create SESSION_X_PLAN.md
     │
     ▼
/implement  ─────▶  Write code following plan
     │
     ▼
/test  ──────────▶  Validate implementation
     │
     ▼
/document  ──────▶  Update SESSION_X_SUMMARY.md
     │
     ▼
/session-end  ───▶  Update PROJECT_STATUS.md, git commit
```

---

## Key Project Facts (Quick Reference)

| Aspect | Value |
|--------|-------|
| Project | "Loving Away" - 2-4 player arena shooter |
| Unity Version | 6000.2.6f1 (Unity 6) |
| Input System | New Input System (UnityEngine.InputSystem) |
| Network Model | Server-authoritative, UDP, passive replication |
| Server Tick | 20 Hz |
| Client Send | 30 Hz |
| Branch | Phase_4 |

---

## Code Locations

| Component | File |
|-----------|------|
| Message structs | `Assets/Scripts/Network/NetworkProtocol.cs` |
| Serialization | `Assets/Scripts/Network/Serializer.cs` |
| Network I/O | `Assets/Scripts/Network/GameNetworkManager.cs` |
| Server logic | `Assets/Scripts/Gameplay/ServerGameState.cs` |
| Client logic | `Assets/Scripts/Gameplay/SimplePlayerController.cs` |
| Projectile | `Assets/Scripts/Gameplay/Projectile.cs` |

---

## Thread Safety Reminder

**Worker threads (NO Unity API):**
- `ServerProcess()`, `ClientProcess()` in GameNetworkManager.cs

**Main thread only (Unity API allowed):**
- `Update()`, `Start()`, event handlers

**Pattern:** Worker queues data → Main thread reads queue in Update()

---

*This checklist ensures any new Claude Code session can quickly understand and continue the project.*
