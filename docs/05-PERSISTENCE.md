# Persistence Guide

## Storage Options

For home server applications, we use two persistence strategies:

1. **SQLite**: Structured data requiring queries, indexes, relationships
2. **Local Files**: Simple configuration, caching, event logs

## SQLite Setup

### Dependencies

Add to `Server.fsproj`:
```xml
<PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
<PackageReference Include="Dapper" Version="2.1.35" />
```

### Connection String Pattern

```fsharp
module Persistence

open System.IO
open Microsoft.Data.Sqlite

let private dataDir = "./data"
let private dbPath = Path.Combine(dataDir, "app.db")
let private connectionString = $"Data Source={dbPath}"

let ensureDataDir () =
    if not (Directory.Exists dataDir) then
        Directory.CreateDirectory dataDir |> ignore

let getConnection () =
    new SqliteConnection(connectionString)
```

### Database Initialization

```fsharp
let initializeDatabase () =
    ensureDataDir()
    
    use conn = getConnection()
    conn.Open()
    
    // Enable WAL mode for better concurrency
    use walCmd = new SqliteCommand("PRAGMA journal_mode=WAL;", conn)
    walCmd.ExecuteNonQuery() |> ignore
    
    // Create tables
    use cmd = new SqliteCommand("""
        CREATE TABLE IF NOT EXISTS items (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            description TEXT NOT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );
        
        CREATE INDEX IF NOT EXISTS idx_items_name ON items(name);
        CREATE INDEX IF NOT EXISTS idx_items_created_at ON items(created_at);
    """, conn)
    
    cmd.ExecuteNonQuery() |> ignore
```

## Dapper Query Patterns

### Basic Queries

```fsharp
open Dapper
open Shared.Domain

// ============================================
// Query All
// ============================================

let getAllItems () : Async<Item list> =
    async {
        use conn = getConnection()
        
        let! items =
            conn.QueryAsync<Item>("""
                SELECT id, name, description, created_at, updated_at
                FROM items
                ORDER BY created_at DESC
            """)
            |> Async.AwaitTask
        
        return items |> Seq.toList
    }

// ============================================
// Query by ID
// ============================================

let getItemById (itemId: int) : Async<Item option> =
    async {
        use conn = getConnection()
        
        let! item =
            conn.QueryFirstOrDefaultAsync<Item>(
                "SELECT id, name, description, created_at, updated_at FROM items WHERE id = @Id",
                {| Id = itemId |}
            )
            |> Async.AwaitTask
        
        return
            if isNull (box item) then None
            else Some item
    }

// ============================================
// Query with Parameters
// ============================================

let searchItems (searchTerm: string) : Async<Item list> =
    async {
        use conn = getConnection()
        
        let! items =
            conn.QueryAsync<Item>(
                """
                SELECT id, name, description, created_at, updated_at
                FROM items
                WHERE name LIKE @SearchTerm OR description LIKE @SearchTerm
                ORDER BY name
                """,
                {| SearchTerm = $"%%{searchTerm}%%" |}
            )
            |> Async.AwaitTask
        
        return items |> Seq.toList
    }

// ============================================
// Paged Query
// ============================================

let getItemsPaged (page: int) (pageSize: int) : Async<Item list * int> =
    async {
        use conn = getConnection()
        
        let offset = (page - 1) * pageSize
        
        let! items =
            conn.QueryAsync<Item>(
                """
                SELECT id, name, description, created_at, updated_at
                FROM items
                ORDER BY created_at DESC
                LIMIT @PageSize OFFSET @Offset
                """,
                {| PageSize = pageSize; Offset = offset |}
            )
            |> Async.AwaitTask
        
        let! totalCount =
            conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM items")
            |> Async.AwaitTask
        
        return (items |> Seq.toList, totalCount)
    }
```

### Insert/Update/Delete

