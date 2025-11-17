module Api

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Shared.Api
open Shared.Domain

let counterApi : ICounterApi = {
    getCounter = fun () -> async {
        return! Persistence.loadCounter()
    }

    incrementCounter = fun () -> async {
        let! counter = Persistence.loadCounter()
        let newCounter = { Value = counter.Value + 1 }
        do! Persistence.saveCounter newCounter
        return newCounter
    }
}

let webApp =
    Remoting.createApi()
    |> Remoting.withRouteBuilder (fun typeName methodName -> $"/api/{typeName}/{methodName}")
    |> Remoting.fromValue counterApi
    |> Remoting.buildHttpHandler
