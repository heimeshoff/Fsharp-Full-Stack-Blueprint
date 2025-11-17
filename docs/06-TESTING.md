# Testing Guide with Expecto

## Test Project Structure

```
src/Tests/
├── Shared.Tests/
│   ├── DomainTests.fs
│   ├── ValidationTests.fs
│   └── Shared.Tests.fsproj
│
├── Server.Tests/
│   ├── ApiTests.fs
│   ├── PersistenceTests.fs
│   ├── DomainTests.fs
│   └── Server.Tests.fsproj
│
└── Client.Tests/
    ├── StateTests.fs
    ├── ViewTests.fs
    └── Client.Tests.fsproj
```

## Expecto Basics

### Test Project Setup

**Shared.Tests.fsproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="DomainTests.fs" />
    <Compile Include="ValidationTests.fs" />
    <Compile Include="Program.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Expecto" Version="10.2.1" />
    <PackageReference Include="Expecto.FsCheck" Version="10.2.1" />
    <PackageReference Include="FsCheck" Version="3.0.0-rc3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../Shared/Shared.fsproj" />
  </ItemGroup>
</Project>
```

**Program.fs** (test runner):
```fsharp
module Program

open Expecto

[<EntryPoint>]
let main args =
    runTestsInAssembly defaultConfig args
```

### Basic Test Structure

```fsharp
module Tests.DomainTests

open Expecto
open Shared.Domain

[<Tests>]
let domainTests =
    testList "Domain Tests" [
        testCase "Item creation sets properties correctly" <| fun () ->
            let item = {
                Id = 1
                Name = "Test Item"
                Description = "Description"
                CreatedAt = System.DateTime.UtcNow
                UpdatedAt = System.DateTime.UtcNow
            }
            
            Expect.equal item.Name "Test Item" "Name should match"
            Expect.equal item.Id 1 "ID should match"
        
        testCase "Email validation accepts valid email" <| fun () ->
            let result = EmailAddress.create "test@example.com"
            Expect.isOk result "Should accept valid email"
        
        testCase "Email validation rejects invalid email" <| fun () ->
            let result = EmailAddress.create "not-an-email"
            Expect.isError result "Should reject invalid email"
    ]
```

## Testing Patterns

### 1. Arrange-Act-Assert Pattern

```fsharp
testCase "Process item normalizes name" <| fun () ->
    // Arrange
    let item = {
        Id = 1
        Name = "  Test Item  "
        Description = "Description"
        CreatedAt = DateTime.UtcNow
        UpdatedAt = DateTime.UtcNow
    }
    
    // Act
    let processed = Domain.processItem item
    
    // Assert
    Expect.equal processed.Name "Test Item" "Name should be trimmed"
```

### 2. Test Lists and Organization

```fsharp
[<Tests>]
let allTests =
    testList "All Tests" [
        testList "Domain" [
            testCase "Test 1" <| fun () -> ()
            testCase "Test 2" <| fun () -> ()
        ]
        
        testList "Validation" [
            testCase "Test 1" <| fun () -> ()
            testCase "Test 2" <| fun () -> ()
        ]
    ]

// Or use modules
module DomainTests =
    [<Tests>]
    let tests =
        testList "Domain" [
            testCase "Test 1" <| fun () -> ()
        ]

module ValidationTests =
    [<Tests>]
    let tests =
        testList "Validation" [
            testCase "Test 1" <| fun () -> ()
        ]
```

### 3. Parameterized Tests

```fsharp
let validateEmailTests =
    [
        "valid@example.com", true
        "another@test.org", true
        "invalid", false
        "@example.com", false
        "test@", false
        "test", false
    ]
    |> List.map (fun (email, shouldBeValid) ->
        testCase $"Email '{email}' validation" <| fun () ->
            let result = EmailAddress.create email
            if shouldBeValid then
                Expect.isOk result $"'{email}' should be valid"
            else
                Expect.isError result $"'{email}' should be invalid"
    )

[<Tests>]
let emailTests =
    testList "Email Validation" validateEmailTests
```

### 4. Testing Async Operations

```fsharp
testCase "Load items returns data" <| fun () ->
    // Use Async.RunSynchronously for sync tests
    let items = 
        Api.itemApi.getItems()
        |> Async.RunSynchronously
    
    Expect.isNotEmpty items "Should return items"

