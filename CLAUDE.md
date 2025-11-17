# F# Full-Stack Web Application

## Documentation

All guides are in `/docs/` directory. **Read before implementing!**

- `README.md` - How to use guides
- `09-QUICK-REFERENCE.md` - Code templates
- `02-FRONTEND-GUIDE.md` - Elmish + Feliz
- `03-BACKEND-GUIDE.md` - Giraffe patterns
- `04-SHARED-TYPES.md` - Type design
- `05-PERSISTENCE.md` - Database patterns

## Tech Stack

- Frontend: Elmish.React + Feliz, Vite, TailwindCSS 4.3
- Backend: Giraffe + Fable.Remoting
- Persistence: SQLite + JSON files
- Testing: Expecto
- Deployment: Docker + Tailscale sidecar

## Development Workflow

### Before Any Feature
1. Read relevant guide from `/docs/`
2. Check `09-QUICK-REFERENCE.md` for templates
3. Follow established patterns

### Adding a Feature
1. Define types in `src/Shared/Domain.fs`
2. Add API contract in `src/Shared/Api.fs`
3. Implement backend: Validation → Domain → Persistence → API
4. Update client: State (Model/Msg/update) → View
5. Write tests (at least domain + validation)

## Key Principles

- **Type safety first**: Define types in Shared
- **Pure domain**: No I/O in `Domain.fs`
- **MVU architecture**: All state through `update`
- **RemoteData pattern**: For async operations
- **Result types**: For fallible operations
- **Validate early**: At API boundary

## Common Patterns

### Backend API
```fsharp
let api : IEntityApi = {
    getAll = fun () -> Persistence.getAllEntities()
    getById = fun id -> async {
        match! Persistence.getEntityById id with
        | Some e -> return Ok e
        | None -> return Error "Not found"
    }
    save = fun entity -> async {
        match Validation.validate entity with
        | Error errs -> return Error (String.concat ", " errs)
        | Ok valid ->
            let processed = Domain.process valid
            do! Persistence.save processed
            return Ok processed
    }
}
```

### Client State
```fsharp
type Model = { Entities: RemoteData }
type Msg = LoadEntities | EntitiesLoaded of Result

let update msg model =
    match msg with
    | LoadEntities ->
        let cmd = Cmd.OfAsync.either Api.api.getAll () (Ok >> EntitiesLoaded) (Error >> EntitiesLoaded)
        { model with Entities = Loading }, cmd
    | EntitiesLoaded (Ok entities) ->
        { model with Entities = Success entities }, Cmd.none
    | EntitiesLoaded (Error err) ->
        { model with Entities = Failure err }, Cmd.none
```

## Anti-Patterns

❌ I/O in domain logic
❌ Ignoring Result types  
❌ Using classes for domain
❌ Not reading documentation

✅ Pure domain functions
✅ Explicit error handling
✅ Records for domain types
✅ Following documented patterns

## Quick Commands
```bash
# Dev
dotnet watch run          # Backend
npm run dev               # Frontend
dotnet test --watch       # Tests

# Build
docker build -t app .
docker-compose up -d
```

## Success Checklist

- [ ] Types in Shared
- [ ] API contract defined
- [ ] Validation implemented
- [ ] Domain logic pure
- [ ] Persistence added
- [ ] Client state (MVU)
- [ ] Client view (Feliz)
- [ ] Tests written
- [ ] Follows patterns

**Remember**: Check `/docs/09-QUICK-REFERENCE.md` first!