```fsharp
open System

// ============================================
// Insert
// ============================================

let insertItem (item: Item) : Async<Item> =
    async {
        use conn = getConnection()
        
        let now = DateTime.UtcNow
        
        let! id =
            conn.ExecuteScalarAsync<int64>(
                """
                INSERT INTO items (name, description, created_at, updated_at)
                VALUES (@Name, @Description, @CreatedAt, @UpdatedAt);
                SELECT last_insert_rowid();
                """,
                {|
                    Name = item.Name
                    Description = item.Description
                    CreatedAt = now.ToString("O")
                    UpdatedAt = now.ToString("O")
                |}
            )
            |> Async.AwaitTask
        
        return {
            Id = int id
            Name = item.Name
            Description = item.Description
            CreatedAt = now
            UpdatedAt = now
        }
    }

// ============================================
// Update
// ============================================

let updateItem (item: Item) : Async<unit> =
    async {
        use conn = getConnection()
        
        let now = DateTime.UtcNow
        
        let! rowsAffected =
            conn.ExecuteAsync(
                """
                UPDATE items
                SET name = @Name,
                    description = @Description,
                    updated_at = @UpdatedAt
                WHERE id = @Id
                """,
                {|
                    Id = item.Id
                    Name = item.Name
                    Description = item.Description
                    UpdatedAt = now.ToString("O")
                |}
            )
            |> Async.AwaitTask
        
        if rowsAffected = 0 then
            failwith $"Item {item.Id} not found"
    }

// ============================================
// Delete
// ============================================

let deleteItem (itemId: int) : Async<unit> =
    async {
        use conn = getConnection()
        
        let! rowsAffected =
            conn.ExecuteAsync(
                "DELETE FROM items WHERE id = @Id",
                {| Id = itemId |}
            )
            |> Async.AwaitTask
        
        if rowsAffected = 0 then
            failwith $"Item {itemId} not found"
    }

// ============================================
// Bulk Operations
// ============================================

let insertItems (items: Item list) : Async<int> =
    async {
        use conn = getConnection()
        conn.Open()
        
        use transaction = conn.BeginTransaction()
        
        try
            let now = DateTime.UtcNow
            
            let! count =
                conn.ExecuteAsync(
                    """
                    INSERT INTO items (name, description, created_at, updated_at)
                    VALUES (@Name, @Description, @CreatedAt, @UpdatedAt)
                    """,
                    items |> List.map (fun item ->
                        {|
                            Name = item.Name
                            Description = item.Description
                            CreatedAt = now.ToString("O")
                            UpdatedAt = now.ToString("O")
                        |}
                    ),
                    transaction
                )
                |> Async.AwaitTask
            
            transaction.Commit()
            return count
        with ex ->
            transaction.Rollback()
            return raise ex
    }
```

### Complex Queries

```fsharp
// ============================================
// Joins
// ============================================

type ItemWithUser = {
    ItemId: int
    ItemName: string
    UserId: int
    Username: string
}

let getItemsWithUsers () : Async<ItemWithUser list> =
    async {
        use conn = getConnection()
        
        let! results =
            conn.QueryAsync<ItemWithUser>("""
                SELECT 
                    i.id as ItemId,
                    i.name as ItemName,
                    u.id as UserId,
                    u.username as Username
                FROM items i
                INNER JOIN users u ON i.user_id = u.id
                ORDER BY i.created_at DESC
            """)
            |> Async.AwaitTask
        
        return results |> Seq.toList
    }

// ============================================
// Aggregations
// ============================================

type ItemStats = {
    TotalCount: int
    PublishedCount: int
    DraftCount: int
    AverageNameLength: float
}

let getItemStats () : Async<ItemStats> =
    async {
        use conn = getConnection()
        
        let! stats =
            conn.QueryFirstAsync<ItemStats>("""
                SELECT 
                    COUNT(*) as TotalCount,
                    SUM(CASE WHEN status = 'Published' THEN 1 ELSE 0 END) as PublishedCount,
                    SUM(CASE WHEN status = 'Draft' THEN 1 ELSE 0 END) as DraftCount,
                    AVG(LENGTH(name)) as AverageNameLength
                FROM items
            """)
            |> Async.AwaitTask
        
        return stats
    }

// ============================================
// Multi-Mapping (1-to-many relationships)
// ============================================

type ItemWithTags = {
    Item: Item
    Tags: string list
}

let getItemsWithTags () : Async<ItemWithTags list> =
    async {
        use conn = getConnection()
        
        let! results =
            conn.QueryAsync<Item, string, Item * string>(
                """
                SELECT 
                    i.id, i.name, i.description, i.created_at, i.updated_at,
                    t.name as TagName
                FROM items i
                LEFT JOIN item_tags it ON i.id = it.item_id
                LEFT JOIN tags t ON it.tag_id = t.id
                ORDER BY i.id
                """,
                (fun item tag -> (item, tag))
            )
            |> Async.AwaitTask
        
        // Group by item
        let grouped =
            results
            |> Seq.groupBy (fun (item, _) -> item.Id)
            |> Seq.map (fun (_, group) ->
                let item, _ = Seq.head group
                let tags = 
                    group 
                    |> Seq.map snd 
                    |> Seq.filter (not << String.IsNullOrEmpty)
                    |> Seq.toList
                { Item = item; Tags = tags }
            )
            |> Seq.toList
        
        return grouped
    }
```

