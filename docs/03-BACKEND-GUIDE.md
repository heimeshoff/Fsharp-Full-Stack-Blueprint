# Backend Development Guide

## Giraffe Architecture

Giraffe uses a functional composition model with `HttpHandler` as the core abstraction.

### HttpHandler Signature

```fsharp
type HttpHandler = HttpContext -> Task<HttpContext option>
```

- Returns `Some context` to continue pipeline
- Returns `None` to short-circuit (useful for auth, validation)

### Composition Operator

```fsharp
// Fish operator: compose handlers sequentially
let (>=>) = compose

// Example
let handler = validateRequest >=> processData >=> returnJson
```

## Project Structure (Server)

```
src/Server/
├── Program.fs          # Entry point, app configuration
├── Api.fs              # Fable.Remoting API implementation
├── Domain.fs           # Business logic (pure functions)
├── Persistence.fs      # Database and file I/O
├── Validation.fs       # Input validation
└── Types.fs            # Server-only types (optional)
```

## Program.fs - Application Setup

```fsharp
module Program

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Giraffe

let configureApp (app: IApplicationBuilder) =
    // Initialize on startup
    Persistence.ensureDataDir()
    Persistence.initializeDatabase()
    
    // Middleware order matters!
    app.UseStaticFiles() |> ignore      // Serve static files first
    app.UseRouting() |> ignore
    app.UseGiraffe(Api.webApp)          // Fable.Remoting handlers

let configureServices (services: IServiceCollection) =
    services.AddGiraffe() |> ignore
    
    // Optional: Add logging
    services.AddLogging(fun logging ->
        logging.AddConsole() |> ignore
        logging.SetMinimumLevel(LogLevel.Information) |> ignore
    ) |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    
    // Configure Kestrel to listen on specific port
    builder.WebHost.UseUrls("http://0.0.0.0:5000") |> ignore
    
    configureServices builder.Services
    
    let app = builder.Build()
    
    // Development vs Production
    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore
    
    // Serve static files from wwwroot or dist/public
    app.UseStaticFiles() |> ignore
    
    configureApp app
    
    app.Run()
    0
```

## Api.fs - Fable.Remoting Implementation

```fsharp
module Api

open System
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Shared.Api
open Shared.Domain

// ============================================
// API Implementation
// ============================================

let itemApi : IItemApi = {
    // Simple query
    getItems = fun () -> async {
        return! Persistence.getAllItems()
    }
    
    // Query with parameter
    getItem = fun itemId -> async {
        match! Persistence.getItemById itemId with
        | Some item -> return Ok item
        | None -> return Error $"Item {itemId} not found"
    }
    
    // Create/Update
    saveItem = fun item -> async {
        try
            // Validate
            let validation = Validation.validateItem item
            match validation with
            | Error errors -> return Error (String.concat ", " errors)
            | Ok validItem ->
                // Business logic
                let processedItem = Domain.processItem validItem
                
                // Persist
                do! Persistence.saveItem processedItem
                
                return Ok processedItem
        with ex ->
            return Error ex.Message
    }
    
    // Delete
    deleteItem = fun itemId -> async {
        try
            do! Persistence.deleteItem itemId
            return Ok ()
        with ex ->
            return Error ex.Message
    }
    
    // Complex operation
    searchItems = fun query -> async {
        let! allItems = Persistence.getAllItems()
        
        let filtered =
            allItems
            |> List.filter (fun item ->
                item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            )
        
        return filtered
    }
}

// ============================================
// Remoting Configuration
// ============================================

let webApp =
    Remoting.createApi()
    |> Remoting.withRouteBuilder (fun typeName methodName -> 
        $"/api/{typeName}/{methodName}"
    )
    |> Remoting.fromValue itemApi
    |> Remoting.withErrorHandler (fun ex routeInfo ->
        // Log errors
        printfn $"Error in {routeInfo.methodName}: {ex.Message}"
        
        // Return friendly error message
        Propagate ex.Message
    )
    |> Remoting.buildHttpHandler

// ============================================
// Multiple APIs (if needed)
// ============================================

let userApi : IUserApi = {
    getCurrentUser = fun () -> async {
        // Implementation
        return { Id = 1; Name = "User" }
    }
}

let combinedWebApp =
    // Combine multiple Fable.Remoting APIs
    choose [
        Remoting.createApi()
        |> Remoting.fromValue itemApi
        |> Remoting.buildHttpHandler
        
        Remoting.createApi()
        |> Remoting.fromValue userApi
        |> Remoting.buildHttpHandler
    ]
```

