# Chat Server - New Features

## SQLite Database Persistence

The chat server now uses SQLite for data persistence, making it easy to deploy without external database dependencies.

### Database Features

- **Automatic Schema Creation**: Database is created automatically on first run
- **Three Main Tables**:
  - `Conversations`: User conversations with metadata
  - `Messages`: All chat messages with timestamps and read status
  - `Presence`: Real-time presence tracking for users and admins

### Database Location

By default, the database is created as `chat.db` in the application root directory.

Configure the location in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "ChatDatabase": "Data Source=/path/to/your/chat.db"
  }
}
```

### Migrations

The application uses `EnsureCreated()` for simplicity. For production with schema changes, consider using EF Core migrations:

```bash
# Add migration
dotnet ef migrations add MigrationName --project Mostlylucid.Chat.Server

# Apply migration
dotnet ef database update --project Mostlylucid.Chat.Server
```

## API Key Authentication

Simple API key-based authentication protects admin endpoints and admin SignalR connections.

### Configuration

Set your API key in `appsettings.json`:

```json
{
  "Chat": {
    "AdminApiKey": "your-secure-api-key-here"
  }
}
```

**Security Best Practices**:
- Use a strong, randomly generated API key
- Store production keys in environment variables or Azure Key Vault
- Never commit API keys to source control
- Rotate keys regularly

### Usage

#### For Windows Tray App

Set the API key in `MainWindow.xaml.cs`:

```csharp
private readonly string _apiKey = "your-api-key-here"; // Load from config
```

The key is automatically appended to the SignalR connection URL.

#### For Custom Admin Clients

Add the API key to your SignalR connection:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl('http://localhost:5100/chathub?apiKey=your-api-key-here')
    .build();
```

#### For HTTP Endpoints

Protected endpoints (like `/admin/stats`) require the API key in headers:

```bash
curl http://localhost:5100/admin/stats \
  -H "X-Api-Key: your-api-key-here"
```

### Protected Resources

- **SignalR Hub Methods**: `RegisterAdmin` requires API key validation
- **HTTP Endpoints**: All `/admin/*` endpoints require API key
- **User Connections**: Regular users do NOT need an API key

## Presence Tracking

Real-time presence indicators show who's online (users and admins).

### Features

- **Admin Online Status**: Users see if admins are available
- **User Count**: Admins see how many users are online
- **Real-Time Updates**: Presence updates broadcast automatically
- **Persistent Storage**: Presence data stored in SQLite with last-seen timestamps

### Database Schema

```sql
CREATE TABLE Presence (
    UserId TEXT PRIMARY KEY,
    UserName TEXT NOT NULL,
    UserType TEXT NOT NULL, -- 'user' or 'admin'
    IsOnline INTEGER NOT NULL, -- 1 for online, 0 for offline
    LastSeen TEXT NOT NULL, -- ISO 8601 datetime
    ConnectionId TEXT
);
```

### SignalR Events

#### Server → Client

**PresenceUpdate**: Broadcast to all connected clients when presence changes

```javascript
connection.on('PresenceUpdate', (data) => {
    // For users
    console.log('Admin online:', data.adminOnline); // boolean

    // For admins
    console.log('Online users:', data.onlineUserCount); // number
    console.log('Online admins:', data.onlineAdminCount); // number
});
```

### Widget Integration

The web widget displays admin availability in the header:

- **Green pulsing dot + "Available"**: At least one admin is online
- **Gray dot + "Away"**: No admins currently online

### Tray App Integration

The Windows tray app displays online user count:

- Header shows: "Connected • X users online"
- Updates in real-time as users connect/disconnect

### Presence Logic

1. **Connection**: User/admin comes online
   - `SetUserOnline()` called
   - `PresenceUpdate` broadcast to all clients

2. **Disconnection**: User/admin goes offline
   - `SetUserOffline()` called
   - `LastSeen` timestamp updated
   - `PresenceUpdate` broadcast to all clients

3. **Reconnection**: Existing user reconnects
   - Presence record updated with new `ConnectionId`
   - `LastSeen` refreshed

### Querying Presence

The service provides several query methods:

