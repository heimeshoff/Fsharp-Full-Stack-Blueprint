# F# Full-Stack Development Skills

This directory contains specialized skills for Claude Code when developing F# full-stack applications following the blueprint patterns.

## Skill Structure

Each skill is a directory containing a `SKILL.md` file with:
- **YAML frontmatter** - name, description, allowed-tools
- **When to Use** - Activation scenarios
- **Complete patterns** - Working code examples
- **Best practices** - Dos and don'ts
- **Checklists** - Verification steps

## Available Skills

### 🎯 `fsharp-feature/` - Complete Feature Development
**Orchestrates** end-to-end feature implementation

**Use when:**
- "Add X feature", "Implement Y"
- Building complete feature from scratch
- Need guidance through entire stack

**Guides through:** Shared types → Backend (validation/domain/persistence/API) → Frontend (state/view) → Tests

---

### 📦 `fsharp-shared/` - Types and API Contracts
**Defines** domain types and API interfaces

**Use when:**
- Starting new features (always start here)
- "Define X types", "Create Y entity"
- Modifying existing types

**Creates:** Records, discriminated unions, Fable.Remoting interfaces in `src/Shared/`

---

### ⚙️ `fsharp-backend/` - Backend Implementation
**Implements** server-side logic with proper separation

**Use when:**
- "Implement backend for X"
- Adding API endpoints
- Creating business logic

**Creates:** Validation → Domain (pure) → Persistence (I/O) → API in `src/Server/`

---

### ✅ `fsharp-validation/` - Validation Patterns
**Creates** comprehensive input validation

**Use when:**
- "Add validation for X"
- Implementing API endpoints (always validate)
- Complex validation rules

**Creates:** Field validators, entity validation, error accumulation in `src/Server/Validation.fs`

---

### 💾 `fsharp-persistence/` - Data Persistence
**Implements** database and file operations

**Use when:**
- "Add database table", "Save X to database"
- CRUD operations
- File storage or event sourcing

**Creates:** SQLite/Dapper, JSON files, event sourcing patterns in `src/Server/Persistence.fs`

---

### 🎨 `fsharp-frontend/` - Frontend (Elmish + Feliz)
**Implements** UI with MVU architecture

**Use when:**
- "Add UI for X", "Create component"
- Managing client state
- Interactive features

**Creates:** Model/Msg/Update/View in `src/Client/` with RemoteData pattern

---

### 🧪 `fsharp-tests/` - Testing with Expecto
**Writes** comprehensive tests

**Use when:**
- "Add tests for X"
- Implementing any feature (always write tests)
- Ensuring code quality

**Creates:** Unit tests, async tests, property tests in `src/Tests/`

---

## Quick Selection Guide

### By Task Type

| User Request | Primary Skill | Supporting Skills |
|--------------|---------------|-------------------|
| "Add todo feature" | `fsharp-feature` | All others as needed |
| "Define user types" | `fsharp-shared` | - |
| "Add validation" | `fsharp-validation` | `fsharp-backend` |
| "Add database table" | `fsharp-persistence` | `fsharp-backend` |
| "Implement API" | `fsharp-backend` | `fsharp-validation`, `fsharp-persistence` |
| "Add UI component" | `fsharp-frontend` | `fsharp-shared` |
| "Write tests" | `fsharp-tests` | Depends on what's tested |

### By Project Layer

| File Path | Skill |
|-----------|-------|
| `src/Shared/Domain.fs` | `fsharp-shared` |
| `src/Shared/Api.fs` | `fsharp-shared` |
| `src/Server/Validation.fs` | `fsharp-validation` |
| `src/Server/Domain.fs` | `fsharp-backend` |
| `src/Server/Persistence.fs` | `fsharp-persistence` |
| `src/Server/Api.fs` | `fsharp-backend` |
| `src/Client/State.fs` | `fsharp-frontend` |
| `src/Client/View.fs` | `fsharp-frontend` |
| `src/Tests/` | `fsharp-tests` |

## Usage Pattern

### For New Features
1. Invoke `fsharp-feature` for overview
2. Use specific skills as you work through each layer
3. Follow development order: **Shared → Backend → Frontend → Tests**

### For Specific Tasks
- Directly invoke the relevant skill
- Example: Need validation? → `fsharp-validation`
- Example: Need database? → `fsharp-persistence`

## Skill Activation

Claude Code automatically activates skills based on:
- **User requests** matching skill descriptions
- **Context** (files being edited, project structure)
- **Task type** (explicit skill invocation)

Each skill's `description` field in YAML frontmatter helps Claude recognize when to activate it.

## Key Principles (All Skills)

1. **Type Safety** - Define types in `src/Shared/` first
2. **Pure Domain** - NO I/O in `src/Server/Domain.fs`
3. **MVU Pattern** - All state through `update` function (frontend)
4. **Explicit Errors** - Use `Result<'T, string>` and `RemoteData<'T>`
5. **Validate Early** - At API boundary (backend)
6. **Test Coverage** - Especially domain logic and validation

## Common Workflows

**Adding a CRUD feature:**
```
fsharp-shared → fsharp-backend → fsharp-frontend → fsharp-tests
```

**Adding validation:**
```
fsharp-validation → integrate with fsharp-backend
```

**Adding UI component:**
```
fsharp-frontend (View section)
```

**Adding database table:**
```
fsharp-persistence → fsharp-backend
```

## Tool Restrictions

Skills use `allowed-tools` to control capabilities:
- Read-only skills: `Read, Grep, Glob`
- Edit skills: `Read, Edit, Write, Grep, Glob`
- Build skills: `Read, Edit, Write, Grep, Glob, Bash`

This ensures skills only access tools they need.

## Documentation Reference

Skills complement `/docs/`:
- **Skills** - Focused, task-oriented, code-heavy
- **Docs** - Comprehensive guides with explanations

**When implementing:**
1. Use relevant skill for immediate patterns
2. Check `/docs/09-QUICK-REFERENCE.md` for quick lookups
3. Consult specific `/docs/` guide for deep dives

## Benefits

✅ **Focused** - Each skill covers one area deeply
✅ **Discoverable** - Descriptions help Claude activate automatically
✅ **Complete** - Working examples with best practices
✅ **Layered** - Matches project architecture
✅ **Testable** - Easy to verify skill activation

## Skill Format

Each `SKILL.md` follows this structure:

```markdown
---
name: skill-name
description: |
  What it does and when to use it.
  Include trigger phrases users might say.
allowed-tools: Read, Edit, Write
---

# Skill Title

## When to Use This Skill
Activation scenarios...

## Content
Patterns, examples, best practices...

## Verification Checklist
Steps to verify completion...

## Related Skills
Other relevant skills...
```

## Testing Skills

To test if a skill activates:
1. Ask questions matching the description
2. Use `claude --debug` to see skill activation
3. Check that YAML frontmatter is valid
4. Verify file paths are correct

## Contributing Skills

When adding new skills:
1. Create directory with `SKILL.md`
2. Include YAML frontmatter (name + description)
3. Write specific, trigger-rich description
4. Include complete working examples
5. Add best practices and checklists
6. Cross-reference related skills
7. Update this README