## Domain.fs - Business Logic

Keep business logic pure (no I/O):

```fsharp
module Domain

open System
open Shared.Domain

// ============================================
// Pure Business Logic
// ============================================

let processItem (item: Item) : Item =
    // Example: Normalize name
    { item with Name = item.Name.Trim() }

let calculateItemScore (item: Item) : int =
    // Example business rule
    item.Name.Length * 10

let isItemValid (item: Item) : bool =
    not (String.IsNullOrWhiteSpace item.Name) &&
    item.Name.Length >= 3

// ============================================
// Business Operations (can be async if needed)
// ============================================

let processItemList (items: Item list) : Item list =
    items
    |> List.filter isItemValid
    |> List.map processItem
    |> List.sortBy (fun item -> item.Name)

let aggregateStats (items: Item list) : ItemStats =
    {
        TotalCount = items.Length
        AverageScore = 
            if items.Length > 0 then
                items |> List.averageBy (calculateItemScore >> float)
            else
                0.0
    }

// ============================================
// Domain Events (for event sourcing)
// ============================================

type ItemEvent =
    | ItemCreated of Item
    | ItemUpdated of Item
    | ItemDeleted of itemId: int
    | ItemsImported of Item list

let applyEvent (state: Item list) (event: ItemEvent) : Item list =
    match event with
    | ItemCreated item ->
        item :: state
    
    | ItemUpdated item ->
        state |> List.map (fun i ->
            if i.Id = item.Id then item else i
        )
    
    | ItemDeleted itemId ->
        state |> List.filter (fun i -> i.Id <> itemId)
    
    | ItemsImported items ->
        items @ state

let replayEvents (events: ItemEvent list) : Item list =
    events |> List.fold applyEvent []
```

## Validation.fs - Input Validation

```fsharp
module Validation

open System
open Shared.Domain

type ValidationError = string
type ValidationResult<'T> = Result<'T, ValidationError list>

// ============================================
// Field Validators
// ============================================

let validateRequired (fieldName: string) (value: string) : ValidationError option =
    if String.IsNullOrWhiteSpace value then
        Some $"{fieldName} is required"
    else
        None

let validateLength (fieldName: string) (min: int) (max: int) (value: string) : ValidationError option =
    let len = value.Length
    if len < min || len > max then
        Some $"{fieldName} must be between {min} and {max} characters"
    else
        None

let validateRange (fieldName: string) (min: int) (max: int) (value: int) : ValidationError option =
    if value < min || value > max then
        Some $"{fieldName} must be between {min} and {max}"
    else
        None

let validateEmail (email: string) : ValidationError option =
    if email.Contains("@") && email.Contains(".") then
        None
    else
        Some "Invalid email format"

// ============================================
// Entity Validators
// ============================================

let validateItem (item: Item) : ValidationResult<Item> =
    let errors =
        [
            validateRequired "Name" item.Name
            validateLength "Name" 3 100 item.Name
        ]
        |> List.choose id
    
    if errors.IsEmpty then
        Ok item
    else
        Error errors

let validateItemUpdate (existingItem: Item) (updates: ItemUpdate) : ValidationResult<Item> =
    // Validate that update is allowed
    let errors = []
    
    let errors =
        match updates.Name with
        | Some name ->
            [
                validateRequired "Name" name
                validateLength "Name" 3 100 name
            ]
            |> List.choose id
            |> List.append errors
        | None -> errors
    
    if errors.IsEmpty then
        let updated = 
            { existingItem with 
                Name = updates.Name |> Option.defaultValue existingItem.Name
            }
        Ok updated
    else
        Error errors

// ============================================
// Validation Combinators
// ============================================

let (>>=) result f =
    match result with
    | Ok value -> f value
    | Error errors -> Error errors

let validateAll (validators: ('T -> ValidationError option) list) (value: 'T) : ValidationResult<'T> =
    let errors =
        validators
        |> List.choose (fun validator -> validator value)
    
    if errors.IsEmpty then
        Ok value
    else
        Error errors
```

## Error Handling Patterns

### 1. Result Type for Operations

```fsharp
// In Domain.fs
let processOrder (order: Order) : Result<ProcessedOrder, string> =
    if order.Items.IsEmpty then
        Error "Order must contain at least one item"
    elif order.Total < 0m then
        Error "Order total cannot be negative"
    else
        Ok { OrderId = order.Id; ProcessedAt = DateTime.UtcNow }

// In Api.fs
let orderApi = {
    submitOrder = fun order -> async {
        match Domain.processOrder order with
        | Ok processed ->
            do! Persistence.saveOrder processed
            return Ok processed
        | Error msg ->
            return Error msg
    }
}
```

