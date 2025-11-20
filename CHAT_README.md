# Chat with Scott - Real-Time Chat System

A complete real-time chat system for engaging with website visitors. Includes an embeddable JavaScript widget, SignalR server, and Windows notification tray application.

## 🎯 Overview

This system enables real-time chat communication between website visitors and administrators. It consists of three main components:

1. **Chat Widget** - Embeddable JavaScript widget for websites
2. **Chat Server** - SignalR hub for real-time communication
3. **Tray Application** - Windows desktop app for administrators

## 🚀 Quick Start

### 1. Start the Chat Server

```bash
cd Mostlylucid.Chat.Server
dotnet run
```

Server runs on `http://localhost:5100`

### 2. Build the Widget

```bash
cd Mostlylucid.Chat.Widget
npm install
npm run build
```

Copy `dist/widget.js` to `Mostlylucid.Chat.Server/wwwroot/`

### 3. Run the Tray App (Windows only)

```bash
cd Mostlylucid.Chat.TrayApp
dotnet run
```

### 4. Embed Widget on Your Website

```html
<script
  src="http://localhost:5100/widget.js"
  data-chat-widget
  data-hub-url="http://localhost:5100/chathub"
></script>
```

## 📁 Project Structure

```
Mostlylucid.Chat/
├── Mostlylucid.Chat.Server/         # SignalR hub server
│   ├── Hubs/
│   │   └── ChatHub.cs               # Main SignalR hub
│   ├── Services/
│   │   ├── IConversationService.cs
│   │   ├── InMemoryConversationService.cs
│   │   ├── IConnectionTracker.cs
│   │   └── InMemoryConnectionTracker.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── Dockerfile
│
├── Mostlylucid.Chat.Shared/         # Shared models
│   └── Models/
│       ├── ChatMessage.cs
│       ├── Conversation.cs
│       └── ConnectionInfo.cs
│
├── Mostlylucid.Chat.Widget/         # Embeddable widget
│   ├── src/
│   │   ├── js/
│   │   │   └── widget.js            # Main widget logic
│   │   └── css/
│   │       └── widget.css           # Widget styles
│   ├── examples/
│   │   ├── index.html               # Full demo
│   │   ├── simple.html              # Simple integration
│   │   └── manual.html              # Manual initialization
│   ├── dist/
│   │   └── widget.js                # Compiled widget
│   ├── package.json
│   └── webpack.config.js
│
└── Mostlylucid.Chat.TrayApp/        # Windows tray app
    ├── MainWindow.xaml              # Main UI
    ├── MainWindow.xaml.cs           # UI logic
    ├── App.xaml
    ├── App.xaml.cs
    ├── Converters.cs
    └── Resources/
        └── Styles.xaml
```

## 🎨 Features

### Chat Widget
- ✅ Single-line integration
- ✅ Beautiful, responsive UI
- ✅ Real-time messaging
- ✅ Browser notifications
- ✅ Typing indicators
- ✅ Message history
- ✅ Persistent sessions
- ✅ Mobile-friendly

### Chat Server
- ✅ SignalR hub
- ✅ Connection tracking
- ✅ Conversation management
- ✅ In-memory storage (easily replaceable)
- ✅ CORS support
- ✅ Health check endpoint
- ✅ Docker support

### Tray Application
- ✅ Windows system tray
- ✅ Multi-conversation support
- ✅ Toast notifications
- ✅ Read status tracking
- ✅ Typing indicators
- ✅ Auto-reconnect
- ✅ Modern WPF UI

## 🔧 Configuration

### Server Configuration

Edit `Mostlylucid.Chat.Server/appsettings.json`:

```json
{
  "Urls": "http://localhost:5100",
  "AllowedOrigins": [
    "http://localhost:3000",
    "https://yourdomain.com"
  ]
}
```

### Widget Configuration

```html
<!-- Auto-init with data attributes -->
<script
  src="http://localhost:5100/widget.js"
  data-chat-widget
  data-hub-url="http://localhost:5100/chathub"
  data-user-name="John Doe"
  data-user-email="john@example.com"
></script>

<!-- Manual initialization -->
<script src="http://localhost:5100/widget.js"></script>
<script>
  new ChatWidget({
    hubUrl: 'http://localhost:5100/chathub',
    userName: 'John Doe',
    userEmail: 'john@example.com'
  });
</script>
```

### Tray App Configuration

Edit `Mostlylucid.Chat.TrayApp/MainWindow.xaml.cs`:

```csharp
private readonly string _hubUrl = "http://localhost:5100/chathub";
private readonly string _adminName = "Scott";
```

## 🐳 Docker Deployment

### Using Docker Compose

