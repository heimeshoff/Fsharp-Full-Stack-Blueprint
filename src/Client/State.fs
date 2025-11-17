module State

open Elmish
open Shared.Domain
open Types

/// Application model
type Model = {
    Counter: RemoteData<Counter>
}

/// Application messages
type Msg =
    | LoadCounter
    | CounterLoaded of Result<Counter, string>
    | IncrementCounter
    | CounterIncremented of Result<Counter, string>

/// Initialize the model and load counter
let init () : Model * Cmd<Msg> =
    let model = { Counter = NotAsked }
    let cmd = Cmd.ofMsg LoadCounter
    model, cmd

/// Update function following the MVU pattern
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | LoadCounter ->
        let cmd =
            Cmd.OfAsync.either
                Api.api.getCounter
                ()
                (Ok >> CounterLoaded)
                (fun ex -> Error ex.Message |> CounterLoaded)
        { model with Counter = Loading }, cmd

    | CounterLoaded (Ok counter) ->
        { model with Counter = Success counter }, Cmd.none

    | CounterLoaded (Error err) ->
        { model with Counter = Failure err }, Cmd.none

    | IncrementCounter ->
        let cmd =
            Cmd.OfAsync.either
                Api.api.incrementCounter
                ()
                (Ok >> CounterIncremented)
                (fun ex -> Error ex.Message |> CounterIncremented)
        { model with Counter = Loading }, cmd

    | CounterIncremented (Ok counter) ->
        { model with Counter = Success counter }, Cmd.none

    | CounterIncremented (Error err) ->
        { model with Counter = Failure err }, Cmd.none