### 2. Exception Handling at Boundaries

```fsharp
let safeExecute (operation: unit -> Async<'T>) : Async<Result<'T, string>> =
    async {
        try
            let! result = operation()
            return Ok result
        with
        | :? System.IO.IOException as ex ->
            return Error $"File operation failed: {ex.Message}"
        | ex ->
            return Error $"Unexpected error: {ex.Message}"
    }

// Usage in API
let getItemApi = {
    getItem = fun itemId -> async {
        let! result = safeExecute (fun () -> Persistence.getItemById itemId)
        match result with
        | Ok (Some item) -> return Ok item
        | Ok None -> return Error $"Item {itemId} not found"
        | Error msg -> return Error msg
    }
}
```

### 3. Custom Error Types

```fsharp
type AppError =
    | NotFound of entityType: string * id: int
    | ValidationError of errors: string list
    | DatabaseError of message: string
    | UnauthorizedAccess of resource: string

let errorToString (error: AppError) : string =
    match error with
    | NotFound (entityType, id) -> $"{entityType} with id {id} not found"
    | ValidationError errors -> String.concat ", " errors
    | DatabaseError msg -> $"Database error: {msg}"
    | UnauthorizedAccess resource -> $"Unauthorized access to {resource}"

// Usage
type ApiResult<'T> = Result<'T, AppError>

let getItemResult (itemId: int) : Async<ApiResult<Item>> =
    async {
        try
            match! Persistence.getItemById itemId with
            | Some item -> return Ok item
            | None -> return Error (NotFound ("Item", itemId))
        with ex ->
            return Error (DatabaseError ex.Message)
    }
```

## Async Patterns

### 1. Sequential Async Operations

```fsharp
let processItemWorkflow (item: Item) : Async<Result<Item, string>> =
    async {
        // Step 1: Validate
        match Validation.validateItem item with
        | Error errors -> return Error (String.concat ", " errors)
        | Ok validItem ->
            
            // Step 2: Process
            let processedItem = Domain.processItem validItem
            
            // Step 3: Save
            try
                do! Persistence.saveItem processedItem
                
                // Step 4: Log event
                do! Persistence.logEvent (ItemCreated processedItem)
                
                return Ok processedItem
            with ex ->
                return Error ex.Message
    }
```

### 2. Parallel Async Operations

```fsharp
let loadDashboardData () : Async<DashboardData> =
    async {
        let! items, users, stats =
            Async.Parallel [
                Persistence.getAllItems()
                Persistence.getAllUsers()
                Persistence.getStats()
            ]
            |> Async.map (fun results ->
                results.[0] :?> Item list,
                results.[1] :?> User list,
                results.[2] :?> Stats
            )
        
        return {
            Items = items
            Users = users
            Stats = stats
        }
    }

// Or with explicit types
let loadDashboardDataTyped () : Async<DashboardData> =
    async {
        let! itemsTask = Persistence.getAllItems() |> Async.StartChild
        let! usersTask = Persistence.getAllUsers() |> Async.StartChild
        let! statsTask = Persistence.getStats() |> Async.StartChild
        
        let! items = itemsTask
        let! users = usersTask
        let! stats = statsTask
        
        return { Items = items; Users = users; Stats = stats }
    }
```

### 3. Async with Timeout

```fsharp
let withTimeout (timeout: int) (operation: Async<'T>) : Async<Result<'T, string>> =
    async {
        let! result = 
            Async.Choice [
                async {
                    let! value = operation
                    return Choice1Of2 value
                }
                async {
                    do! Async.Sleep timeout
                    return Choice2Of2 "Operation timed out"
                }
            ]
        
        match result with
        | Choice1Of2 value -> return Ok value
        | Choice2Of2 error -> return Error error
    }
```

## Caching Patterns

```fsharp
module Cache =
    open System.Collections.Concurrent
    
    let private cache = ConcurrentDictionary<string, obj * DateTime>()
    let private cacheDuration = TimeSpan.FromMinutes(5.0)
    
    let get<'T> (key: string) : 'T option =
        match cache.TryGetValue(key) with
        | true, (value, expiry) when DateTime.UtcNow < expiry ->
            Some (value :?> 'T)
        | _ ->
            None
    
    let set<'T> (key: string) (value: 'T) : unit =
        let expiry = DateTime.UtcNow.Add(cacheDuration)
        cache.[key] <- (box value, expiry)
    
    let getOrAdd<'T> (key: string) (factory: unit -> Async<'T>) : Async<'T> =
        async {
            match get<'T> key with
            | Some cached -> return cached
            | None ->
                let! value = factory()
                set key value
                return value
        }
    
    let clear () =
        cache.Clear()

// Usage in API
let itemApi = {
    getItems = fun () -> async {
        return!
            Cache.getOrAdd "all-items" (fun () ->
                Persistence.getAllItems()
            )
    }
}
```

