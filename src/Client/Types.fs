module Types

/// Represents the state of a remote data fetch
type RemoteData<'T> =
    | NotAsked
    | Loading
    | Success of 'T
    | Failure of string