// Or use testAsync for native async tests
testAsync "Load items returns data (async)" {
    let! items = Api.itemApi.getItems()
    Expect.isNotEmpty items "Should return items"
}
```

### 5. Testing Result Types

```fsharp
testCase "Save valid item succeeds" <| fun () ->
    let item = { Id = 0; Name = "Valid"; Description = "Valid" }
    
    let result =
        Api.itemApi.saveItem item
        |> Async.RunSynchronously
    
    match result with
    | Ok savedItem ->
        Expect.equal savedItem.Name item.Name "Name should match"
    | Error e ->
        failtest $"Should succeed but got error: {e}"

testCase "Save invalid item fails" <| fun () ->
    let item = { Id = 0; Name = ""; Description = "" }
    
    let result =
        Api.itemApi.saveItem item
        |> Async.RunSynchronously
    
    match result with
    | Ok _ ->
        failtest "Should fail validation"
    | Error msg ->
        Expect.stringContains msg "Name" "Error should mention Name field"
```

## Testing Elmish (Client State)

### Testing Update Function

```fsharp
module Tests.StateTests

open Expecto
open State
open Types

[<Tests>]
let stateTests =
    testList "State Tests" [
        testCase "Init returns NotAsked state" <| fun () ->
            let model, _ = State.init()
            
            match model.Items with
            | NotAsked -> ()
            | _ -> failtest "Should be NotAsked initially"
        
        testCase "LoadItems sets Loading state" <| fun () ->
            let model, _ = State.init()
            let updatedModel, _ = State.update LoadItems model
            
            match updatedModel.Items with
            | Loading -> ()
            | _ -> failtest "Should be Loading after LoadItems"
        
        testCase "ItemsLoaded (Ok) sets Success state" <| fun () ->
            let model = { (fst (State.init())) with Items = Loading }
            let items = [ { Id = 1; Name = "Test" } ]
            
            let updatedModel, _ = State.update (ItemsLoaded (Ok items)) model
            
            match updatedModel.Items with
            | Success loadedItems ->
                Expect.equal loadedItems items "Items should match"
            | _ ->
                failtest "Should be Success"
        
        testCase "ItemsLoaded (Error) sets Failure state" <| fun () ->
            let model = { (fst (State.init())) with Items = Loading }
            
            let updatedModel, _ = State.update (ItemsLoaded (Error "Failed")) model
            
            match updatedModel.Items with
            | Failure msg ->
                Expect.equal msg "Failed" "Error message should match"
            | _ ->
                failtest "Should be Failure"
        
        testCase "FormInputChanged updates input" <| fun () ->
            let model, _ = State.init()
            
            let updatedModel, _ = State.update (FormInputChanged "test") model
            
            Expect.equal updatedModel.FormInput "test" "Input should update"
    ]
```

### Testing Commands

```fsharp
testCase "LoadItems generates correct command" <| fun () ->
    let model, _ = State.init()
    let _, cmd = State.update LoadItems model
    
    // Commands are harder to test directly, but we can test the side effects
    // by mocking the API
    ()
```

## Testing Backend (Server)

### Testing Domain Logic

```fsharp
module Tests.DomainTests

open Expecto
open Domain
open Shared.Domain

[<Tests>]
let domainTests =
    testList "Domain Logic" [
        testCase "Process item trims name" <| fun () ->
            let item = {
                Id = 1
                Name = "  Test  "
                Description = "Description"
                CreatedAt = DateTime.UtcNow
                UpdatedAt = DateTime.UtcNow
            }
            
            let processed = Domain.processItem item
            
            Expect.equal processed.Name "Test" "Should trim whitespace"
        
        testCase "Calculate item score correctly" <| fun () ->
            let item = {
                Id = 1
                Name = "Test"  // 4 chars
                Description = ""
                CreatedAt = DateTime.UtcNow
                UpdatedAt = DateTime.UtcNow
            }
            
            let score = Domain.calculateItemScore item
            
            Expect.equal score 40 "Score should be 4 * 10"
        
        testCase "Filter valid items" <| fun () ->
            let items = [
                { Id = 1; Name = "Valid"; Description = ""; CreatedAt = DateTime.UtcNow; UpdatedAt = DateTime.UtcNow }
                { Id = 2; Name = ""; Description = ""; CreatedAt = DateTime.UtcNow; UpdatedAt = DateTime.UtcNow }
                { Id = 3; Name = "Al"; Description = ""; CreatedAt = DateTime.UtcNow; UpdatedAt = DateTime.UtcNow }
            ]
            
            let processed = Domain.processItemList items
            
            Expect.hasLength processed 1 "Should filter to 1 valid item"
    ]