```csharp
// Check if any admin is online
bool isOnline = await _presenceService.IsAnyAdminOnline();

// Get counts
int userCount = await _presenceService.GetOnlineUserCount();
int adminCount = await _presenceService.GetOnlineAdminCount();

// Update last seen
await _presenceService.UpdateLastSeen(userId);
```

## Admin Stats Endpoint

Protected endpoint for monitoring chat system statistics:

```bash
GET /admin/stats

Headers:
  X-Api-Key: your-api-key-here

Response:
{
  "totalConversations": 42,
  "activeConversations": 5,
  "totalMessages": 328,
  "onlineUsers": 3,
  "onlineAdmins": 1
}
```

## Educational Code Patterns

This project demonstrates several .NET patterns for learning:

### 1. Entity Framework Core with SQLite

**Location**: `Data/ChatDbContext.cs`

- DbContext setup
- Entity configuration with `ModelBuilder`
- Navigation properties
- Indexes for performance

### 2. Repository Pattern

**Location**: `Services/SqliteConversationService.cs`

- Interface segregation (`IConversationService`)
- Dependency injection
- Async/await patterns
- LINQ queries with EF Core

### 3. Middleware

**Location**: `Middleware/ApiKeyAuthMiddleware.cs`

- Custom middleware implementation
- Request pipeline configuration
- Header and query string validation

### 4. SignalR Hub Filters

**Location**: `Hubs/ApiKeyAuthorizationFilter.cs`

- `IHubFilter` implementation
- Authorization logic
- Exception handling in hubs

### 5. Minimal APIs

**Location**: `Program.cs`

- Modern .NET hosting model
- Service registration
- Endpoint mapping
- Database initialization on startup

### 6. Scoped vs Singleton Services

**Program.cs** demonstrates when to use each:

```csharp
// Scoped: New instance per request/hub invocation
builder.Services.AddScoped<IConversationService, SqliteConversationService>();

// Singleton: Single instance for application lifetime
builder.Services.AddSingleton<IConnectionTracker, InMemoryConnectionTracker>();
```

## Security Considerations

### For Learning/Demo

The current implementation is suitable for:
- Local development
- Learning .NET concepts
- Internal tools
- Trusted network deployments

### For Production

Consider these enhancements:

1. **Authentication**:
   - Replace API keys with OAuth2/JWT
   - Use ASP.NET Core Identity
   - Implement role-based access control

2. **Database**:
   - Use connection pooling
   - Implement proper migrations
   - Add database backups
   - Consider PostgreSQL/SQL Server for production scale

3. **Secrets Management**:
   - Use Azure Key Vault
   - Environment variables
   - User Secrets for development

4. **HTTPS**:
   - Always use HTTPS in production
   - Configure proper SSL certificates
   - Use Caddy/nginx for SSL termination

5. **Rate Limiting**:
   - Protect against abuse
   - Use `AspNetCoreRateLimit` package

## Deployment

### Local Development

```bash
cd Mostlylucid.Chat.Server
dotnet run
```

Database will be created automatically.

### Docker

```bash
docker build -t chat-server .
docker run -d \
  -p 5100:80 \
  -v $(pwd)/data:/app/data \
  -e Chat__AdminApiKey=your-secure-key \
  -e ConnectionStrings__ChatDatabase="Data Source=/app/data/chat.db" \
  chat-server
```

### Behind Caddy (Recommended)

Caddyfile:

```
chat.yourdomain.com {
    reverse_proxy localhost:5100
}
```

Caddy automatically handles:
- HTTPS certificates (Let's Encrypt)
- WebSocket upgrades (for SignalR)
- HTTP/2

## Troubleshooting

### Database Locked

If you see "database is locked" errors:

```csharp
// Update connection string with timeout
"Data Source=chat.db;Cache=Shared;Mode=ReadWriteCreate;Timeout=30"
```

### API Key Not Working

1. Check `appsettings.json` has the key configured
2. Ensure key matches in client (tray app)
3. Check browser console for WebSocket errors
4. Verify middleware is registered: `app.UseApiKeyAuth();`

### Presence Not Updating

1. Check database write permissions
2. Verify `PresenceUpdate` event handler exists in client
3. Check browser console for JavaScript errors
4. Ensure SignalR connection is established

## License

MIT
