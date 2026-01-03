# Session End

**Recommended Model: Sonnet** (cost-effective for documentation updates)

You are concluding a development session. This is the **back-propagation step** that updates all context for future sessions.

## Required Actions

### 1. Create/Update SESSION_X_SUMMARY.md
Location: `Docs/Deliverable X/SESSION_X_SUMMARY.md`

If session summary doesn't exist, create it with:
- What was implemented (file:line references)
- Architecture flow (ASCII diagram)
- Testing checklist (checked items)
- Known limitations
- Troubleshooting section
- **Next Session Context** (CRITICAL)
- Files modified table
- Git commit message template

### 2. Update PROJECT_STATUS.md
Location: `Docs/Workflow/PROJECT_STATUS.md`

Update:
- `Last Updated:` to today's date
- `Last Session:` to this session name
- Phase progress percentages
- Task checkboxes (mark completed tasks)
- Add entry to "Recent Sessions" table
- Update "Next Session" section

### 3. Git Commit (If User Approves)
Prepare commit with format:
```bash
git add .
git commit -m "feat(PhaseX-SessionY): Brief description

- Bullet point of what was done
- Another bullet point

Testing: How to verify this works
Next: What comes next"
```

### 4. Session Summary Output
```
=== SESSION END ===
Session: [name]
Duration: ~[X] hours
Completed:
- [task 1]
- [task 2]

Documentation Updated:
- ✅ SESSION_X_SUMMARY.md
- ✅ PROJECT_STATUS.md
- [✅/⏭️] CLAUDE.md

Git Status:
[output of git status --short]

Next Session Should:
1. [specific next step]
2. [another next step]

Ready to commit? (y/n)
```

## Back-Propagation Checklist
- [ ] SESSION_X_SUMMARY.md has "Next Session Context" section
- [ ] PROJECT_STATUS.md "Next Session" matches summary
- [ ] No uncommitted changes left undocumented
- [ ] Any new patterns added to CLAUDE.md (if applicable)

This ensures the next session (even months later) can pick up exactly where we left off.
