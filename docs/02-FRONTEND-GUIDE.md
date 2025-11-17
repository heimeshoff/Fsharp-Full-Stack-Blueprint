# Frontend Development Guide

## Elmish MVU Pattern

The Model-View-Update (MVU) architecture ensures predictable state management through unidirectional data flow.

### Core Concepts

```
┌──────────────────────────────────────┐
│                                      │
│  User Interaction                    │
│         │                            │
│         ▼                            │
│    ┌────────┐      ┌──────────┐    │
│    │  Msg   │─────▶│  Update  │    │
│    └────────┘      └──────────┘    │
│         ▲               │           │
│         │               ▼           │
│    ┌────────┐      ┌──────────┐    │
│    │  View  │◀─────│  Model   │    │
│    └────────┘      └──────────┘    │
│                                      │
└──────────────────────────────────────┘
```

## State.fs Structure

### Model Definition

```fsharp
module State

open Elmish
open Types

// Always represent async data as RemoteData
type Model = {
    // Page state
    CurrentPage: Page
    
    // Data from API
    Items: RemoteData<Item list>
    SelectedItem: RemoteData<Item>
    
    // Form state (local)
    FormInput: string
    FormErrors: Map<string, string>
    
    // UI state
    IsModalOpen: bool
    Toast: Toast option
}
```

**Key Principles**:
- Keep model flat and simple
- No derived data (compute in view)
- Use `RemoteData` for all async operations
- Separate concerns: page state, data, forms, UI

### Message Types

```fsharp
type Msg =
    // Navigation
    | NavigateTo of Page
    
    // API Calls - Request/Response pattern
    | LoadItems
    | ItemsLoaded of Result<Item list, string>
    
    | LoadItem of id: int
    | ItemLoaded of Result<Item, string>
    
    | SaveItem of Item
    | ItemSaved of Result<Item, string>
    
    | DeleteItem of id: int
    | ItemDeleted of Result<unit, string>
    
    // User Input
    | FormInputChanged of string
    | FormSubmitted
    
    // UI Events
    | OpenModal
    | CloseModal
    | ShowToast of Toast
    | DismissToast
```

**Naming Conventions**:
- Use present tense for user actions: `LoadItems`, `SaveItem`
- Use past tense for async results: `ItemsLoaded`, `ItemSaved`
- Include data type in Result: `Result<Data, string>`

### Init Function

```fsharp
let init () : Model * Cmd<Msg> =
    let initialModel = {
        CurrentPage = HomePage
        Items = NotAsked
        SelectedItem = NotAsked
        FormInput = ""
        FormErrors = Map.empty
        IsModalOpen = false
        Toast = None
    }
    
    // Commands to run on startup
    let initialCmd = Cmd.batch [
        Cmd.ofMsg LoadItems  // Load data immediately
    ]
    
    initialModel, initialCmd
```

### Update Function

