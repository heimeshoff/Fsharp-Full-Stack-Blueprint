module Program

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.FileProviders
open Giraffe
open System.IO

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    // Add Giraffe
    builder.Services.AddGiraffe() |> ignore

    // Configure CORS for development
    builder.Services.AddCors(fun options ->
        options.AddPolicy("AllowAll", fun policy ->
            policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
            |> ignore
        )
    ) |> ignore

    let app = builder.Build()

    // Initialize data directory
    Persistence.ensureDataDir()

    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    app.UseCors("AllowAll") |> ignore
    app.UseRouting() |> ignore

    // Serve static files from dist/public
    let publicPath = Path.Combine(Directory.GetCurrentDirectory(), "dist", "public")
    if Directory.Exists(publicPath) then
        app.UseStaticFiles(StaticFileOptions(
            FileProvider = new PhysicalFileProvider(publicPath)
        )) |> ignore

    // API routes
    app.UseGiraffe(Api.webApp)

    printfn "🚀 Server starting on http://localhost:5000"
    printfn "📊 Counter API ready at /api/ICounterApi/*"

    app.Run("http://0.0.0.0:5000")
    0
