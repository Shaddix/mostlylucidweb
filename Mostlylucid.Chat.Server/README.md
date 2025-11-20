# Chat Server - SignalR Hub for Real-Time Chat

A minimal .NET SignalR server for managing real-time chat conversations between website visitors and administrators.

## Features

- 🔌 **SignalR Hub** - Real-time bidirectional communication
- 💾 **In-Memory Storage** - Simple deployment (easily replaceable with database)
- 🔄 **Connection Tracking** - Manage user and admin connections
- 📊 **Conversation Management** - Track multiple conversations
- 🌐 **CORS Enabled** - Embed on any website
- 📝 **Typing Indicators** - Real-time typing status
- ✅ **Read Receipts** - Track message read status

## Quick Start

### 1. Run the Server

```bash
cd Mostlylucid.Chat.Server
dotnet run
```

The server starts on `http://localhost:5100` by default.

### 2. Configuration

Edit `appsettings.json`:

```json
{
  "Urls": "http://localhost:5100",
  "AllowedOrigins": [
    "http://localhost:3000",
    "https://yourdomain.com",
    "*"
  ]
}
```

**Important**: For production, specify exact origins instead of `"*"` for security.

## API Endpoints

### Health Check

```
GET /health
```

Returns server health status.

### Widget Script

```
GET /widget.js
```

Serves the embeddable widget JavaScript file.

## SignalR Hub Methods

### Client → Server Methods

#### RegisterUser
Register a new user connection.

```javascript
await connection.invoke('RegisterUser', userName, email, sourceUrl);
```

**Parameters:**
- `userName` (string) - User's display name
- `email` (string) - User's email (optional)
- `sourceUrl` (string) - URL where the chat was initiated

#### RegisterAdmin
Register an admin connection.

```javascript
await connection.invoke('RegisterAdmin', adminName);
```

**Parameters:**
- `adminName` (string) - Admin's display name

#### SendMessage
Send a message.

```javascript
await connection.invoke('SendMessage', content);
```

**Parameters:**
- `content` (string) - Message content

#### JoinConversation
Admin joins a specific conversation.

```javascript
await connection.invoke('JoinConversation', conversationId);
```

**Parameters:**
- `conversationId` (string) - ID of the conversation to join

#### MarkAsRead
Mark messages as read.

```javascript
await connection.invoke('MarkAsRead', conversationId);
```

**Parameters:**
- `conversationId` (string) - ID of the conversation

#### UserTyping
Send typing indicator.

```javascript
await connection.invoke('UserTyping', isTyping);
```

**Parameters:**
- `isTyping` (boolean) - Whether user is typing

### Server → Client Events

#### ConversationHistory
Receive conversation message history.

```javascript
connection.on('ConversationHistory', (messages) => {
  // messages: ChatMessage[]
});
```

#### ActiveConversations
Receive list of active conversations (admin only).

```javascript
connection.on('ActiveConversations', (conversations) => {
  // conversations: Conversation[]
});
```

#### NewUserConnected
Notification when a new user connects (admin only).

```javascript
connection.on('NewUserConnected', (connectionInfo, conversation) => {
  // connectionInfo: ConnectionInfo
  // conversation: Conversation
});
```

#### UserMessage
Receive a message from a user (admin only).

```javascript
connection.on('UserMessage', (message) => {
  // message: ChatMessage
});
```

#### AdminMessage
Receive a message from admin (user only).

```javascript
connection.on('AdminMessage', (message) => {
  // message: ChatMessage
});
```

#### MessageReceived
Confirmation that message was received.

```javascript
connection.on('MessageReceived', (message) => {
  // message: ChatMessage
});
```

#### UserTyping
User is typing indicator (admin only).

```javascript
connection.on('UserTyping', (conversationId, isTyping) => {
  // conversationId: string
  // isTyping: boolean
});
```

#### AdminTyping
Admin is typing indicator (user only).

```javascript
connection.on('AdminTyping', (isTyping) => {
  // isTyping: boolean
});
```

#### UserDisconnected
User disconnected notification (admin only).

```javascript
connection.on('UserDisconnected', (connectionInfo) => {
  // connectionInfo: ConnectionInfo
});
```

## Data Models

### ChatMessage

```csharp
public class ChatMessage
{
    public string Id { get; set; }
    public string ConversationId { get; set; }
    public string SenderId { get; set; }
    public string SenderName { get; set; }
    public string SenderType { get; set; } // "user" or "admin"
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsRead { get; set; }
}
```

### Conversation

```csharp
public class Conversation
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string UserEmail { get; set; }
    public string SourceUrl { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public bool IsActive { get; set; }
    public int UnreadCount { get; set; }
    public List<ChatMessage> Messages { get; set; }
}
```

### ConnectionInfo

```csharp
public class ConnectionInfo
{
    public string ConnectionId { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string ConnectionType { get; set; } // "user" or "admin"
    public DateTime ConnectedAt { get; set; }
    public string? ConversationId { get; set; }
}
```

## Deployment

### Docker

Create a `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Mostlylucid.Chat.Server/Mostlylucid.Chat.Server.csproj", "Mostlylucid.Chat.Server/"]
COPY ["Mostlylucid.Chat.Shared/Mostlylucid.Chat.Shared.csproj", "Mostlylucid.Chat.Shared/"]
RUN dotnet restore "Mostlylucid.Chat.Server/Mostlylucid.Chat.Server.csproj"
COPY . .
WORKDIR "/src/Mostlylucid.Chat.Server"
RUN dotnet build "Mostlylucid.Chat.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Mostlylucid.Chat.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Mostlylucid.Chat.Server.dll"]
```

Build and run:

```bash
docker build -t chat-server .
docker run -p 5100:80 chat-server
```

### Production Considerations

1. **Replace In-Memory Storage**: Implement `IConversationService` with database backing (PostgreSQL, MongoDB, etc.)

2. **Add Authentication**: Secure admin endpoints with authentication

3. **Configure CORS**: Set specific allowed origins in production

4. **Add Logging**: Configure Serilog or other logging providers

5. **Enable HTTPS**: Use SSL certificates for production

6. **Scale with Redis**: Use Redis backplane for multiple server instances

```csharp
builder.Services.AddSignalR()
    .AddStackExchangeRedis(configuration.GetConnectionString("Redis"));
```

## Architecture

### Services

- **IConversationService** - Manages conversations and messages
- **IConnectionTracker** - Tracks active connections

### Implementations

- **InMemoryConversationService** - Simple in-memory storage
- **InMemoryConnectionTracker** - In-memory connection tracking

For production, create database-backed implementations.

## License

MIT