## Logging

```fsharp
module Logging =
    open Microsoft.Extensions.Logging
    
    let mutable private logger: ILogger option = None
    
    let initialize (log: ILogger) =
        logger <- Some log
    
    let logInfo message =
        logger |> Option.iter (fun l -> l.LogInformation(message))
    
    let logError message (ex: exn) =
        logger |> Option.iter (fun l -> l.LogError(ex, message))
    
    let logDebug message =
        logger |> Option.iter (fun l -> l.LogDebug(message))

// Usage in Program.fs
let configureApp (app: IApplicationBuilder) =
    let logger = app.ApplicationServices.GetService<ILogger<obj>>()
    Logging.initialize logger
    
    // ... rest of configuration

// Usage in API
let itemApi = {
    saveItem = fun item -> async {
        Logging.logInfo $"Saving item: {item.Name}"
        
        try
            do! Persistence.saveItem item
            Logging.logInfo $"Item saved successfully: {item.Id}"
            return Ok item
        with ex ->
            Logging.logError "Failed to save item" ex
            return Error ex.Message
    }
}
```

## Testing Backends with Expecto

```fsharp
module Tests.ApiTests

open Expecto
open Shared.Domain

[<Tests>]
let apiTests =
    testList "API Tests" [
        testCase "Get items returns list" <| fun () ->
            let result = 
                Api.itemApi.getItems()
                |> Async.RunSynchronously
            
            Expect.isNotEmpty result "Should return items"
        
        testCase "Get non-existent item returns error" <| fun () ->
            let result =
                Api.itemApi.getItem 99999
                |> Async.RunSynchronously
            
            match result with
            | Error _ -> ()
            | Ok _ -> failtest "Should return error for non-existent item"
        
        testCase "Save valid item succeeds" <| fun () ->
            let item = { Id = 0; Name = "Test Item" }
            
            let result =
                Api.itemApi.saveItem item
                |> Async.RunSynchronously
            
            match result with
            | Ok saved -> Expect.equal saved.Name item.Name "Name should match"
            | Error e -> failtest $"Should succeed: {e}"
    ]
```

## Best Practices

### 1. Keep Handlers Small

```fsharp
// ❌ Bad: Everything in API handler
let saveItem item = async {
    if String.IsNullOrEmpty item.Name then
        return Error "Name required"
    else
        let processed = { item with Name = item.Name.Trim() }
        do! Persistence.saveItem processed
        do! Persistence.logEvent (ItemCreated processed)
        return Ok processed
}

// ✅ Good: Delegate to domain and validation
let saveItem item = async {
    match Validation.validateItem item with
    | Error errors -> return Error (String.concat ", " errors)
    | Ok valid ->
        let processed = Domain.processItem valid
        do! Persistence.saveItem processed
        do! Persistence.logEvent (ItemCreated processed)
        return Ok processed
}
```

### 2. Use Result for Expected Failures

```fsharp
// Expected failures (use Result)
let getItem id : Async<Result<Item, string>>

// Unexpected failures (throw/catch)
let saveItem item : Async<Item>  // Throws on DB failure
```

### 3. Separate I/O from Logic

```fsharp
// ✅ Good separation
module Domain =
    let calculatePrice (item: Item) (discount: Discount) : decimal =
        item.Price * (1.0m - discount.Percentage)

module Api =
    let api = {
        checkout = fun itemId discountCode -> async {
            let! item = Persistence.getItem itemId
            let! discount = Persistence.getDiscount discountCode
            
            let finalPrice = Domain.calculatePrice item discount
            
            do! Persistence.recordTransaction itemId finalPrice
            return Ok finalPrice
        }
    }
```

### 4. Handle Async Cancellation

```fsharp
let longRunningOperation (ct: System.Threading.CancellationToken) : Async<unit> =
    async {
        for i in 1 .. 1000 do
            ct.ThrowIfCancellationRequested()
            do! Async.Sleep 100
            // ... work
    }
```

## Next Steps

- Read `04-SHARED-TYPES.md` for type sharing patterns
- Read `05-PERSISTENCE.md` for database and file patterns
- Read `06-TESTING.md` for comprehensive testing strategies
