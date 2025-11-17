# F# Full-Stack Development Guide for Claude Code

## Purpose

This documentation set provides comprehensive guidance for developing F# web applications with:
- **Frontend**: Elmish.React + Feliz (MVU architecture)
- **Backend**: Giraffe + Fable.Remoting
- **Persistence**: SQLite + local files
- **Deployment**: Docker + Portainer + Tailscale
- **Testing**: Expecto

These guides are designed to help Claude Code develop applications autonomously while maintaining consistency and best practices.

## Quick Start

### For New Projects

1. **Read**: `01-PROJECT-SETUP.md` to initialize project structure
2. **Reference**: `09-QUICK-REFERENCE.md` for common patterns
3. **Develop**: Follow patterns in `02-FRONTEND-GUIDE.md` and `03-BACKEND-GUIDE.md`
4. **Test**: Use patterns from `06-TESTING.md`
5. **Deploy**: Follow `07-BUILD-DEPLOY.md` and `08-TAILSCALE-INTEGRATION.md`

### For Adding Features to Existing Projects

1. **Read current code** to understand existing patterns
2. **Reference**: `09-QUICK-REFERENCE.md` for quick patterns
3. **Consult specific guides** as needed:
   - Types: `04-SHARED-TYPES.md`
   - Persistence: `05-PERSISTENCE.md`
   - Testing: `06-TESTING.md`

## Documentation Structure

```
📁 F# Full-Stack Documentation
├── 00-ARCHITECTURE.md ⭐ Start here for overview
├── 01-PROJECT-SETUP.md - Initialize new projects
├── 02-FRONTEND-GUIDE.md - Elmish + Feliz patterns
├── 03-BACKEND-GUIDE.md - Giraffe patterns
├── 04-SHARED-TYPES.md - Type design
├── 05-PERSISTENCE.md - Data storage
├── 06-TESTING.md - Test strategies
├── 07-BUILD-DEPLOY.md - Docker deployment
├── 08-TAILSCALE-INTEGRATION.md - Networking
├── 09-QUICK-REFERENCE.md ⭐ Quick lookup
└── README.md - This file
```

## How to Use These Guides

### As Claude Code

When developing features:

1. **Understand the requirement**: What is the user asking for?

2. **Consult the relevant guide**:
   - New feature with API? → `02-FRONTEND-GUIDE.md` + `03-BACKEND-GUIDE.md`
   - New domain type? → `04-SHARED-TYPES.md`
   - Database changes? → `05-PERSISTENCE.md`
   - Tests needed? → `06-TESTING.md`

3. **Follow the patterns**: Don't reinvent - use established patterns from guides

4. **Check Quick Reference**: `09-QUICK-REFERENCE.md` has common code templates

5. **Maintain consistency**: Match existing code style and patterns

### Development Workflow

```
User Request
    ↓
Read Relevant Documentation
    ↓
Plan Implementation
    ↓
Write Code (following patterns)
    ↓
Write Tests
    ↓
Verify Build
    ↓
Present to User for Review
```

## Key Principles

### 1. Type Safety First
- Define types in `Shared` before implementing
- Use `Result<'T, string>` for fallible operations
- Leverage F#'s type system to prevent errors

### 2. Pure Functions
- Keep domain logic in `src/Server/Domain.fs`
- No side effects in business logic
- I/O only in `Persistence.fs` and `Api.fs`

### 3. MVU Architecture
- All state changes through `update` function
- Commands for side effects
- View is pure function of model

### 4. Explicit State
- Use `RemoteData<'T>` for async operations
- Don't hide loading/error states
- Make impossible states impossible

### 5. Test Coverage
- Test domain logic thoroughly (pure functions)
- Test API contracts
- Integration tests for critical paths

## Common Patterns Quick Reference

### Adding a New Feature (Full Stack)

**Step 1**: Define types in `src/Shared/Domain.fs`
```fsharp
type NewItem = {
    Id: int
    Name: string
}
```