## File-Based Persistence

### JSON Files

```fsharp
open System.IO
open System.Text.Json

module JsonFile =
    
    let private options = JsonSerializerOptions(WriteIndented = true)
    
    // ============================================
    // Read
    // ============================================
    
    let read<'T> (filePath: string) : Async<'T option> =
        async {
            try
                if File.Exists filePath then
                    let! json = File.ReadAllTextAsync filePath |> Async.AwaitTask
                    let data = JsonSerializer.Deserialize<'T>(json, options)
                    return Some data
                else
                    return None
            with ex ->
                printfn $"Error reading {filePath}: {ex.Message}"
                return None
        }
    
    // ============================================
    // Write
    // ============================================
    
    let write<'T> (filePath: string) (data: 'T) : Async<unit> =
        async {
            try
                let dir = Path.GetDirectoryName filePath
                if not (Directory.Exists dir) then
                    Directory.CreateDirectory dir |> ignore
                
                let json = JsonSerializer.Serialize(data, options)
                do! File.WriteAllTextAsync(filePath, json) |> Async.AwaitTask
            with ex ->
                printfn $"Error writing {filePath}: {ex.Message}"
                return raise ex
        }
    
    // ============================================
    // Update (read-modify-write)
    // ============================================
    
    let update<'T> (filePath: string) (defaultValue: 'T) (f: 'T -> 'T) : Async<unit> =
        async {
            let! current = read<'T> filePath
            let updated = 
                current 
                |> Option.defaultValue defaultValue
                |> f
            do! write filePath updated
        }

// ============================================
// Usage Examples
// ============================================

// Configuration
type AppConfig = {
    AppName: string
    MaxItems: int
    EnableDebug: bool
}

let loadConfig () : Async<AppConfig> =
    async {
        let configPath = "./data/config.json"
        let! config = JsonFile.read<AppConfig> configPath
        return config |> Option.defaultValue {
            AppName = "My App"
            MaxItems = 100
            EnableDebug = false
        }
    }

let saveConfig (config: AppConfig) : Async<unit> =
    JsonFile.write "./data/config.json" config

// Cache
type CachedData = {
    LastUpdated: DateTime
    Data: Item list
}

let cacheItems (items: Item list) : Async<unit> =
    async {
        let cached = {
            LastUpdated = DateTime.UtcNow
            Data = items
        }
        do! JsonFile.write "./data/cache/items.json" cached
    }

let getCachedItems (maxAge: TimeSpan) : Async<Item list option> =
    async {
        let! cached = JsonFile.read<CachedData> "./data/cache/items.json"
        return
            cached
            |> Option.bind (fun c ->
                if DateTime.UtcNow - c.LastUpdated < maxAge then
                    Some c.Data
                else
                    None
            )
    }
```

### Event Sourcing (Append-Only Files)