```bash
# Build widget first
cd Mostlylucid.Chat.Widget
npm install && npm run build

# Copy widget to server wwwroot
cp dist/widget.js ../Mostlylucid.Chat.Server/wwwroot/

# Run with Docker Compose
cd ..
docker-compose -f docker-compose.chat.yml up -d
```

The server will be available at `http://localhost:5100`

### Manual Docker Build

```bash
# Build image
docker build -t chat-server -f Mostlylucid.Chat.Server/Dockerfile .

# Run container
docker run -d \
  -p 5100:80 \
  -e AllowedOrigins__0=* \
  -v $(pwd)/Mostlylucid.Chat.Widget/dist:/app/wwwroot:ro \
  chat-server
```

## 📖 Usage Examples

### Basic Integration

The simplest way to add the chat widget:

```html
<!DOCTYPE html>
<html>
<head>
    <title>My Website</title>
</head>
<body>
    <h1>Welcome to my website</h1>

    <!-- Chat widget -->
    <script
      src="http://localhost:5100/widget.js"
      data-chat-widget
      data-hub-url="http://localhost:5100/chathub"
    ></script>
</body>
</html>
```

### With User Information

Pre-fill user information if available:

```html
<script
  src="http://localhost:5100/widget.js"
  data-chat-widget
  data-hub-url="http://localhost:5100/chathub"
  data-user-name="<?= $user->name ?>"
  data-user-email="<?= $user->email ?>"
></script>
```

### Programmatic Control

Control the widget with JavaScript:

```html
<script src="http://localhost:5100/widget.js"></script>
<script>
  const widget = new ChatWidget({
    hubUrl: 'http://localhost:5100/chathub'
  });

  // Open chat programmatically
  setTimeout(() => {
    const component = Alpine.$data(document.querySelector('.chat-widget'));
    component.toggleChat();
  }, 2000);
</script>
```

## 🔐 Production Considerations

### Security

1. **CORS**: Configure specific allowed origins

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://yourdomain.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
```

2. **Authentication**: Add admin authentication

```csharp
builder.Services.AddAuthentication(...)
    .AddCookie(...);

// In ChatHub
[Authorize(Roles = "Admin")]
public async Task RegisterAdmin(string adminName) { ... }
```

3. **Rate Limiting**: Prevent abuse

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("messages", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
    });
});
```

### Persistence

Replace in-memory services with database implementations:

```csharp
// PostgreSQL example
public class PostgresConversationService : IConversationService
{
    private readonly ChatDbContext _context;

    public PostgresConversationService(ChatDbContext context)
    {
        _context = context;
    }

    public async Task<Conversation> CreateOrGetConversation(...)
    {
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (conversation == null)
        {
            conversation = new Conversation { ... };
            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();
        }

        return conversation;
    }

    // ... other methods
}

// Register in Program.cs
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IConversationService, PostgresConversationService>();
```

### Scaling

For multiple server instances, use Redis backplane:

```bash
dotnet add package Microsoft.AspNetCore.SignalR.StackExchangeRedis
```

```csharp
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis"), options =>
    {
        options.Configuration.ChannelPrefix = "chat";
    });
```

### Monitoring

Add logging and metrics:

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddSeq(builder.Configuration.GetSection("Seq"));
});

// In ChatHub
private readonly ILogger<ChatHub> _logger;

public async Task SendMessage(string content)
{
    _logger.LogInformation("Message sent by {UserId}", Context.ConnectionId);
    // ...
}
```

## 🧪 Testing

### Test the Widget

Open `Mostlylucid.Chat.Widget/examples/index.html` in a browser.

### Test the Connection

```javascript
// Browser console
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5100/chathub")
    .build();

connection.start().then(() => {
    console.log("Connected!");
    connection.invoke("RegisterUser", "Test User", "test@example.com", window.location.href);
});

connection.on("ConversationHistory", (messages) => {
    console.log("Messages:", messages);
});
```

## 📚 API Documentation

See component READMEs for detailed API documentation:

- [Server API](Mostlylucid.Chat.Server/README.md)
- [Widget API](Mostlylucid.Chat.Widget/README.md)
- [Tray App Guide](Mostlylucid.Chat.TrayApp/README.md)

## 🐛 Troubleshooting

### Widget doesn't appear

1. Check browser console for errors
2. Verify server is running: `http://localhost:5100/health`
3. Check CORS configuration

### Connection fails

1. Verify `data-hub-url` is correct
2. Check firewall settings
3. Ensure SignalR is properly configured

### Tray app can't connect

1. Check hub URL in code
2. Verify server is accessible from Windows machine
3. Check Windows Firewall

## 📄 License

MIT License - see LICENSE file for details

## 🤝 Contributing

Contributions welcome! Please open an issue or PR.

## 📞 Support

For issues and questions, please open a GitHub issue.

---

Built with ❤️ using .NET 9, SignalR, Alpine.js, and WPF
