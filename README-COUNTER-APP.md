# F# Counter App

A simple full-stack counter application built with F#, demonstrating:
- **Frontend**: Elmish + Feliz (MVU pattern)
- **Backend**: Giraffe + Fable.Remoting
- **Persistence**: File-based storage (JSON)
- **Deployment**: Docker + Docker Compose

## Features

- Click a button to increment a counter
- Counter value is persisted on the backend in a JSON file
- Fully containerized for easy deployment
- Type-safe communication between client and server
- Beautiful UI with TailwindCSS and DaisyUI

## Architecture

```
┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐
│   Frontend      │─────▶│   Backend API   │─────▶│  File Storage   │
│  (Elmish+Feliz) │◀─────│ (Giraffe+F.R.)  │◀─────│  (counter.json) │
└─────────────────┘      └─────────────────┘      └─────────────────┘
```

## Project Structure

```
/
├── src/
│   ├── Shared/         # Shared types and API contracts
│   │   ├── Domain.fs   # Counter domain type
│   │   └── Api.fs      # API interface
│   ├── Client/         # Frontend (Fable/Elmish)
│   │   ├── Types.fs    # RemoteData type
│   │   ├── Api.fs      # API client proxy
│   │   ├── State.fs    # MVU state management
│   │   ├── View.fs     # Feliz views
│   │   ├── App.fs      # Elmish entry point
│   │   └── index.html  # HTML entry point
│   └── Server/         # Backend (Giraffe)
│       ├── Persistence.fs  # File-based storage
│       ├── Api.fs      # API implementation
│       └── Program.fs  # ASP.NET Core setup
├── Dockerfile          # Multi-stage Docker build
├── docker-compose.yml  # Docker Compose configuration
├── vite.config.js      # Vite bundler config
├── tailwind.config.js  # TailwindCSS config
└── package.json        # NPM dependencies
```

## Quick Start with Docker

### Prerequisites

- Docker
- Docker Compose

### Deploy Locally

1. **Build and run**:
   ```bash
   docker-compose up -d
   ```

2. **Access the app**:
   Open your browser to http://localhost:5000

3. **View logs**:
   ```bash
   docker-compose logs -f
   ```

4. **Stop the app**:
   ```bash
   docker-compose down
   ```

### Data Persistence

The counter value is stored in `./data/counter.json` on the host machine, which is mounted as a volume in the container. This ensures the counter persists across container restarts.

## Deploy with Portainer

### Method 1: Using Portainer Stacks

1. **Login to Portainer**

2. **Navigate to Stacks** → Add Stack

3. **Name your stack**: `fsharp-counter-app`

4. **Paste the docker-compose.yml content** or upload the file

5. **Deploy the stack**

6. **Access your app** at http://your-server-ip:5000

### Method 2: Using Git Repository

1. **Push this repo to your Git server**

2. **In Portainer**: Stacks → Add Stack → Git Repository

3. **Configure**:
   - Repository URL: `https://your-git-server/your-repo.git`
   - Repository reference: `main`
   - Compose path: `docker-compose.yml`

4. **Deploy**

### Method 3: Build Image Separately

1. **Build the image**:
   ```bash
   docker build -t fsharp-counter-app:latest .
   ```

2. **Push to your registry** (optional):
   ```bash
   docker tag fsharp-counter-app:latest your-registry/fsharp-counter-app:latest
   docker push your-registry/fsharp-counter-app:latest
   ```

3. **In Portainer**:
   - Containers → Add Container
   - Name: `fsharp-counter-app`
   - Image: `fsharp-counter-app:latest`
   - Port mapping: `5000:5000`
   - Volumes: Mount `./data` to `/app/data`
   - Deploy

## Development (Local)

### Prerequisites

- .NET 8 SDK
- Node.js 18+
- npm

### Setup

1. **Install dependencies**:
   ```bash
   npm install
   dotnet restore
   ```

2. **Start backend** (Terminal 1):
   ```bash
   cd src/Server
   dotnet watch run
   ```

3. **Start frontend** (Terminal 2):
   ```bash
   npm run dev
   ```

4. **Open browser**: http://localhost:5173

### Building for Production

```bash
# Build frontend
npm run build

# Build backend
dotnet publish src/Server -c Release -o ./publish
```

## API Endpoints

The app uses Fable.Remoting for type-safe RPC:

- `GET /api/ICounterApi/getCounter` - Get current counter value
- `POST /api/ICounterApi/incrementCounter` - Increment and return new value

## Configuration

### Environment Variables

- `ASPNETCORE_URLS` - Server listening URL (default: `http://+:5000`)
- `ASPNETCORE_ENVIRONMENT` - Environment (Development/Production)

### Ports

- Frontend dev server: `5173` (Vite)
- Backend server: `5000` (Kestrel)
- Production (Docker): `5000`

## Customization

### Change Port

Edit `docker-compose.yml`:
```yaml
ports:
  - "8080:5000"  # Map host port 8080 to container port 5000
```

### Change Data Directory

Edit `docker-compose.yml`:
```yaml
volumes:
  - /your/custom/path:/app/data
```

## Troubleshooting

### Container won't start

```bash
# Check logs
docker-compose logs

# Check if port 5000 is already in use
lsof -i :5000  # Linux/Mac
netstat -ano | findstr :5000  # Windows
```

### Data not persisting

- Ensure the `./data` directory exists on the host
- Check volume mount in `docker-compose.yml`
- Verify container has write permissions

### Build fails

```bash
# Clean Docker cache and rebuild
docker-compose down
docker system prune -a
docker-compose build --no-cache
docker-compose up -d
```

## Tech Stack Details

- **Language**: F# 8
- **Frontend Framework**: Elmish.React 4.0 + Feliz 2.9
- **Backend Framework**: Giraffe 6.4
- **RPC**: Fable.Remoting 7.32 / 5.16
- **Bundler**: Vite 7.0
- **CSS**: TailwindCSS 4.0 + DaisyUI 4.12
- **Runtime**: .NET 8.0

## License

MIT

## Next Steps

- Add authentication
- Add more counter operations (decrement, reset)
- Add multiple counters
- Add SQLite persistence
- Add Tailscale integration for private deployment

---

For more information about the F# full-stack blueprint patterns, see the `/docs` directory.