```fsharp
module EventStore =
    
    open System.IO
    open System.Text.Json
    open Shared.Domain
    
    let private eventsDir = "./data/events"
    
    let ensureEventsDir () =
        if not (Directory.Exists eventsDir) then
            Directory.CreateDirectory eventsDir |> ignore
    
    // ============================================
    // Append Event
    // ============================================
    
    let appendEvent<'T> (streamName: string) (event: 'T) : Async<unit> =
        async {
            ensureEventsDir()
            
            let filePath = Path.Combine(eventsDir, $"{streamName}.jsonl")
            
            let eventRecord = {|
                Timestamp = DateTime.UtcNow
                Data = event
            |}
            
            let json = JsonSerializer.Serialize eventRecord
            
            // Append to file (thread-safe with lock)
            lock eventsDir (fun () ->
                File.AppendAllLines(filePath, [json])
            )
    }
    
    // ============================================
    // Read Events
    // ============================================
    
    let readEvents<'T> (streamName: string) : Async<Event<'T> list> =
        async {
            ensureEventsDir()
            
            let filePath = Path.Combine(eventsDir, $"{streamName}.jsonl")
            
            if not (File.Exists filePath) then
                return []
            else
                let! lines = File.ReadAllLinesAsync filePath |> Async.AwaitTask
                
                return
                    lines
                    |> Array.choose (fun line ->
                        try
                            let record = JsonSerializer.Deserialize<{| Timestamp: DateTime; Data: 'T |}>(line)
                            Some {
                                Id = Guid.NewGuid()
                                Timestamp = record.Timestamp
                                Data = record.Data
                            }
                        with _ ->
                            None
                    )
                    |> Array.toList
        }
    
    // ============================================
    // Replay Events to Build State
    // ============================================
    
    let replayEvents<'State, 'Event> 
        (streamName: string) 
        (initialState: 'State) 
        (apply: 'State -> 'Event -> 'State) : Async<'State> =
        async {
            let! events = readEvents<'Event> streamName
            
            return
                events
                |> List.fold (fun state event -> apply state event.Data) initialState
        }

// ============================================
// Usage Example
// ============================================

type ItemEvent =
    | ItemCreated of Item
    | ItemUpdated of Item
    | ItemDeleted of itemId: int

let applyItemEvent (items: Item list) (event: ItemEvent) : Item list =
    match event with
    | ItemCreated item ->
        item :: items
    
    | ItemUpdated item ->
        items |> List.map (fun i ->
            if i.Id = item.Id then item else i
        )
    
    | ItemDeleted itemId ->
        items |> List.filter (fun i -> i.Id <> itemId)

// Log event
let logItemCreated (item: Item) : Async<unit> =
    EventStore.appendEvent "items" (ItemCreated item)

// Rebuild state from events
let rebuildItemsFromEvents () : Async<Item list> =
    EventStore.replayEvents "items" [] applyItemEvent
```

### Text/CSV Files

```fsharp
module TextFile =
    
    // ============================================
    // Simple Text Storage
    // ============================================
    
    let writeLines (filePath: string) (lines: string list) : Async<unit> =
        async {
            let dir = Path.GetDirectoryName filePath
            if not (Directory.Exists dir) then
                Directory.CreateDirectory dir |> ignore
            
            do! File.WriteAllLinesAsync(filePath, lines) |> Async.AwaitTask
        }
    
    let readLines (filePath: string) : Async<string list> =
        async {
            if File.Exists filePath then
                let! lines = File.ReadAllLinesAsync filePath |> Async.AwaitTask
                return lines |> Array.toList
            else
                return []
        }
    
    // ============================================
    // CSV Export
    // ============================================
    
    let exportToCsv (filePath: string) (items: Item list) : Async<unit> =
        async {
            let header = "Id,Name,Description,CreatedAt,UpdatedAt"
            let rows =
                items
                |> List.map (fun item ->
                    let escapeCsv (s: string) =
                        if s.Contains(",") || s.Contains("\"") || s.Contains("\n") then
                            $"\"{s.Replace("\"", "\"\"")}\""
                        else
                            s
                    
                    $"{item.Id},{escapeCsv item.Name},{escapeCsv item.Description},{item.CreatedAt},{item.UpdatedAt}"
                )
            
            do! writeLines filePath (header :: rows)
        }
```