**Step 2**: Add API contract in `src/Shared/Api.fs`
```fsharp
type INewItemApi = {
    getNewItems: unit -> Async<NewItem list>
}
```

**Step 3**: Implement backend in `src/Server/Api.fs`
```fsharp
let newItemApi : INewItemApi = {
    getNewItems = fun () -> Persistence.getNewItems()
}
```

**Step 4**: Update client state in `src/Client/State.fs`
```fsharp
type Model = {
    // ... existing fields
    NewItems: RemoteData<NewItem list>
}

type Msg =
    // ... existing cases
    | LoadNewItems
    | NewItemsLoaded of Result<NewItem list, string>
```

**Step 5**: Update view in `src/Client/View.fs`
```fsharp
// Add rendering logic
```

**Step 6**: Write tests in `src/Tests/`

### Adding Persistence

**SQLite** (for structured data):
```fsharp
// In Persistence.fs
let getAllNewItems () : Async<NewItem list> =
    async {
        use conn = getConnection()
        let! items = conn.QueryAsync<NewItem>(
            "SELECT * FROM new_items"
        ) |> Async.AwaitTask
        return items |> Seq.toList
    }
```

**JSON File** (for simple config):
```fsharp
// In Persistence.fs
let loadConfig () : Async<Config> =
    JsonFile.read<Config> "./data/config.json"
```

### Adding Tests

```fsharp
[<Tests>]
let newFeatureTests =
    testList "New Feature" [
        testCase "Test case name" <| fun () ->
            // Arrange
            let input = ...
            
            // Act
            let result = ...
            
            // Assert
            Expect.equal result expected "Message"
    ]
```

## Anti-Patterns to Avoid

### ❌ Don't: Put I/O in Domain Logic
```fsharp
// BAD
let processItem item =
    let fromDb = Persistence.getItem item.Id  // I/O in domain!
    // ... logic
```

### ✅ Do: Keep Domain Pure
```fsharp
// GOOD
let processItem item otherItem =
    // Pure logic, no I/O
    { item with Value = item.Value + otherItem.Value }
```

### ❌ Don't: Use Classes for Domain Types
```fsharp
// BAD
type Item() =
    member val Id = 0 with get, set
    member val Name = "" with get, set
```

### ✅ Do: Use Records
```fsharp
// GOOD
type Item = {
    Id: int
    Name: string
}
```

### ❌ Don't: Ignore Errors
```fsharp
// BAD
let! items = api.getItems()
// What if it fails?
```

### ✅ Do: Handle Errors Explicitly
```fsharp
// GOOD
match! api.getItems() with
| Ok items -> // Handle success
| Error msg -> // Handle error
```

## Project Structure Conventions

```
src/
├── Shared/
│   ├── Domain.fs        # Core business types
│   └── Api.fs           # API contracts
│
├── Client/
│   ├── Types.fs         # Client-only types (RemoteData, Page, etc.)
│   ├── Api.fs           # Fable.Remoting client setup
│   ├── State.fs         # Model, Msg, init, update
│   ├── View.fs          # UI components
│   └── App.fs           # Application entry point
│
├── Server/
│   ├── Persistence.fs   # Database and file I/O
│   ├── Domain.fs        # Business logic (pure)
│   ├── Validation.fs    # Input validation
│   ├── Api.fs           # Fable.Remoting implementation
│   └── Program.fs       # Server entry point
│
└── Tests/
    ├── Shared.Tests/
    ├── Client.Tests/
    └── Server.Tests/
```

## When to Consult Which Guide

