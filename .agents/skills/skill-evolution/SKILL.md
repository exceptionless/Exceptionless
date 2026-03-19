---
name: skill-evolution
description: >
  Protocol for making skills self-improving over time. Use when you encounter a gap
  in an existing skill, when reviewing skill effectiveness, or when the docs agent
  processes accumulated skill gaps. Defines the observe-inspect-amend-evaluate cycle
  for skill maintenance.
---

# Skill Evolution

Skills are living documents, not static prompt files. This protocol defines how skills improve based on real usage patterns.

## The Problem

Skills that worked last month can silently degrade when:
- The codebase changes (new patterns, deprecated APIs, renamed modules)
- The kinds of tasks shift (new features, different complexity)
- Review findings reveal repeated gaps in guidance

Without a feedback loop, these failures are invisible until output quality drops.

## The Cycle

```
SKILL → RUN → OBSERVE → INSPECT → AMEND → EVALUATE → SKILL (improved)
```

### 1. Observe — Gap Detection

When any agent encounters something not covered by a skill during normal work, append a gap marker to the relevant skill file:

```markdown
<!-- SKILL-GAP: Missing guidance on [specific pattern] — encountered in [file] on [date] -->
```

**Rules:**
- Be specific — "missing CancellationToken propagation guidance" not "missing async stuff"
- Include the file where the gap was encountered
- Include the date for tracking
- Do NOT fix the skill inline during other work — just mark the gap

**Example:**
```markdown
<!-- SKILL-GAP: No guidance on Svelte 5 snippet pattern for reusable slot content — encountered in src/Exceptionless.Web/ClientApp/src/lib/components/ui/data-table.svelte on 2026-03-14 -->
```

### 2. Inspect — Pattern Recognition

Periodically (or on request via the `docs` agent), scan for accumulated gaps:

```bash
grep -r "SKILL-GAP" .agents/skills/ --include="*.md"
```

Group gaps by:
- **Frequency**: Same gap appearing 3+ times = high priority
- **Skill**: Multiple gaps in one skill = skill needs major update
- **Recency**: Recent gaps in previously stable skills = environment changed

### 3. Amend — Propose Changes

When enough evidence exists (3+ gaps on same topic, or 1 critical gap), update the skill:

1. Add the missing guidance to the appropriate section
2. Mark the amendment with a structured comment:
   ```markdown
   <!-- SKILL-AMENDMENT v{N} date:{YYYY-MM-DD} reason:"{why}" evidence:"{gap count or specific incident}" -->
   ```
3. Remove the resolved `SKILL-GAP` comments
4. Add a changelog entry

**Amendment types:**
- **Add**: New section or checklist item for uncovered pattern
- **Tighten**: More specific trigger conditions to reduce false matches
- **Reorder**: Move frequently-needed guidance higher
- **Update**: Change guidance that no longer matches codebase reality

### 4. Evaluate — Verify Improvement

Git history IS the evaluation system. After an amendment:

- If the `reviewer` agent stops flagging the pattern → amendment worked
- If the same gap reappears → amendment was insufficient, revisit
- If new issues appear in the amended area → amendment may have introduced confusion, consider rollback

To roll back: `git revert` the amendment commit. The original skill is preserved in history.

## Changelog Format

Every skill should have a `## Changelog` section at the bottom:

```markdown
## Changelog
- YYYY-MM-DD: Description of change (evidence: N gap markers / review finding / codebase change)
```

## Constraints

- **Never modify third-party skills** — check `skills-lock.json` before editing
- **One amendment per commit** — makes rollback granular
- **Human review for major changes** — if an amendment touches >30% of a skill, flag for review
- **Preserve existing structure** — amend within the skill's existing organization, don't restructure

## When to Trigger

| Trigger | Action |
|---------|--------|
| During any agent work, encounter undocumented pattern | Add `SKILL-GAP` comment |
| User asks to review/improve skills | Run full inspect cycle |
| `docs` agent runs | Check for gaps, propose amendments |
| After 5+ gaps accumulate in one skill | Priority amendment needed |
| After major codebase change (new framework, migration) | Audit all affected skills |