```fsharp
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    // Navigation
    | NavigateTo page ->
        { model with CurrentPage = page }, Cmd.none
    
    // Loading items
    | LoadItems ->
        let cmd = 
            Cmd.OfAsync.either
                Api.api.getItems
                ()
                (Ok >> ItemsLoaded)
                (fun ex -> Error ex.Message |> ItemsLoaded)
        
        { model with Items = Loading }, cmd
    
    | ItemsLoaded (Ok items) ->
        { model with Items = Success items }, Cmd.none
    
    | ItemsLoaded (Error err) ->
        let cmd = Cmd.ofMsg (ShowToast { Message = err; Type = ToastError })
        { model with Items = Failure err }, cmd
    
    // Loading single item
    | LoadItem id ->
        let cmd =
            Cmd.OfAsync.either
                Api.api.getItem
                id
                (Ok >> ItemLoaded)
                (fun ex -> Error ex.Message |> ItemLoaded)
        
        { model with SelectedItem = Loading }, cmd
    
    | ItemLoaded result ->
        match result with
        | Ok item -> 
            { model with SelectedItem = Success item }, Cmd.none
        | Error err ->
            { model with SelectedItem = Failure err }, Cmd.none
    
    // Saving item
    | SaveItem item ->
        let cmd =
            Cmd.OfAsync.either
                Api.api.saveItem
                item
                (Ok >> ItemSaved)
                (fun ex -> Error ex.Message |> ItemSaved)
        
        model, cmd
    
    | ItemSaved (Ok savedItem) ->
        let updatedItems =
            match model.Items with
            | Success items -> 
                Success (items |> List.map (fun i -> 
                    if i.Id = savedItem.Id then savedItem else i
                ))
            | other -> other
        
        let cmd = Cmd.batch [
            Cmd.ofMsg CloseModal
            Cmd.ofMsg (ShowToast { Message = "Saved!"; Type = ToastSuccess })
        ]
        
        { model with Items = updatedItems; SelectedItem = Success savedItem }, cmd
    
    | ItemSaved (Error err) ->
        let cmd = Cmd.ofMsg (ShowToast { Message = err; Type = ToastError })
        model, cmd
    
    // Form handling
    | FormInputChanged value ->
        { model with FormInput = value }, Cmd.none
    
    | FormSubmitted ->
        // Validate
        if System.String.IsNullOrWhiteSpace(model.FormInput) then
            let errors = Map.empty |> Map.add "input" "Cannot be empty"
            { model with FormErrors = errors }, Cmd.none
        else
            // Create item and save
            let newItem = { Id = 0; Name = model.FormInput }
            let cmd = Cmd.ofMsg (SaveItem newItem)
            { model with FormErrors = Map.empty }, cmd
    
    // UI State
    | OpenModal ->
        { model with IsModalOpen = true }, Cmd.none
    
    | CloseModal ->
        { model with IsModalOpen = false; FormInput = ""; FormErrors = Map.empty }, Cmd.none
    
    | ShowToast toast ->
        { model with Toast = Some toast }, Cmd.none
    
    | DismissToast ->
        { model with Toast = None }, Cmd.none
```

**Update Patterns**:
1. **Immediate state changes**: Return new model + `Cmd.none`
2. **Async operations**: Set Loading state + return Cmd
3. **Success handling**: Update model + optional success commands (toasts, navigation)
4. **Error handling**: Set Failure state + show toast
5. **Batch commands**: Use `Cmd.batch` for multiple effects

## View.fs Structure

### Component Organization