| Task | Guide | Section |
|------|-------|---------|
| Starting new project | 01-PROJECT-SETUP.md | Full guide |
| Adding UI component | 02-FRONTEND-GUIDE.md | Component Organization |
| Adding state | 02-FRONTEND-GUIDE.md | State.fs Structure |
| Adding API endpoint | 03-BACKEND-GUIDE.md | Api.fs |
| Validating input | 03-BACKEND-GUIDE.md | Validation.fs |
| Adding domain type | 04-SHARED-TYPES.md | Domain.fs |
| Adding database table | 05-PERSISTENCE.md | SQLite Setup |
| Reading/writing files | 05-PERSISTENCE.md | File-Based Persistence |
| Event sourcing | 05-PERSISTENCE.md | Event Sourcing |
| Writing tests | 06-TESTING.md | Testing Patterns |
| Building Docker image | 07-BUILD-DEPLOY.md | Dockerfile |
| Deploying to Portainer | 07-BUILD-DEPLOY.md | Deployment |
| Setting up Tailscale | 08-TAILSCALE-INTEGRATION.md | Full guide |
| Quick code lookup | 09-QUICK-REFERENCE.md | Full guide |

## Troubleshooting

### Build Issues
1. Check `01-PROJECT-SETUP.md` - Project Checklist
2. Verify all `.fsproj` files have correct references
3. Run `dotnet restore` and `npm install`

### Runtime Issues
1. Check `07-BUILD-DEPLOY.md` - Troubleshooting section
2. Verify environment variables
3. Check Docker logs: `docker logs my-app`

### Type Errors
1. Ensure Shared project builds first
2. Check `04-SHARED-TYPES.md` for type design patterns
3. Rebuild solution: `dotnet build`

### API Not Working
1. Verify route builder matches in client and server
2. Check `03-BACKEND-GUIDE.md` - Api.fs section
3. Check network: `curl http://localhost:5000/api/TypeName/MethodName`

### Tailscale Issues
1. Consult `08-TAILSCALE-INTEGRATION.md` - Troubleshooting
2. Check logs: `docker logs my-app-tailscale`
3. Verify auth key is valid

## Best Practices Checklist

Before considering a feature complete:
- [ ] Types defined in `Shared`
- [ ] API contract defined
- [ ] Backend implementation follows patterns
- [ ] Frontend state management follows MVU
- [ ] Error handling is explicit (Result types)
- [ ] Validation implemented
- [ ] Tests written (at least for domain logic)
- [ ] Code matches existing style
- [ ] No I/O in domain logic
- [ ] Documentation updated if needed

## Tech Stack at a Glance

| Layer | Technology | Version |
|-------|-----------|---------|
| Language | F# | 8.0 |
| Frontend Framework | Elmish.React | 4.0+ |
| UI Library | Feliz | 2.9+ |
| CSS Framework | TailwindCSS | 4.3 |
| Component Library | DaisyUI | 4.12+ |
| Build Tool | Vite | 5.4+ |
| Backend Framework | Giraffe | 6.4+ |
| RPC | Fable.Remoting | 5.16+ |
| Database | SQLite | via Microsoft.Data.Sqlite |
| Query Builder | Dapper | 2.1+ |
| Test Framework | Expecto | 10.2+ |
| Runtime | .NET | 8.0 |
| Container | Docker | Latest |
| Networking | Tailscale | Latest |

## Additional Resources

### External Documentation
- [F# for Fun and Profit](https://fsharpforfunandprofit.com/)
- [Elmish Documentation](https://elmish.github.io/)
- [Feliz Documentation](https://zaid-ajaj.github.io/Feliz/)
- [Giraffe Documentation](https://github.com/giraffe-fsharp/Giraffe)
- [Fable.Remoting](https://zaid-ajaj.github.io/Fable.Remoting/)
- [TailwindCSS](https://tailwindcss.com/)
- [DaisyUI](https://daisyui.com/)

### Internal Guides
Start with `00-ARCHITECTURE.md` for the big picture, then dive into specific guides as needed.

## Conclusion

These guides provide comprehensive, battle-tested patterns for F# full-stack development. Follow the patterns, consult the guides, and maintain consistency. The goal is to help Claude Code develop features autonomously while ensuring high quality and maintainability.

**Remember**: 
- When in doubt, check the guides
- Follow established patterns
- Keep it simple
- Type safety first
- Test your code

Happy coding! 🚀
