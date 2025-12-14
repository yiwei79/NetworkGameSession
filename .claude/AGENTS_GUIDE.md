# Claude Code Agents Guide

This project now uses **official Claude Code agents** alongside slash commands for a hybrid agentic workflow.

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    HYBRID WORKFLOW SYSTEM                    │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Slash Commands           ←→        Agents                  │
│  (.claude/commands/)                (.claude/agents/)        │
│                                                              │
│  Manual triggers                    Auto/explicit invoke    │
│  Load prompts                       Specialized AI          │
│  Quick actions                      Task delegation         │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## Available Agents

Run `/agents` to see all agents, or invoke them explicitly:

| Agent Name | Model | Purpose | When to Use |
|------------|-------|---------|-------------|
| `session-coordinator-opus` | Opus 4.5 | Orchestrates workflow phases | Session start/end, workflow coordination |
| `planning-agent-opus` | Opus 4.5 | Creates implementation plans | New features, architectural decisions |
| `implementation-agent-sonnet` | Sonnet 4.5 | Writes production code | Feature implementation, bug fixes |
| `testing-agent-sonnet` | Sonnet 4.5 | Quality assurance | After implementation, debugging |
| `documentation-agent-sonnet` | Sonnet 4.5 | Updates project docs | Session end, after features complete |

## How to Use Agents

### Method 1: Automatic Invocation

Claude will automatically use agents when it detects relevant work:

```
> I need to implement hit detection for projectiles

[Claude automatically invokes planning-agent to create plan]
[Then invokes implementation-agent to write code]
[Then invokes testing-agent to validate]
[Finally invokes documentation-agent to update docs]
```

### Method 2: Explicit Invocation

Tell Claude which agent to use:

```
> Use the planning-agent to plan the projectile collision system
> Have the implementation-agent add the network message
> Ask the testing-agent to validate multiplayer sync
> Use the documentation-agent to update the session summary
```

### Method 3: Via Slash Commands

Your existing slash commands can trigger agents:

```
> /session-start
[Invokes session-coordinator agent]

> /plan
[Can invoke planning-agent-opus for detailed plans]

> /implement
[Can invoke implementation-agent-sonnet for coding]

> /test
[Can invoke testing-agent-sonnet for validation]

> /document
[Can invoke documentation-agent-sonnet for updates]
```

## Slash Commands vs. Agents

**Slash Commands** (`.claude/commands/`) - Keep using these for:
- ✅ Quick workflow triggers (`/status`, `/session-start`)
- ✅ Manual phase transitions
- ✅ Loading specific prompts
- ✅ User-controlled orchestration

**Agents** (`.claude/agents/`) - Use for:
- ✅ Specialized task delegation
- ✅ Automatic expertise application
- ✅ Complex multi-step work
- ✅ Maintaining consistent patterns

**They work together!** Slash commands can invoke agents, and agents can suggest slash commands.

## Recommended Workflows

### Workflow 1: Full Session (Traditional)

```bash
/session-start          # Loads context, presents status
/plan                   # Create session plan
/implement              # Code the feature
/test                   # Validate implementation
/document               # Update docs
/session-end            # Create handoff
```

**Behind the scenes:** These slash commands can invoke agents as needed.

### Workflow 2: Agent-Driven Session (New)

```
> Start a new session
[session-coordinator-opus agent loads context]

> Plan and implement projectile hit detection
[planning-agent-opus creates plan]
[implementation-agent-sonnet writes code]
[testing-agent-sonnet validates]
[documentation-agent-sonnet updates docs]

> End the session
[session-coordinator-opus creates handoff]
```

### Workflow 3: Hybrid (Recommended)

```bash
/session-start          # Manual trigger for familiarity

> Implement hit detection
[Agents automatically handle planning → coding → testing]

/status                 # Quick check (manual)

> Document what we did
[documentation-agent-sonnet updates docs]

/session-end            # Manual closure
```

## Agent Details

### session-coordinator-opus

**Specialization:** Workflow orchestration
**Model:** Opus 4.5 (deep reasoning for complex coordination)
**Tools:** Read, Write, Edit, Glob, Grep, Bash, TodoWrite, Task

**Use when:**
- Starting a new session
- Need help coordinating between phases
- Ending a session and creating handoff
- Unsure what to do next

**Example:**
```
> Use session-coordinator-opus to start this session
> Have session-coordinator-opus help me close this session properly
```

### planning-agent-opus

**Specialization:** Software architecture, implementation planning
**Model:** Opus 4.5 (best for architectural decisions and complex planning)
**Tools:** Read, Write, Glob, Grep, TodoWrite

**Use when:**
- Starting a new feature
- Need architectural decisions
- Want task breakdown
- Unclear how to implement something

