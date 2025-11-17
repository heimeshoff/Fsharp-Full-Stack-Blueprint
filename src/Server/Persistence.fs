module Persistence

open System.IO
open System.Text.Json
open Shared.Domain

let private dataDir = "./data"
let private counterFile = Path.Combine(dataDir, "counter.json")

/// Ensure the data directory exists
let ensureDataDir () =
    if not (Directory.Exists dataDir) then
        Directory.CreateDirectory dataDir |> ignore

/// Load counter from file, or initialize to 0 if file doesn't exist
let loadCounter () : Async<Counter> =
    async {
        ensureDataDir()

        if File.Exists counterFile then
            let! json = File.ReadAllTextAsync(counterFile) |> Async.AwaitTask
            let counter = JsonSerializer.Deserialize<Counter>(json)
            return counter
        else
            return { Value = 0 }
    }

/// Save counter to file
let saveCounter (counter: Counter) : Async<unit> =
    async {
        ensureDataDir()
        let json = JsonSerializer.Serialize(counter)
        do! File.WriteAllTextAsync(counterFile, json) |> Async.AwaitTask
    }
