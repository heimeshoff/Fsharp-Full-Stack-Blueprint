module View

open Feliz
open State
open Types

/// Counter display component
let private counterView (remoteData: RemoteData<_>) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "flex flex-col items-center gap-8"
        prop.children [
            // Counter display card
            Html.div [
                prop.className "card bg-base-100 shadow-xl w-96"
                prop.children [
                    Html.div [
                        prop.className "card-body items-center text-center"
                        prop.children [
                            Html.h2 [
                                prop.className "card-title text-2xl"
                                prop.text "Counter App"
                            ]

                            match remoteData with
                            | NotAsked ->
                                Html.div "Initializing..."

                            | Loading ->
                                Html.div [
                                    prop.className "loading loading-spinner loading-lg"
                                ]

                            | Success counter ->
                                Html.div [
                                    prop.className "flex flex-col gap-4"
                                    prop.children [
                                        Html.div [
                                            prop.className "text-6xl font-bold text-primary"
                                            prop.text counter.Value
                                        ]

                                        Html.button [
                                            prop.className "btn btn-primary btn-lg"
                                            prop.onClick (fun _ -> dispatch IncrementCounter)
                                            prop.children [
                                                Html.span "Increment"
                                            ]
                                        ]
                                    ]
                                ]

                            | Failure err ->
                                Html.div [
                                    prop.className "alert alert-error"
                                    prop.children [
                                        Html.span $"Error: {err}"
                                    ]
                                ]
                        ]
                    ]
                ]
            ]

            // Info card
            Html.div [
                prop.className "text-center text-gray-600"
                prop.children [
                    Html.p "Click the button to increment the counter."
                    Html.p [
                        prop.className "text-sm"
                        prop.text "The value is persisted on the backend."
                    ]
                ]
            ]
        ]
    ]

/// Main view
let view (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "min-h-screen bg-base-200 flex flex-col"
        prop.children [
            // Header
            Html.div [
                prop.className "navbar bg-base-100 shadow-lg"
                prop.children [
                    Html.div [
                        prop.className "flex-1"
                        prop.children [
                            Html.h1 [
                                prop.className "text-2xl font-bold px-4"
                                prop.text "F# Counter Demo"
                            ]
                        ]
                    ]
                ]
            ]

            // Main content
            Html.div [
                prop.className "flex-1 container mx-auto p-8 flex items-center justify-center"
                prop.children [
                    counterView model.Counter dispatch
                ]
            ]
        ]
    ]