```

### Testing Validation

```fsharp
module Tests.ValidationTests

open Expecto
open Validation
open Shared.Domain

[<Tests>]
let validationTests =
    testList "Validation" [
        testCase "Valid item passes validation" <| fun () ->
            let item = {
                Id = 1
                Name = "Valid Name"
                Description = "Valid Description"
                CreatedAt = DateTime.UtcNow
                UpdatedAt = DateTime.UtcNow
            }
            
            let result = Validation.validateItem item
            
            Expect.isOk result "Should pass validation"
        
        testCase "Empty name fails validation" <| fun () ->
            let item = {
                Id = 1
                Name = ""
                Description = "Valid"
                CreatedAt = DateTime.UtcNow
                UpdatedAt = DateTime.UtcNow
            }
            
            let result = Validation.validateItem item
            
            match result with
            | Error errors ->
                Expect.isNonEmpty errors "Should have errors"
                Expect.exists errors (fun e -> e.Contains "Name") "Should mention Name"
            | Ok _ ->
                failtest "Should fail validation"
        
        testCase "Name too short fails validation" <| fun () ->
            let item = {
                Id = 1
                Name = "ab"  // Too short (min 3)
                Description = "Valid"
                CreatedAt = DateTime.UtcNow
                UpdatedAt = DateTime.UtcNow
            }
            
            let result = Validation.validateItem item
            
            Expect.isError result "Should fail validation"
    ]
```

### Testing Persistence (with In-Memory DB)

```fsharp
module Tests.PersistenceTests

open Expecto
open System.IO
open Microsoft.Data.Sqlite
open Persistence

// Helper to create test database
let createTestDb () =
    let conn = new SqliteConnection("Data Source=:memory:")
    conn.Open()
    
    // Initialize schema
    use cmd = new SqliteCommand("""
        CREATE TABLE items (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            description TEXT NOT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        )
    """, conn)
    cmd.ExecuteNonQuery() |> ignore
    
    conn

[<Tests>]
let persistenceTests =
    testList "Persistence" [
        testCase "Insert and retrieve item" <| fun () ->
            use conn = createTestDb()
            
            // Insert
            let item = {
                Id = 0
                Name = "Test"
                Description = "Test Description"
                CreatedAt = DateTime.UtcNow
                UpdatedAt = DateTime.UtcNow
            }
            
            let insertedItem =
                // Use your actual persistence functions with the test connection
                // You may need to adapt your persistence layer to accept connections
                item
            
            Expect.isGreaterThan insertedItem.Id 0 "Should assign ID"
        
        testCase "Get non-existent item returns None" <| fun () ->
            use conn = createTestDb()
            
            let result =
                // Your getItemById function
                None
            
            Expect.isNone result "Should return None"
    ]
```

### Testing API Layer

```fsharp
module Tests.ApiTests

open Expecto
open Api
open Shared.Api

// Mock persistence for testing
module MockPersistence =
    let mutable items = []
    
    let getAllItems () = async { return items }
    let getItemById id = async { return items |> List.tryFind (fun i -> i.Id = id) }
    let saveItem item = async { items <- item :: items }
    let deleteItem id = async { items <- items |> List.filter (fun i -> i.Id <> id) }

[<Tests>]
let apiTests =
    testList "API Tests" [
        testCase "Get items returns list" <| fun () ->
            // Setup mock
            MockPersistence.items <- [
                { Id = 1; Name = "Item 1"; Description = ""; CreatedAt = DateTime.UtcNow; UpdatedAt = DateTime.UtcNow }
            ]
            
            let result =
                itemApi.getItems()
                |> Async.RunSynchronously
            
            Expect.hasLength result 1 "Should return 1 item"
        
        testCase "Save valid item succeeds" <| fun () ->
            MockPersistence.items <- []
            
            let item = {
                Id = 0
                Name = "New Item"
                Description = "Description"
                CreatedAt = DateTime.UtcNow
                UpdatedAt = DateTime.UtcNow
            }
            
            let result =
                itemApi.saveItem item
                |> Async.RunSynchronously
            
            match result with
            | Ok saved ->
                Expect.equal saved.Name item.Name "Name should match"
            | Error e ->
                failtest $"Should succeed: {e}"
    ]
