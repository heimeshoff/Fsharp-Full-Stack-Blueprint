# Shared Types Guide

## Purpose of Shared Types

The `/src/Shared` project contains types and contracts that are used by both the client (Fable) and server (Giraffe). This ensures:

1. **Type safety across the network boundary**
2. **Single source of truth for domain models**
3. **Compile-time guarantees that client and server agree**
4. **No manual JSON serialization/deserialization**

## Project Structure

```
src/Shared/
├── Domain.fs       # Core domain types
├── Api.fs          # Fable.Remoting API contracts
├── Validation.fs   # Shared validation rules (optional)
└── Shared.fsproj
```

## Domain.fs - Core Domain Types

Define your business entities here:

```fsharp
module Shared.Domain

open System

// ============================================
// Core Entities
// ============================================

type Item = {
    Id: int
    Name: string
    Description: string
    CreatedAt: DateTime
    UpdatedAt: DateTime
}

type ItemUpdate = {
    Name: string option
    Description: string option
}

type User = {
    Id: int
    Username: string
    Email: string
}

// ============================================
// Value Objects
// ============================================

type EmailAddress = private EmailAddress of string
module EmailAddress =
    let create (email: string) : Result<EmailAddress, string> =
        if email.Contains("@") && email.Contains(".") then
            Ok (EmailAddress email)
        else
            Error "Invalid email format"
    
    let value (EmailAddress email) = email

type ItemId = ItemId of int
module ItemId =
    let create (id: int) : ItemId = ItemId id
    let value (ItemId id) = id

// ============================================
// Enums / Discriminated Unions
// ============================================

type ItemStatus =
    | Draft
    | Published
    | Archived

type SortOrder =
    | Ascending
    | Descending

type ItemFilter =
    | All
    | ByStatus of ItemStatus
    | ByName of searchTerm: string
    | ByDateRange of startDate: DateTime * endDate: DateTime

// ============================================
// Responses and Results
// ============================================

type PagedResult<'T> = {
    Items: 'T list
    TotalCount: int
    Page: int
    PageSize: int
}

type ApiError =
    | NotFound of entityType: string
    | ValidationError of errors: string list
    | ServerError of message: string

// Common response type
type ApiResult<'T> = Result<'T, string>

// ============================================
// DTOs (Data Transfer Objects)
// ============================================

type CreateItemRequest = {
    Name: string
    Description: string
}

type UpdateItemRequest = {
    Id: int
    Name: string option
    Description: string option
}

type SearchRequest = {
    Query: string
    Page: int
    PageSize: int
    SortBy: string
    SortOrder: SortOrder
}

type DashboardSummary = {
    TotalItems: int
    PublishedItems: int
    DraftItems: int
    RecentItems: Item list
}

// ============================================
// Events (for event sourcing)
// ============================================

type ItemEvent =
    | ItemCreated of Item
    | ItemUpdated of Item
    | ItemPublished of itemId: int
    | ItemArchived of itemId: int
    | ItemDeleted of itemId: int
    
type Event<'T> = {
    Id: Guid
    Timestamp: DateTime
    Data: 'T
}
```

## Api.fs - Fable.Remoting Contracts

Define your API surface:

```fsharp
module Shared.Api

open System
open Domain

// ============================================
// Single API Interface
// ============================================

type IItemApi = {
    // Queries
    getItems: unit -> Async<Item list>
    getItem: itemId: int -> Async<ApiResult<Item>>
    searchItems: query: SearchRequest -> Async<PagedResult<Item>>
    
    // Commands
    createItem: request: CreateItemRequest -> Async<ApiResult<Item>>
    updateItem: request: UpdateItemRequest -> Async<ApiResult<Item>>
    deleteItem: itemId: int -> Async<ApiResult<unit>>
    
    // Bulk operations
    importItems: items: Item list -> Async<ApiResult<int>>
    exportItems: filter: ItemFilter -> Async<byte[]>
}

// ============================================
// Multiple API Interfaces (if needed)
// ============================================

type IUserApi = {
    getCurrentUser: unit -> Async<User>
    updateProfile: user: User -> Async<ApiResult<User>>
}

type IStatsApi = {
    getDashboard: unit -> Async<DashboardSummary>
    getItemStats: itemId: int -> Async<ItemStats>
}

// ============================================
// API Grouping (Alternative Pattern)
// ============================================

type IAppApi = {
    Items: IItemApi
    Users: IUserApi
    Stats: IStatsApi
}
```

## Type Design Patterns

### 1. Required vs Optional Fields

```fsharp
// Creation request - all required
type CreateItemRequest = {
    Name: string
    Description: string
}

// Update request - all optional (partial update)
type UpdateItemRequest = {
    Id: int  // ID is always required
    Name: string option
    Description: string option
}

// In backend update logic:
let applyUpdate (existing: Item) (update: UpdateItemRequest) : Item =
    { existing with
        Name = update.Name |> Option.defaultValue existing.Name
        Description = update.Description |> Option.defaultValue existing.Description
        UpdatedAt = DateTime.UtcNow
    }
```

