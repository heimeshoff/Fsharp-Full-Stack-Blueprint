# F# Full-Stack Web Application Architecture

## Overview

This document defines the architecture for F# web applications running on a Linux Mint home server with Docker/Portainer and Tailscale networking.

## Tech Stack

### Frontend
- **Framework**: Elmish.React + Feliz (MVU architecture)
- **Build Tool**: Vite with fable-plugin (Hot Module Replacement)
- **Styling**: TailwindCSS 4.3, DaisyUI (optional components)
- **Routing**: Feliz.Router
- **State Management**: Pure Elmish (Msg/Update pattern)

### Backend
- **Framework**: Giraffe (ASP.NET Core functional wrapper)
- **RPC**: Fable.Remoting (type-safe F#-to-F# communication)
- **Persistence**: SQLite + local file storage
- **Testing**: Expecto

### Shared
- **Domain Types**: Shared between client and server in `/src/Shared`
- **Deployment**: Single container, multi-stage Docker build

### Networking
- **Exposure**: Tailscale sidecar (tsnet) per application stack
- **Authentication**: Handled by Tailscale (private network only)
- **Isolation**: Each app gets its own Tailnet binding

## Core Principles

### 1. Type Safety End-to-End
- All domain types defined in `/src/Shared`
- Fable.Remoting ensures compile-time checking of API contracts
- No manual JSON serialization/deserialization in business logic

### 2. Pure Functional Architecture
- Immutable data structures throughout
- Pure functions for business logic
- Side effects isolated at boundaries (I/O, persistence)

### 3. MVU (Model-View-Update) Pattern
```fsharp
type Model = { /* application state */ }
type Msg = 
    | UserAction1
    | UserAction2 of data
    | ApiResponseReceived of Result<Data, Error>

let init () : Model * Cmd<Msg> = 
    // Initial state and commands

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    // State transitions

let view (model: Model) (dispatch: Msg -> unit) =
    // UI rendering
```

### 4. Simple Persistence
- SQLite for structured data requiring queries
- Local JSON/text files for simple state/configuration
- Event sourcing (append-only files) for audit trails when needed

### 5. No Runtime Authentication
- All apps are private by default (Tailscale-only)
- No login forms, JWT tokens, or session management
- Focus on business logic, not auth plumbing

## Project Structure

```
/
├── src/
│   ├── Shared/              # Shared domain types and contracts
│   │   ├── Domain.fs        # Core domain types
│   │   ├── Api.fs           # Fable.Remoting API contract
│   │   └── Shared.fsproj
│   │
│   ├── Client/              # Fable/Elmish frontend
│   │   ├── App.fs           # Main Elmish app
│   │   ├── Types.fs         # Client-only types
│   │   ├── State.fs         # Model, Msg, init, update
│   │   ├── View.fs          # UI components
│   │   ├── Api.fs           # Fable.Remoting client
│   │   ├── index.html       # HTML entry point
│   │   └── Client.fsproj
│   │
│   ├── Server/              # Giraffe backend
│   │   ├── Program.fs       # App entry point
│   │   ├── Api.fs           # Fable.Remoting implementation
│   │   ├── Persistence.fs   # Database/file access
│   │   ├── Domain.fs        # Server-side business logic
│   │   └── Server.fsproj
│   │
│   └── Tests/               # Expecto tests
│       ├── Client.Tests/
│       ├── Server.Tests/
│       └── Shared.Tests/
│
├── vite.config.js           # Vite configuration
├── tailwind.config.js       # TailwindCSS configuration
├── package.json             # npm dependencies
├── Dockerfile               # Multi-stage build
├── docker-compose.yml       # Stack definition with Tailscale sidecar
└── README.md                # Project-specific documentation
```

## Communication Flow

```
User Browser
    ↓
Client (Elmish/React)
    ↓ (Fable.Remoting - type-safe RPC)
Server (Giraffe)
    ↓
Persistence (SQLite / Files)
```

## Deployment Model

Each application stack consists of:
1. **App Container**: Built from multi-stage Dockerfile
   - Stage 1: dotnet SDK + Node.js (build both client and server)
   - Stage 2: dotnet runtime (serve static files + API)
2. **Tailscale Sidecar**: tsnet container bound to Tailnet
   - Provides hostname on private network
   - No ports exposed to public internet

```yaml
# docker-compose.yml pattern
services:
  app:
    build: .
    networks:
      - internal
  
  tailscale:
    image: tailscale/tailscale:latest
    environment:
      - TS_AUTHKEY=${TS_AUTHKEY}
      - TS_STATE_DIR=/var/lib/tailscale
      - TS_HOSTNAME=myapp
    volumes:
      - tailscale-data:/var/lib/tailscale
    networks:
      - internal
    cap_add:
      - NET_ADMIN
```

## Development Workflow

### 1. Start Development Server
```bash
# Terminal 1: Start backend (watches F# files)
cd src/Server
dotnet watch run

# Terminal 2: Start frontend (Vite HMR)
cd src/Client
npm run dev
```

### 2. Make Changes
- Frontend: Hot reload on save (Vite + fable-plugin)
- Backend: Automatic recompilation and restart (dotnet watch)
- Shared types: Triggers rebuild in both client and server

### 3. Run Tests
```bash
dotnet test
```

### 4. Build for Production
```bash
docker build -t myapp:latest .
```

### 5. Deploy to Portainer
- Upload docker-compose.yml to Portainer
- Set Tailscale auth key environment variable
- Deploy stack

## Key Technologies - When to Use What

### SQLite
- **Use for**: Structured data requiring queries, relationships, indexes
- **Example**: User preferences, application state with search/filter needs
- **Library**: `Microsoft.Data.Sqlite` or `Dapper` for queries

### Local Files (JSON)
- **Use for**: Simple configuration, small datasets, cache
- **Example**: App settings, feature flags, lookup tables
- **Pattern**: `System.Text.Json` serialization

### Event Sourcing (Append-only files)
- **Use for**: Audit trails, undo/redo, temporal queries
- **Example**: User activity log, command history
- **Pattern**: Append JSON lines, rebuild state on load

### Fable.Remoting
- **Use for**: All client-server communication
- **Benefit**: Type safety, no manual API contracts
- **Pattern**: Define API in Shared, implement in Server, call from Client

## Error Handling Philosophy

### Frontend (Elmish)
```fsharp
type RemoteData<'T> =
    | NotAsked
    | Loading
    | Success of 'T
    | Failure of string

// Always represent async operations as RemoteData in Model
```

### Backend (Giraffe)
```fsharp
// Use Result<'T, 'Error> for operations that can fail
type ApiResponse<'T> = Result<'T, string>

// Let Fable.Remoting serialize Result types automatically
```

## Performance Considerations

### Frontend
- Keep Model lean - no derived data (compute in view)
- Use `React.memo` for expensive components (via Feliz)
- Debounce rapid user input (search, autocomplete)

### Backend
- SQLite in WAL mode for concurrent reads
- Use async for all I/O operations
- Consider caching for expensive computations

### Docker
- Multi-stage builds keep final image small
- Static files served directly by Kestrel (no need for nginx)
- Volume mount for SQLite database persistence

## Security Model

### Tailscale Provides
- Network-level authentication (device authorization)
- Encrypted connections (WireGuard)
- Access control (Tailscale ACLs)

### Application Does Not Need
- Login forms or user authentication
- Password management
- Session handling or JWT tokens
- HTTPS certificates (handled by Tailscale)

### What You Still Handle
- Authorization (who can do what within the app)
- Input validation
- SQL injection prevention (parameterized queries)
- XSS prevention (React handles by default)

## Next Steps

Read the following guides in order:
1. `01-PROJECT-SETUP.md` - Initialize a new project
2. `02-FRONTEND-GUIDE.md` - Frontend development patterns
3. `03-BACKEND-GUIDE.md` - Backend development patterns
4. `04-SHARED-TYPES.md` - Type sharing patterns
5. `05-PERSISTENCE.md` - SQLite and file storage patterns
6. `06-TESTING.md` - Test strategies with Expecto
7. `07-BUILD-DEPLOY.md` - Docker build and deployment
8. `08-TAILSCALE-INTEGRATION.md` - Tailscale sidecar setup

## References

- [Elmish Documentation](https://elmish.github.io/elmish/)
- [Feliz Documentation](https://zaid-ajaj.github.io/Feliz/)
- [Giraffe Documentation](https://github.com/giraffe-fsharp/Giraffe)
- [Fable.Remoting Documentation](https://zaid-ajaj.github.io/Fable.Remoting/)
- [TailwindCSS Documentation](https://tailwindcss.com/)
- [DaisyUI Documentation](https://daisyui.com/)
