# Project Setup Guide

## Prerequisites

Ensure the following are installed on the development machine:
- .NET 8 SDK
- Node.js 18+ and npm
- Docker and Docker Compose

## Creating a New Project

### Step 1: Initialize Directory Structure

```bash
mkdir my-app
cd my-app
mkdir -p src/{Shared,Client,Server,Tests}
```

### Step 2: Create Shared Project

```bash
cd src/Shared
dotnet new classlib -lang F# -n Shared
rm Library.fs
```

**Shared/Shared.fsproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Domain.fs" />
    <Compile Include="Api.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Fable.Remoting.Giraffe" Version="5.16.0" />
  </ItemGroup>
</Project>
```

### Step 3: Create Server Project

```bash
cd ../Server
dotnet new console -lang F# -n Server
rm Program.fs
```

**Server/Server.fsproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Persistence.fs" />
    <Compile Include="Domain.fs" />
    <Compile Include="Api.fs" />
    <Compile Include="Program.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Giraffe" Version="6.4.0" />
    <PackageReference Include="Fable.Remoting.Giraffe" Version="5.16.0" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
    <PackageReference Include="Dapper" Version="2.1.35" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../Shared/Shared.fsproj" />
  </ItemGroup>
</Project>
```

### Step 4: Create Client Project

```bash
cd ../Client
dotnet new console -lang F# -n Client
rm Program.fs
```

**Client/Client.fsproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Types.fs" />
    <Compile Include="Api.fs" />
    <Compile Include="State.fs" />
    <Compile Include="View.fs" />
    <Compile Include="App.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Fable.Core" Version="4.3.0" />
    <PackageReference Include="Fable.Elmish" Version="4.1.0" />
    <PackageReference Include="Fable.Elmish.React" Version="4.0.0" />
    <PackageReference Include="Fable.Elmish.Debugger" Version="4.0.2" />
    <PackageReference Include="Fable.Elmish.HMR" Version="7.0.0" />
    <PackageReference Include="Feliz" Version="2.9.0" />
    <PackageReference Include="Feliz.Router" Version="4.0.0" />
    <PackageReference Include="Fable.Remoting.Client" Version="7.32.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../Shared/Shared.fsproj" />
  </ItemGroup>
</Project>
```

### Step 5: Initialize npm and Vite

In project root:

**package.json**:
```json
{
  "name": "my-app",
  "version": "1.0.0",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "vite build",
    "preview": "vite preview"
  },
  "devDependencies": {
    "vite": "^5.4.11",
    "vite-plugin-fable": "^0.2.0",
    "tailwindcss": "^4.3.0",
    "daisyui": "^4.12.14",
    "@tailwindcss/vite": "^4.0.0-beta.5"
  }
}
```

```bash
npm install
```

### Step 6: Configure Vite

**vite.config.js**:
```javascript
import { defineConfig } from 'vite';
import fable from 'vite-plugin-fable';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [
    fable({
      fsproj: './src/Client/Client.fsproj',
      babel: {
        plugins: []
      }
    }),
    tailwindcss()
  ],
  root: './src/Client',
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      }
    }
  },
  build: {
    outDir: '../../dist/public',
    emptyOutDir: true
  }
});
```

### Step 7: Configure TailwindCSS

**tailwind.config.js**:
```javascript
/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './src/Client/**/*.{fs,html}'
  ],
  theme: {
    extend: {},
  },
  plugins: [
    require('daisyui')
  ],
  daisyui: {
    themes: ["light", "dark"],
  }
}
```

### Step 8: Create Client Entry Point

**src/Client/index.html**:
```html
<!DOCTYPE html>
<html lang="en" data-theme="light">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>My App</title>
</head>
<body>
    <div id="root"></div>
    <script type="module" src="./App.fs.js"></script>
</body>
</html>
```

**src/Client/App.fs**:
```fsharp
module App

open Elmish
open Elmish.React
open Elmish.HMR // Hot module replacement

// Import Tailwind CSS
Fable.Core.JsInterop.importSideEffects "tailwindcss/tailwind.css"

Program.mkProgram State.init State.update View.view
|> Program.withReactSynchronous "root"
|> Program.withDebugger // Elmish debugger for dev
|> Program.run
```

### Step 9: Create Minimal Shared Types

**src/Shared/Domain.fs**:
```fsharp
module Shared.Domain

type AppInfo = {
    Name: string
    Version: string
}
```

**src/Shared/Api.fs**:
```fsharp
module Shared.Api

open Domain

type IAppApi = {
    getInfo: unit -> Async<AppInfo>
}
```

### Step 10: Create Minimal Client

**src/Client/Types.fs**:
```fsharp
module Types

type RemoteData<'T> =
    | NotAsked
    | Loading
    | Success of 'T
    | Failure of string
```

**src/Client/Api.fs**:
```fsharp
module Api

open Fable.Remoting.Client
open Shared.Api

let api =
    Remoting.createApi()
    |> Remoting.withRouteBuilder (fun typeName methodName -> $"/api/{typeName}/{methodName}")
    |> Remoting.buildProxy<IAppApi>
```

**src/Client/State.fs**:
```fsharp
module State

open Elmish
open Shared.Domain
open Types

type Model = {
    AppInfo: RemoteData<AppInfo>
}

type Msg =
    | LoadAppInfo
    | AppInfoLoaded of AppInfo
    | AppInfoLoadFailed of string

