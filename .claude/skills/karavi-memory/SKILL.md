---
name: karavi-memory
description: Use when you need your project's shared memory — before answering questions about conventions, architecture, stack rules, gotchas, past decisions, or infra facts, and after learning something durable worth recording. Triggers on "search memory", "save memory", "remember this", "what is our convention for X", "why did we choose X", "check memory", karavi-memory, mem0.
---

# karavi-memory — Ntk.Asterisk

Shared, durable memory served by self-hosted KARAVI Mem0 server (`https://api.memory.ai.ntk.ir/mcp`).

| Parameter | Value |
|---|---|
| Server URL | `https://api.memory.ai.ntk.ir/mcp` |
| Organization | `ntk` (from `X-Mem0-Org` header) |
| Fleet | `coding` (from `X-Mem0-Fleet` header) |
| Project slug | **`ntk-asterisk`** |
| Visibility | `team` (default) |

---

## Authority

Repository documentation (`karavi/karavi.doc/`, `karavi/README.md`, project docs, `.cursor/rules/`) is authoritative. Memory supplements documentation.

---

## Tools

| Tool | Use |
|---|---|
| `search_memory` | Primary read path — semantic search within project (`project="ntk-asterisk"`) |
| `save_memory` | Record durable convention, gotcha, decision, or infra fact |
| `list_projects` | List projects with stored memories |
| `update_memory` | Correct stale memories |
| `delete_memory` | Remove invalid memories |
| `report_outcome`| Report memory utility feedback |

---

## Project Context Examples

- **Decision**: Ntk.Asterisk is a C# .NET Asterisk integration solution providing client libraries (Ntk.AsterNet.AMI, Ntk.AsterNet.ARI), an API-only WebApi control plane (Ntk.Asterisk.WebApi), Angular panels (AdminPanel, UserPanel), and a SipJS softphone (Ntk.Asterisk.WebPhone).
- **Convention**: src/DotNet/Ntk.Asterisk.WebApi is strictly API-only (AMI control plane, SignalR, Swagger, health) with NO UI or static files hosted; all UI surfaces live separately in src/Angular/AdminPanel, src/Angular/UserPanel, and src/WebPhone/Ntk.Asterisk.WebPhone.
- **Infra**: Local development port range is allocated strictly in 5310-5320 (WebApi: 5310, AdminPanel: 5314, UserPanel: 5312, WebPhone: 5316, Asterisk AMI: 5038, Asterisk ARI: 8088) per karavi/karavi.build.config/local-dev-ports.json.
- **Convention**: Mandatory Asterisk VoIP skills (.cursor/skills/asterisk-voip-stack, asterisk-dialplan, asterisk-agi, asterisk-ami, asterisk-callfile) must be followed for all dialplan, AGI, AMI, ARI, and call file implementations.
- **Gotcha**: Asterisk credentials and AMI/ARI connection secrets must never be committed to git; local deploy configs use Deploy_FTP.info and Deploy_TestUsers.info under karavi/karavi.deploy.config/ (gitignored).
- **Convention**: Agent planning, history, status reports, deploy configs, and operator scripts are centralized in karavi/ directory following the naming pattern {area}.{verb}-{subject}.ps1.
