---
description: When and how to read and write shared project memory via the karavi-memory MCP server
alwaysApply: true
---

# Project memory (karavi-memory)

> Project slug: `ntk-asterisk` | Org: `ntk` | Fleet: `coding`
> Server: `https://api.memory.ai.ntk.ir/mcp`

Use the **karavi-memory** MCP server before guessing conventions, architecture,
or past decisions. Full protocol: `.cursor/skills/karavi-memory/SKILL.md`.

## Goals

1. **Code quality** — recall conventions, gotchas and decisions before writing
   code that violates them.
2. **Lower token usage** — one `search_memory` call instead of re-reading
   massive architecture and documentation files to re-derive a settled fact.

## Read (start of substantive work)

1. `list_projects` — verify or select the active project slug (`ntk-asterisk`).
2. `search_memory` — one focused query, `project="ntk-asterisk"` for repo-specific
   facts, `limit` 5–8.

## Save (after confirming a durable fact)

```
save_memory(content="One crisp sentence.", project="ntk-asterisk",
            category="convention", visibility="team")
```

- **Categories:** `convention`, `gotcha`, `decision`, `infra`, `preference`.
- **`content`, `project` and `category` are all required.** Content is stored
  verbatim — write the final sentence yourself.

## Visibility

| `visibility` | Who can read it |
|---|---|
| `agent` | Only this agent within the fleet |
| `team` | The whole fleet — **default** |
| `org` | The whole organization |

Save with **`team`** so all IDE agents (Cursor, Claude Code, Codex, Antigravity) share one memory.

## Authority Hierarchy

`karavi/karavi.doc/`, `karavi/README.md`, and project documentation outrank memory. When memory contradicts them, the repository documentation wins — say so, then correct the memory with `update_memory`.

## Do not save

- Secrets of any kind — passwords, connection strings, private tokens.
- Ephemeral session state, line numbers, or file paths that will shift.