### 2. Discriminated Unions for States

```fsharp
// Represent mutually exclusive states
type LoadingState<'T> =
    | NotStarted
    | InProgress
    | Completed of 'T
    | Failed of error: string

type OrderStatus =
    | Pending
    | Confirmed of confirmedAt: DateTime
    | Shipped of trackingNumber: string
    | Delivered of deliveredAt: DateTime
    | Cancelled of reason: string

// Pattern matching forces handling all cases
let getStatusMessage (status: OrderStatus) : string =
    match status with
    | Pending -> "Order is pending"
    | Confirmed date -> $"Confirmed on {date.ToShortDateString()}"
    | Shipped tracking -> $"Shipped: {tracking}"
    | Delivered date -> $"Delivered on {date.ToShortDateString()}"
    | Cancelled reason -> $"Cancelled: {reason}"
```

### 3. Single Case Unions for Type Safety

```fsharp
// Wrap primitive types to prevent mistakes
type UserId = UserId of int
type ItemId = ItemId of int
type ProductId = ProductId of int

// Prevents mixing up IDs
let getUser (userId: UserId) : User = // ...
let getItem (itemId: ItemId) : Item = // ...

// ❌ Can't accidentally pass wrong ID type
// getUser (ItemId 5)  // Compile error!

// ✅ Must use correct type
getUser (UserId 5)
```

### 4. Result Type for Operations

```fsharp
// Use Result for expected failures
type ApiResult<'T> = Result<'T, string>

// More detailed error types
type DetailedResult<'T> = Result<'T, ApiError>

type ApiError =
    | NotFound of entityType: string * id: int
    | ValidationError of field: string * message: string
    | Unauthorized
    | ServerError of message: string

// In API
type IItemApi = {
    getItem: itemId: int -> Async<Result<Item, ApiError>>
    saveItem: item: Item -> Async<Result<Item, ApiError>>
}
```

### 5. Paged Results

```fsharp
type PagedResult<'T> = {
    Items: 'T list
    TotalCount: int
    Page: int
    PageSize: int
    HasNextPage: bool
    HasPreviousPage: bool
}

module PagedResult =
    let create (items: 'T list) (totalCount: int) (page: int) (pageSize: int) =
        {
            Items = items
            TotalCount = totalCount
            Page = page
            PageSize = pageSize
            HasNextPage = (page * pageSize) < totalCount
            HasPreviousPage = page > 1
        }
```

### 6. Command/Query Separation

```fsharp
// Commands (write operations) - return Result
type ICommandApi = {
    createItem: CreateItemRequest -> Async<Result<Item, string>>
    updateItem: UpdateItemRequest -> Async<Result<Item, string>>
    deleteItem: int -> Async<Result<unit, string>>
}

// Queries (read operations) - return data directly or Option
type IQueryApi = {
    getItems: unit -> Async<Item list>
    getItem: int -> Async<Item option>
    searchItems: string -> Async<Item list>
}
```

## What NOT to Share

### ❌ Server-Only Types

Keep these in `src/Server`:
- Database entities with SQLite-specific attributes
- Internal caching structures
- Server configuration types
- File system paths
- Connection strings

```fsharp
// Server-only type (in src/Server/Types.fs)
type DbItem = {
    [<PrimaryKey; AutoIncrement>]
    Id: int
    Name: string
    Description: string
    CreatedAt: DateTime
    UpdatedAt: DateTime
    // Metadata not exposed to client
    DbVersion: int
    LastSyncedAt: DateTime
}

// Map to shared type before sending to client
let toSharedItem (dbItem: DbItem) : Item =
    {
        Id = dbItem.Id
        Name = dbItem.Name
        Description = dbItem.Description
        CreatedAt = dbItem.CreatedAt
        UpdatedAt = dbItem.UpdatedAt
    }
```

### ❌ Client-Only Types

Keep these in `src/Client/Types.fs`:
- React component state
- UI-specific types (modals, toasts, forms)
- Browser-specific types
- View models

```fsharp
// Client-only types (in src/Client/Types.fs)
type RemoteData<'T> =
    | NotAsked
    | Loading
    | Success of 'T
    | Failure of string

type Toast = {
    Message: string
    Type: ToastType
}

type ToastType =
    | ToastSuccess
    | ToastError
    | ToastInfo

type Page =
    | HomePage
    | DetailPage of itemId: int
    | NotFound
```

## JSON Serialization Considerations

Fable.Remoting handles JSON serialization automatically, but keep these in mind:

### 1. DateTime Handling

```fsharp
// Works fine - serializes as ISO 8601 string
type Item = {
    Id: int
    CreatedAt: DateTime
}

// In client, received as JavaScript Date (automatically converted)
// In server, received as .NET DateTime
```