```

## Property-Based Testing with FsCheck

```fsharp
module Tests.PropertyTests

open Expecto
open Expecto.FsCheck
open FsCheck
open Domain

[<Tests>]
let propertyTests =
    testList "Property Tests" [
        testProperty "Process item always trims name" <| fun (item: Item) ->
            let processed = Domain.processItem item
            processed.Name = item.Name.Trim()
        
        testProperty "Item score is always positive" <| fun (item: Item) ->
            let score = Domain.calculateItemScore item
            score >= 0
        
        testProperty "Filtering items returns subset" <| fun (items: Item list) ->
            let filtered = Domain.processItemList items
            filtered.Length <= items.Length
    ]

// Custom generators
type ItemGenerator =
    static member Item() =
        Arb.generate<string>
        |> Gen.map (fun name ->
            {
                Id = 1
                Name = name
                Description = "Test"
                CreatedAt = DateTime.UtcNow
                UpdatedAt = DateTime.UtcNow
            }
        )
        |> Arb.fromGen
```

## Test Helpers and Fixtures

```fsharp
module TestHelpers =
    
    // Sample data builders
    let createTestItem id name =
        {
            Id = id
            Name = name
            Description = $"Description for {name}"
            CreatedAt = DateTime.UtcNow
            UpdatedAt = DateTime.UtcNow
        }
    
    let createTestItems count =
        [ 1 .. count ]
        |> List.map (fun i -> createTestItem i $"Item {i}")
    
    // Assertions
    let expectSuccess result =
        match result with
        | Ok value -> value
        | Error e -> failtest $"Expected success but got error: {e}"
    
    let expectFailure result =
        match result with
        | Ok _ -> failtest "Expected failure but got success"
        | Error msg -> msg
    
    // Test fixtures
    type DatabaseFixture() =
        member val Connection = createTestDb()
        
        interface System.IDisposable with
            member this.Dispose() =
                this.Connection.Dispose()

// Usage
testCase "Test with fixture" <| fun () ->
    use fixture = new TestHelpers.DatabaseFixture()
    // Use fixture.Connection
    ()
```

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test src/Tests/Shared.Tests

# Run with filter
dotnet test --filter "FullyQualifiedName~Domain"

# Run with verbosity
dotnet test --logger "console;verbosity=detailed"
```

### Watch Mode

```bash
# Watch and re-run tests on file changes
dotnet watch test
```

### Expecto CLI Options

```bash
# Run tests matching filter
dotnet run --project src/Tests/Shared.Tests -- --filter "Domain"

# Run tests sequentially (not in parallel)
dotnet run --project src/Tests/Shared.Tests -- --sequenced

# List all tests
dotnet run --project src/Tests/Shared.Tests -- --list

# Run with summary
dotnet run --project src/Tests/Shared.Tests -- --summary
```

## Test Coverage

### Using Coverlet

Add to test project:
```xml
<PackageReference Include="coverlet.collector" Version="6.0.0">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

Run with coverage:
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Best Practices

1. **Test pure functions first**: Easiest to test, highest value
2. **Mock external dependencies**: Database, file system, network
3. **One assertion per test**: Makes failures clear
4. **Use descriptive test names**: "Should validate email correctly"
5. **Arrange-Act-Assert**: Clear test structure
6. **Test both happy and sad paths**: Success and failure cases
7. **Use property-based testing**: For invariants and edge cases
8. **Keep tests fast**: Use in-memory databases, mock I/O
9. **Tests should be independent**: No shared state between tests
10. **Test behavior, not implementation**: Focus on what, not how

## Test Pyramid

```
        /\
       /  \     E2E Tests (Few)
      /____\    
     /      \   Integration Tests (Some)
    /________\  
   /          \ Unit Tests (Many)
  /____________\
```

Focus on:
- **Many unit tests**: Fast, isolated, test pure functions
- **Some integration tests**: Test API + persistence together
- **Few E2E tests**: Full stack, slowest, most brittle

## Next Steps

- Read `07-BUILD-DEPLOY.md` for deployment configuration
- Add tests incrementally as you develop features
- Run tests before committing code
- Consider CI/CD pipeline for automated testing