let init () : Model * Cmd<Msg> =
    let model = { AppInfo = NotAsked }
    let cmd = Cmd.ofMsg LoadAppInfo
    model, cmd

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | LoadAppInfo ->
        let cmd = Cmd.OfAsync.perform Api.api.getInfo () AppInfoLoaded
        { model with AppInfo = Loading }, cmd
    
    | AppInfoLoaded info ->
        { model with AppInfo = Success info }, Cmd.none
    
    | AppInfoLoadFailed error ->
        { model with AppInfo = Failure error }, Cmd.none
```

**src/Client/View.fs**:
```fsharp
module View

open Feliz
open State
open Types

let private appInfoView (remoteData: RemoteData<_>) =
    match remoteData with
    | NotAsked -> Html.div "Initializing..."
    | Loading -> Html.div [ prop.className "loading loading-spinner"; prop.text "Loading..." ]
    | Success info -> 
        Html.div [
            prop.className "card bg-base-100 shadow-xl"
            prop.children [
                Html.div [
                    prop.className "card-body"
                    prop.children [
                        Html.h2 [ prop.className "card-title"; prop.text info.Name ]
                        Html.p $"Version: {info.Version}"
                    ]
                ]
            ]
        ]
    | Failure err -> Html.div [ prop.className "alert alert-error"; prop.text $"Error: {err}" ]

let view (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "container mx-auto p-4"
        prop.children [
            Html.h1 [ prop.className "text-4xl font-bold mb-8"; prop.text "My F# App" ]
            appInfoView model.AppInfo
        ]
    ]
```

### Step 11: Create Minimal Server

**src/Server/Persistence.fs**:
```fsharp
module Persistence

open System.IO

let dataDir = "./data"

let ensureDataDir () =
    if not (Directory.Exists dataDir) then
        Directory.CreateDirectory dataDir |> ignore
```

**src/Server/Domain.fs**:
```fsharp
module Domain

open Shared.Domain

let getAppInfo () : Async<AppInfo> =
    async {
        return {
            Name = "My F# App"
            Version = "1.0.0"
        }
    }
```

**src/Server/Api.fs**:
```fsharp
module Api

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Shared.Api

let appApi : IAppApi = {
    getInfo = fun () -> Domain.getAppInfo()
}

let webApp =
    Remoting.createApi()
    |> Remoting.fromValue appApi
    |> Remoting.buildHttpHandler
```

**src/Server/Program.fs**:
```fsharp
module Program

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe

let configureApp (app: IApplicationBuilder) =
    // Initialize persistence
    Persistence.ensureDataDir()
    
    // Serve static files from dist/public
    app.UseStaticFiles() |> ignore
    
    // API routes
    app.UseGiraffe(Api.webApp)

let configureServices (services: IServiceCollection) =
    services.AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    
    configureServices builder.Services
    
    // Serve static files from dist/public
    builder.Services.AddSpaStaticFiles(fun config ->
        config.RootPath <- "dist/public"
    ) |> ignore
    
    let app = builder.Build()
    
    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore
    
    app.UseStaticFiles() |> ignore
    app.UseRouting() |> ignore
    
    configureApp app
    
    app.Run()
    0
```

### Step 12: Add .gitignore

**.gitignore**:
```
bin/
obj/
node_modules/
dist/
data/
.fake/
.ionide/
*.user
.DS_Store
```

### Step 13: Development Commands

Create these helper scripts:

**dev-server.sh**:
```bash
#!/bin/bash
cd src/Server
dotnet watch run
```

**dev-client.sh**:
```bash
#!/bin/bash
npm run dev
```

Make them executable:
```bash
chmod +x dev-server.sh dev-client.sh
```

### Step 14: Verify Setup

1. **Build all projects**:
   ```bash
   dotnet build
   ```

2. **Start backend** (Terminal 1):
   ```bash
   ./dev-server.sh
   ```
   Should start on `http://localhost:5000`

3. **Start frontend** (Terminal 2):
   ```bash
   ./dev-client.sh
   ```
   Should start on `http://localhost:5173`

4. **Open browser**: Navigate to `http://localhost:5173`
   - Should see "My F# App" title
   - Should see a card with app info loaded from backend

## Project Checklist

- [ ] All `.fsproj` files created and referenced correctly
- [ ] `package.json` and Vite config in place
- [ ] TailwindCSS configured with DaisyUI
- [ ] Minimal client renders and makes API call
- [ ] Backend responds to API calls
- [ ] Hot reload works for both frontend and backend
- [ ] Static files served correctly in development

## Next Steps

- Read `02-FRONTEND-GUIDE.md` for frontend development patterns
- Read `03-BACKEND-GUIDE.md` for backend development patterns
- Add features incrementally following MVU pattern

## Troubleshooting

### "Cannot find module" errors
```bash
npm install
dotnet restore
```

### Vite fails to start
- Check port 5173 is not in use
- Verify `vite.config.js` paths are correct

### Backend fails to start
- Check port 5000 is not in use
- Verify `Program.fs` has correct static file paths

### API calls fail (404)
- Verify Vite proxy is configured correctly
- Check API route builder in `Api.fs` matches server setup
- Confirm backend is running on port 5000

### Hot reload not working
- Frontend: Check `Elmish.HMR` is in `App.fs`
- Backend: Verify `dotnet watch run` is used, not `dotnet run`