### 2. Option Types

```fsharp
// Option<'T> serializes as null or value
type UpdateRequest = {
    Name: string option  // null or "value"
    Age: int option      // null or 42
}

// Client receives:
// { "Name": "John", "Age": null }
// Server receives:
// { Name = Some "John"; Age = None }
```

### 3. Discriminated Unions

```fsharp
// DU serializes as {"Case": "CaseName", "Fields": [...]}
type Status =
    | Active
    | Inactive
    | Suspended of reason: string

// Serializes to:
// { "Case": "Suspended", "Fields": ["Policy violation"] }
```

### 4. Records vs Classes

```fsharp
// ✅ Prefer records (immutable, structural equality)
type Item = {
    Id: int
    Name: string
}

// ❌ Avoid classes (mutable, reference equality)
type Item() =
    member val Id = 0 with get, set
    member val Name = "" with get, set
```

## Versioning Strategy

### Option 1: Add New Fields

```fsharp
// V1
type Item = {
    Id: int
    Name: string
}

// V2 - Add optional field (backward compatible)
type Item = {
    Id: int
    Name: string
    Description: string option  // New field
}
```

### Option 2: Separate API Versions

```fsharp
module V1 =
    type Item = { Id: int; Name: string }
    
    type IItemApi = {
        getItems: unit -> Async<Item list>
    }

module V2 =
    type Item = { Id: int; Name: string; Description: string }
    
    type IItemApi = {
        getItems: unit -> Async<Item list>
        getItemsWithDetails: unit -> Async<Item list>
    }

// In server, expose both
let apiV1 = Remoting.createApi() |> Remoting.withRouteBuilder (fun t m -> $"/api/v1/{t}/{m}") |> ...
let apiV2 = Remoting.createApi() |> Remoting.withRouteBuilder (fun t m -> $"/api/v2/{t}/{m}") |> ...
```

### Option 3: Feature Flags

```fsharp
type ApiFeatures = {
    SupportsAdvancedSearch: bool
    SupportsExport: bool
}

type IAppApi = {
    getFeatures: unit -> Async<ApiFeatures>
    getItems: unit -> Async<Item list>
    // Only call if SupportsAdvancedSearch = true
    advancedSearch: SearchRequest -> Async<PagedResult<Item>>
}
```

## Testing Shared Types

```fsharp
module Tests.SharedTypes

open Expecto
open Shared.Domain

[<Tests>]
let domainTests =
    testList "Domain Types" [
        testCase "Item creation" <| fun () ->
            let item = {
                Id = 1
                Name = "Test"
                Description = "Test item"
                CreatedAt = DateTime.UtcNow
                UpdatedAt = DateTime.UtcNow
            }
            
            Expect.equal item.Name "Test" "Name should match"
        
        testCase "EmailAddress validation" <| fun () ->
            let valid = EmailAddress.create "test@example.com"
            let invalid = EmailAddress.create "notanemail"
            
            Expect.isOk valid "Should accept valid email"
            Expect.isError invalid "Should reject invalid email"
        
        testCase "ItemStatus pattern matching" <| fun () ->
            let status = Published
            let message =
                match status with
                | Draft -> "Draft"
                | Published -> "Published"
                | Archived -> "Archived"
            
            Expect.equal message "Published" "Should match status"
    ]
```

## Common Pitfalls

### ❌ Don't: Circular Dependencies

```fsharp
// Shared.Domain depends on Shared.Api
// Shared.Api depends on Shared.Domain
// This creates a cycle!

// Solution: Keep them separate or in single file
```

### ❌ Don't: Expose Implementation Details

```fsharp
// Bad - exposing database IDs
type Item = {
    DbId: int64  // SQLite-specific
    MongoId: string  // MongoDB-specific
}

// Good - abstract ID
type Item = {
    Id: string
}
```

### ❌ Don't: Use Mutable Types

```fsharp
// Bad
type Item() =
    member val Name = "" with get, set

// Good
type Item = {
    Name: string
}
```

### ✅ Do: Use Meaningful Names

```fsharp
// Bad
type Req = { N: string; D: string }

// Good
type CreateItemRequest = {
    Name: string
    Description: string
}
```

## Best Practices Summary

1. **Keep it simple**: Shared types should be pure data structures
2. **Use records**: Immutable, structural equality, clean syntax
3. **Leverage DUs**: For states, enums, and variants
4. **Type safety**: Use single-case unions to prevent ID mixups
5. **Result types**: For operations that can fail
6. **Option types**: For optional data
7. **Avoid classes**: Prefer records over classes
8. **No side effects**: No I/O, no mutable state
9. **Test your types**: Ensure serialization works as expected
10. **Document complex types**: Add XML comments for clarity

## Next Steps

- Read `05-PERSISTENCE.md` for database patterns
- Read `06-TESTING.md` for testing strategies
- Reference this guide when adding new API endpoints
