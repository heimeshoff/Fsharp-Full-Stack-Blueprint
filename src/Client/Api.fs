module Api

open Fable.Remoting.Client
open Shared.Api

/// Client proxy for the counter API
let api =
    Remoting.createApi()
    |> Remoting.withRouteBuilder (fun typeName methodName -> $"/api/{typeName}/{methodName}")
    |> Remoting.buildProxy<ICounterApi>