```fsharp
module View

open Feliz
open Feliz.Router
open State
open Types

// ============================================
// Reusable Components
// ============================================

module Components =
    
    let private spinner =
        Html.div [
            prop.className "loading loading-spinner loading-lg"
        ]
    
    let remoteDataView (remoteData: RemoteData<'T>) (successView: 'T -> ReactElement) =
        match remoteData with
        | NotAsked -> Html.div "Not loaded yet"
        | Loading -> spinner
        | Success data -> successView data
        | Failure err -> 
            Html.div [
                prop.className "alert alert-error"
                prop.children [
                    Html.span err
                ]
            ]
    
    let button (text: string) (onClick: unit -> unit) (extraClasses: string) =
        Html.button [
            prop.className $"btn {extraClasses}"
            prop.onClick (fun _ -> onClick())
            prop.text text
        ]
    
    let primaryButton text onClick =
        button text onClick "btn-primary"
    
    let secondaryButton text onClick =
        button text onClick "btn-secondary"
    
    let card (title: string) (content: ReactElement list) =
        Html.div [
            prop.className "card bg-base-100 shadow-xl"
            prop.children [
                Html.div [
                    prop.className "card-body"
                    prop.children [
                        Html.h2 [ prop.className "card-title"; prop.text title ]
                        yield! content
                    ]
                ]
            ]
        ]
    
    let modal (isOpen: bool) (onClose: unit -> unit) (content: ReactElement list) =
        Html.div [
            prop.className "modal"
            prop.classes [ "modal-open", isOpen ]
            prop.children [
                Html.div [
                    prop.className "modal-box"
                    prop.children content
                ]
                Html.div [
                    prop.className "modal-backdrop"
                    prop.onClick (fun _ -> onClose())
                ]
            ]
        ]

// ============================================
// Page Views
// ============================================

module Pages =
    
    let homePage (model: Model) (dispatch: Msg -> unit) =
        Html.div [
            prop.className "space-y-4"
            prop.children [
                Html.h1 [ prop.className "text-4xl font-bold"; prop.text "Home" ]
                
                Components.primaryButton "Load Items" (fun () -> dispatch LoadItems)
                
                Components.remoteDataView model.Items (fun items ->
                    Html.div [
                        prop.className "grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4"
                        prop.children [
                            for item in items do
                                Components.card item.Name [
                                    Html.p $"ID: {item.Id}"
                                    Components.secondaryButton "View" (fun () -> 
                                        dispatch (LoadItem item.Id)
                                        dispatch (NavigateTo (DetailPage item.Id))
                                    )
                                ]
                        ]
                    ]
                )
            ]
        ]
    
    let detailPage (itemId: int) (model: Model) (dispatch: Msg -> unit) =
        Html.div [
            prop.className "space-y-4"
            prop.children [
                Html.button [
                    prop.className "btn btn-ghost"
                    prop.onClick (fun _ -> dispatch (NavigateTo HomePage))
                    prop.text "← Back"
                ]
                
                Components.remoteDataView model.SelectedItem (fun item ->
                    Components.card item.Name [
                        Html.p $"ID: {item.Id}"
                        Html.div [
                            prop.className "card-actions justify-end"
                            prop.children [
                                Components.primaryButton "Edit" (fun () -> dispatch OpenModal)
                                Html.button [
                                    prop.className "btn btn-error"
                                    prop.onClick (fun _ -> dispatch (DeleteItem item.Id))
                                    prop.text "Delete"
                                ]
                            ]
                        ]
                    ]
                )
            ]
        ]
    
    let notFoundPage =
        Html.div [
            prop.className "hero min-h-screen bg-base-200"
            prop.children [
                Html.div [
                    prop.className "hero-content text-center"
                    prop.children [
                        Html.div [
                            Html.h1 [ prop.className "text-5xl font-bold"; prop.text "404" ]
                            Html.p [ prop.className "py-6"; prop.text "Page not found" ]
                        ]
                    ]
                ]
            ]
        ]

// ============================================
// Toast Notification
// ============================================

let private toastView (toast: Toast option) (dispatch: Msg -> unit) =
    match toast with
    | None -> Html.none
    | Some toast ->
        let alertClass = 
            match toast.Type with
            | ToastSuccess -> "alert-success"
            | ToastError -> "alert-error"
            | ToastInfo -> "alert-info"
        
        Html.div [
            prop.className "toast toast-top toast-end"
            prop.children [
                Html.div [
                    prop.className $"alert {alertClass}"
                    prop.children [
                        Html.span toast.Message
                        Html.button [
                            prop.className "btn btn-sm btn-ghost"
                            prop.onClick (fun _ -> dispatch DismissToast)
                            prop.text "✕"
                        ]
                    ]
                ]
            ]
        ]

// ============================================
// Edit Modal
// ============================================

let private editModal (model: Model) (dispatch: Msg -> unit) =
    Components.modal model.IsModalOpen (fun () -> dispatch CloseModal) [
        Html.h3 [ prop.className "font-bold text-lg"; prop.text "Edit Item" ]
        
        Html.div [
            prop.className "form-control"
            prop.children [
                Html.label [ prop.className "label"; prop.children [ Html.span "Name" ] ]
                Html.input [
                    prop.className "input input-bordered"
                    prop.value model.FormInput
                    prop.onChange (FormInputChanged >> dispatch)
                ]
                
                match model.FormErrors.TryFind "input" with
                | Some err ->
                    Html.label [
                        prop.className "label"
                        prop.children [
                            Html.span [ prop.className "label-text-alt text-error"; prop.text err ]
                        ]
                    ]
                | None -> Html.none
            ]
        ]
        
        Html.div [
            prop.className "modal-action"
            prop.children [
                Components.secondaryButton "Cancel" (fun () -> dispatch CloseModal)
                Components.primaryButton "Save" (fun () -> dispatch FormSubmitted)
            ]
        ]
    ]

// ============================================
// Main View
// ============================================

let view (model: Model) (dispatch: Msg -> unit) =
    let pageView =
        match model.CurrentPage with
        | HomePage -> Pages.homePage model dispatch
        | DetailPage itemId -> Pages.detailPage itemId model dispatch
        | NotFound -> Pages.notFoundPage
    
    Html.div [
        prop.className "min-h-screen bg-base-100"
        prop.children [
            // Navigation bar
            Html.div [
                prop.className "navbar bg-base-300"
                prop.children [
                    Html.a [
                        prop.className "btn btn-ghost text-xl"
                        prop.onClick (fun _ -> dispatch (NavigateTo HomePage))
                        prop.text "My App"
                    ]
                ]
            ]
            
            // Main content
            Html.div [
                prop.className "container mx-auto p-4"
                prop.children [ pageView ]
            ]
            
            // Modals
            editModal model dispatch
            
            // Toast notifications
            toastView model.Toast dispatch
        ]
    ]
```