## Migration Patterns

### Version-Based Migrations

```fsharp
module Migrations =
    
    let private versionFile = "./data/.db_version"
    
    let getCurrentVersion () : int =
        if File.Exists versionFile then
            File.ReadAllText versionFile |> int
        else
            0
    
    let setVersion (version: int) : unit =
        File.WriteAllText(versionFile, string version)
    
    let private migrations = [
        // Version 1
        (fun (conn: SqliteConnection) ->
            use cmd = new SqliteCommand("""
                CREATE TABLE items (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL
                )
            """, conn)
            cmd.ExecuteNonQuery() |> ignore
        )
        
        // Version 2
        (fun (conn: SqliteConnection) ->
            use cmd = new SqliteCommand("""
                ALTER TABLE items ADD COLUMN description TEXT DEFAULT '';
            """, conn)
            cmd.ExecuteNonQuery() |> ignore
        )
        
        // Version 3
        (fun (conn: SqliteConnection) ->
            use cmd = new SqliteCommand("""
                CREATE INDEX idx_items_name ON items(name);
            """, conn)
            cmd.ExecuteNonQuery() |> ignore
        )
    ]
    
    let runMigrations () =
        let currentVersion = getCurrentVersion()
        let targetVersion = migrations.Length
        
        if currentVersion < targetVersion then
            printfn $"Running migrations from v{currentVersion} to v{targetVersion}..."
            
            use conn = getConnection()
            conn.Open()
            
            for version in (currentVersion + 1) .. targetVersion do
                printfn $"Applying migration v{version}..."
                let migration = migrations.[version - 1]
                migration conn
                setVersion version
            
            printfn "Migrations complete!"
```

## Backup Strategies

```fsharp
module Backup =
    
    open System.IO.Compression
    
    let backupDatabase (outputPath: string) : Async<unit> =
        async {
            // Close connections first
            SqliteConnection.ClearAllPools()
            
            // Copy database file
            let dbPath = "./data/app.db"
            let timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
            let backupPath = $"{outputPath}/backup_{timestamp}.db"
            
            File.Copy(dbPath, backupPath, overwrite = true)
            
            // Compress
            use archive = ZipFile.Open($"{backupPath}.zip", ZipArchiveMode.Create)
            archive.CreateEntryFromFile(backupPath, Path.GetFileName(backupPath))
            
            // Delete uncompressed
            File.Delete backupPath
            
            printfn $"Backup created: {backupPath}.zip"
        }
    
    let restoreDatabase (backupPath: string) : Async<unit> =
        async {
            // Extract
            use archive = ZipFile.OpenRead backupPath
            let entry = archive.Entries.[0]
            
            let tempPath = "./data/temp_restore.db"
            entry.ExtractToFile(tempPath, overwrite = true)
            
            // Close connections
            SqliteConnection.ClearAllPools()
            
            // Replace database
            let dbPath = "./data/app.db"
            File.Delete dbPath
            File.Move(tempPath, dbPath)
            
            printfn "Database restored successfully"
        }
```

## Best Practices

1. **Use WAL mode** for SQLite (better concurrency)
2. **Connection pooling** is handled by ADO.NET automatically
3. **Use transactions** for multi-step operations
4. **Index frequently queried columns**
5. **Parameterize all queries** (prevent SQL injection)
6. **Log errors** but don't expose sensitive data
7. **Backup regularly** (daily for production)
8. **Test migrations** on copy of production data
9. **Use async/await** for all I/O operations
10. **Validate before persisting** (use Validation module)

## Next Steps

- Read `06-TESTING.md` for persistence testing strategies
- Read `07-BUILD-DEPLOY.md` for deployment configuration
- Consider volume mounts for data persistence in Docker