**Example:**
```
> Use planning-agent-opus to design the interpolation system
> Planning agent opus: how should we handle projectile collision?
```

### implementation-agent-sonnet

**Specialization:** Writing code following project patterns
**Model:** Sonnet 4.5 (cost-effective for straightforward coding)
**Tools:** Read, Write, Edit, Glob, Grep, Bash, TodoWrite

**Use when:**
- Implementing features
- Fixing bugs
- Modifying existing code
- Adding network messages

**Example:**
```
> Implementation agent sonnet: add the ProjectileHitMessage
> Use implementation-agent-sonnet to fix the threading bug
```

### testing-agent-sonnet

**Specialization:** QA, validation, edge case discovery
**Model:** Sonnet 4.5 (efficient for systematic testing)
**Tools:** Read, Bash, Glob, Grep, TodoWrite

**Use when:**
- After implementing features
- Debugging issues
- Need comprehensive test coverage
- Validating multiplayer sync

**Example:**
```
> Testing agent sonnet: validate the hit detection works in multiplayer
> Use testing-agent-sonnet to find edge cases in projectile spawning
```

### documentation-agent-sonnet

**Specialization:** Maintaining project documentation
**Model:** Sonnet 4.5 (efficient for documentation tasks)
**Tools:** Read, Write, Edit, Glob, Grep, Bash

**Use when:**
- After completing features
- Session end
- Major changes made
- Need to update PROJECT_STATUS.md

**Example:**
```
> Documentation agent sonnet: update the session summary
> Use documentation-agent-sonnet to create SESSION_3_SUMMARY.md
```

## Benefits of This System

### ✅ Continuity
- Agents know project patterns automatically
- Consistent code quality across sessions
- Less "how do I do X again?" moments

### ✅ Specialization
- Each agent is an expert in its domain
- Better planning, coding, testing, documentation
- Agents maintain quality standards

### ✅ Flexibility
- Use slash commands for manual control
- Let agents auto-delegate for automation
- Mix both approaches as needed

### ✅ Context Preservation
- Agents read the same handoff docs you created
- Back-propagation system still works
- Future sessions start with full context

## Migration from Old System

**What changed:**
- ❌ Old: Slash commands contained all instructions
- ✅ New: Slash commands + specialized agents

**What stayed the same:**
- ✅ `/session-start` still loads context
- ✅ `/status` still shows quick status
- ✅ PROJECT_STATUS.md is still the source of truth
- ✅ SESSION_X_SUMMARY.md handoffs still work
- ✅ Back-propagation documentation system unchanged

**What improved:**
- ✨ Agents can be invoked automatically
- ✨ More consistent code patterns
- ✨ Better separation of concerns
- ✨ Deeper expertise per phase

## Quick Reference

**List available agents:**
```bash
/agents
```

**Invoke specific agent:**
```
> Use the [agent-name] to [task]
> Have [agent-name] [task]
> Ask [agent-name] to [task]
```

**Traditional workflow:**
```bash
/session-start → /plan → /implement → /test → /document → /session-end
```

**Agent-driven workflow:**
```
Start session → Plan feature → Implement → Test → Document → End session
[Claude automatically delegates to appropriate agents]
```

## Best Practices

1. **Start sessions with coordination**
   - Use `/session-start` or ask session-coordinator
   - Loads context properly

2. **Let agents specialize**
   - Don't micromanage - agents know project patterns
   - Trust the planning-agent for architecture
   - Trust the implementation-agent for code quality

3. **Keep using TodoWrite**
   - Agents use it automatically
   - You can see progress in real-time

4. **End sessions properly**
   - Use `/session-end` or session-coordinator
   - Ensures handoff documentation complete

5. **Mix manual and automatic**
   - Use slash commands when you want control
   - Let agents auto-delegate for complex work
   - Both approaches are valid!

## Troubleshooting

**Agents not showing in `/agents`?**
- Check files have proper YAML frontmatter
- Ensure files are in `.claude/agents/` directory
- Restart Claude Code if needed

**Agent not invoking automatically?**
- Try explicit invocation: "Use the X agent to Y"
- Check agent description matches your task
- Agents are smart but not mind readers!

**Want to customize agents?**
- Edit `.claude/agents/[agent-name].md`
- Modify the system prompt (content after `---`)
- Adjust tools, model, or permissions in YAML

## Integration with Git

All agent files should be committed to version control:

```bash
git add .claude/agents/
git commit -m "feat: Add official Claude Code agents integration"
```

This ensures:
- Team members get the same agents
- Agents are versioned with the project
- Future sessions have access to agents

---

**Created:** 2025-12-14
**Integration:** Official Claude Code agents + existing slash command workflow
**Documentation:** Part of the Loving Away agentic workflow system