## Routing with Feliz.Router

### Types.fs - Page Definition

```fsharp
type Page =
    | HomePage
    | DetailPage of itemId: int
    | NotFound
```

### State.fs - URL Parsing

```fsharp
open Feliz.Router

let parseUrl (segments: string list) : Page =
    match segments with
    | [] -> HomePage
    | [ "item"; Route.Int itemId ] -> DetailPage itemId
    | _ -> NotFound

// Add to Model
type Model = {
    // ... other fields
    CurrentPage: Page
}

// Add to Msg
type Msg =
    | UrlChanged of string list
    | NavigateTo of Page
    // ... other messages

// Update url parsing
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | UrlChanged segments ->
        let page = parseUrl segments
        { model with CurrentPage = page }, Cmd.none
    
    | NavigateTo page ->
        let segments =
            match page with
            | HomePage -> []
            | DetailPage itemId -> [ "item"; string itemId ]
            | NotFound -> [ "not-found" ]
        
        model, Cmd.navigatePath segments
    
    // ... other cases

// Update init to handle initial URL
let init () : Model * Cmd<Msg> =
    let initialModel = { (* ... *) CurrentPage = HomePage }
    let initialCmd = Cmd.batch [
        Cmd.ofMsg (UrlChanged (Router.currentPath()))
        // ... other commands
    ]
    initialModel, initialCmd
```

### App.fs - Router Setup

```fsharp
module App

open Elmish
open Elmish.React
open Elmish.HMR
open Feliz.Router

Program.mkProgram State.init State.update View.view
|> Program.toNavigable (Router.currentPath >> State.UrlChanged) State.update
|> Program.withReactSynchronous "root"
|> Program.withDebugger
|> Program.run
```

## Best Practices

### 1. RemoteData Pattern

Always use for async operations:

```fsharp
type RemoteData<'T> =
    | NotAsked      // Initial state
    | Loading       // Request in flight
    | Success of 'T // Data received
    | Failure of string // Error message
```

### 2. Cmd Patterns

```fsharp
// Simple command
Cmd.ofMsg message

// Async with success/error
Cmd.OfAsync.either
    apiCall
    parameter
    (Ok >> MessageConstructor)
    (fun ex -> Error ex.Message |> MessageConstructor)

// Batch multiple commands
Cmd.batch [
    Cmd.ofMsg Message1
    Cmd.ofMsg Message2
]

// Debounced input
Cmd.OfAsync.perform
    (fun () -> async {
        do! Async.Sleep 300
        return value
    })
    ()
    DebouncedValueReady
```

### 3. Form Validation

```fsharp
type FormErrors = Map<string, string>

let validateForm (input: FormData) : Result<ValidatedData, FormErrors> =
    let errors = Map.empty
    
    let errors =
        if String.IsNullOrWhiteSpace input.Name then
            errors |> Map.add "name" "Name is required"
        else
            errors
    
    let errors =
        if input.Age < 18 then
            errors |> Map.add "age" "Must be 18 or older"
        else
            errors
    
    if errors.IsEmpty then
        Ok { Name = input.Name; Age = input.Age }
    else
        Error errors
```

### 4. Conditional Rendering

