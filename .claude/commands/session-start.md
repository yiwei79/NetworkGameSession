# Session Start

**Recommended Model: Haiku** (lightweight context loading)

You are beginning a new development session for the "Loving Away" network game project.

## Automatic Context Loading

Read these files to understand current state:

1. **Docs/Workflow/PROJECT_STATUS.md** - Current phase, last session, next steps
2. **CLAUDE.md** - Master project context
3. **Latest SESSION_X_SUMMARY.md** - Previous session handoff (check Docs/Deliverable X/)

## Session Initialization

After reading context, provide:

### Status Summary
```
=== SESSION START ===
Project: Loving Away (Network Multiplayer Arena Shooter)
Branch: [current branch]
Last Session: [name from PROJECT_STATUS.md]
Current Phase: [phase X] - [progress]%

Next Planned Work:
[from PROJECT_STATUS.md "Next Session" section]
```

### Ask User
1. "Ready to continue with [planned next session]?"
2. "Or would you like to work on something else?"

## If Continuing Planned Work
- Run `/plan` to create session plan (if not exists)
- Then run `/implement` to begin coding

## If Different Work
- Clarify what user wants to accomplish
- Run `/plan` to create appropriate session plan

## Session Tracking
Once work begins, use `TodoWrite` to track tasks throughout the session.
Remember to run `/session-end` when finished to update documentation.