```fsharp
// Using match
match model.IsLoggedIn with
| true -> Html.div "Welcome!"
| false -> Html.div "Please log in"

// Using if/then
if model.Count > 10 then
    Html.div "High"
else
    Html.div "Low"

// Using Html.none for nothing
if model.ShowWarning then
    Html.div [ prop.className "alert"; prop.text "Warning!" ]
else
    Html.none

// Conditional classes
Html.div [
    prop.className "btn"
    prop.classes [
        "btn-primary", model.IsPrimary
        "btn-disabled", not model.IsEnabled
    ]
]
```

### 5. List Rendering

```fsharp
Html.ul [
    for item in items do
        Html.li [
            prop.key item.Id  // Important for React reconciliation!
            prop.text item.Name
        ]
]
```

### 6. Event Handling

```fsharp
// Button click
Html.button [
    prop.onClick (fun _ -> dispatch ButtonClicked)
]

// Input change
Html.input [
    prop.onChange (fun (value: string) -> dispatch (InputChanged value))
]

// Form submit
Html.form [
    prop.onSubmit (fun e ->
        e.preventDefault()
        dispatch FormSubmitted
    )
]

// Keyboard events
Html.input [
    prop.onKeyDown (fun e ->
        if e.key = "Enter" then
            dispatch EnterPressed
    )
]
```

## Performance Tips

### 1. Memoize Expensive Views

```fsharp
open Feliz

// Use React.memo for components that rarely change
let expensiveComponent = React.memo(fun (props: {| data: Data |}) ->
    // Expensive rendering logic
    Html.div "Complex component"
)
```

### 2. Keep Model Lean

```fsharp
// ❌ Bad: Storing derived data
type Model = {
    Items: Item list
    FilteredItems: Item list  // Derived!
    ItemCount: int             // Derived!
}

// ✅ Good: Compute in view
type Model = {
    Items: Item list
    Filter: string
}

// In view:
let filteredItems = 
    model.Items 
    |> List.filter (fun item -> item.Name.Contains(model.Filter))
```

### 3. Debounce User Input

```fsharp
type Msg =
    | InputChanged of string
    | DebouncedSearch of string

let update msg model =
    match msg with
    | InputChanged value ->
        let debounceCmd =
            Cmd.OfAsync.perform
                (fun () -> async {
                    do! Async.Sleep 300
                    return value
                })
                ()
                DebouncedSearch
        
        { model with SearchInput = value }, debounceCmd
    
    | DebouncedSearch value ->
        let cmd = Cmd.ofMsg (PerformSearch value)
        model, cmd
```

## Common Patterns

### Loading States

```fsharp
let view (model: Model) dispatch =
    match model.Data with
    | NotAsked -> 
        Html.button [
            prop.onClick (fun _ -> dispatch LoadData)
            prop.text "Load Data"
        ]
    
    | Loading ->
        Html.div [ prop.className "loading loading-spinner" ]
    
    | Success data ->
        Html.div [
            for item in data -> Html.div item.Name
        ]
    
    | Failure error ->
        Html.div [
            prop.className "alert alert-error"
            prop.text error
            Html.button [
                prop.onClick (fun _ -> dispatch LoadData)
                prop.text "Retry"
            ]
        ]
```

### Optimistic Updates

```fsharp
| SaveItem item ->
    // Optimistically update UI
    let updatedItems =
        model.Items
        |> List.map (fun i -> if i.Id = item.Id then item else i)
    
    let cmd =
        Cmd.OfAsync.either
            Api.api.saveItem
            item
            (Ok >> ItemSaved)
            (fun ex -> Error ex.Message |> ItemSaved)
    
    { model with Items = updatedItems }, cmd

| ItemSaved (Error err) ->
    // Revert on error
    let cmd = Cmd.batch [
        Cmd.ofMsg LoadItems  // Reload from server
        Cmd.ofMsg (ShowToast { Message = err; Type = ToastError })
    ]
    model, cmd
```

## Next Steps

- Read `03-BACKEND-GUIDE.md` for backend patterns
- Read `04-SHARED-TYPES.md` for type sharing
- Refer to [Feliz documentation](https://zaid-ajaj.github.io/Feliz/) for more examples
