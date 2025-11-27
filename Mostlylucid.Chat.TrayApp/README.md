# Chat Tray App - Windows Notification Tray Application

A Windows desktop application that runs in the system tray, allowing administrators to respond to website visitor chats in real-time.

## Features

- 🪟 **System Tray** - Runs minimized in Windows notification area
- 💬 **Multi-Conversation** - Handle multiple chats simultaneously
- 🔔 **Toast Notifications** - Get notified of new messages
- 📊 **Conversation List** - See all active conversations at a glance
- ✅ **Read Status** - Track unread message counts
- ⌨️ **Typing Indicators** - See when users are typing
- 🔄 **Auto-Reconnect** - Automatically reconnects if connection is lost

## Requirements

- Windows 10 or later
- .NET 9.0 Runtime

## Quick Start

### 1. Build the Application

```bash
cd Mostlylucid.Chat.TrayApp
dotnet build -c Release
```

### 2. Run the Application

```bash
dotnet run
```

Or run the compiled executable from `bin/Release/net9.0-windows/`.

### 3. Publish as Single Executable

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

This creates a single `.exe` file in `bin/Release/net9.0-windows/win-x64/publish/`.

## Configuration

Edit the hub URL in `MainWindow.xaml.cs`:

```csharp
private readonly string _hubUrl = "http://localhost:5100/chathub";
private readonly string _adminName = "Scott";
```

For production, move these to a configuration file:

```csharp
private readonly string _hubUrl = ConfigurationManager.AppSettings["HubUrl"];
private readonly string _adminName = ConfigurationManager.AppSettings["AdminName"];
```

## Usage

### Starting the Application

1. Run the executable
2. The app minimizes to the system tray (notification area)
3. Look for the chat icon in the system tray

### Responding to Chats

1. **New Chat Notification**: Toast notification appears when a user starts a chat
2. **Open App**: Click the tray icon to open the main window
3. **Select Conversation**: Click a conversation in the left panel
4. **Send Message**: Type your response and press Enter or click Send

### System Tray Menu

Right-click the tray icon:
- **Open** - Show the main window
- **Exit** - Close the application

### Keyboard Shortcuts

- **Enter** - Send message
- **Minimize** - Close window (app stays in tray)

## Features in Detail

### Conversation List

The left panel shows all active conversations with:
- User name
- User email
- Last message time
- Unread message count (red badge)

Conversations are sorted by most recent activity.

### Chat Interface

The right panel shows:
- Conversation details (name, email, source URL)
- Message history
- Real-time typing indicators
- Message input field

### Notifications

The app shows Windows toast notifications for:
- New conversations
- New messages from users

### Connection Status

The header shows connection status:
- **Connected** - Normal operation
- **Reconnecting...** - Temporarily disconnected
- **Disconnected** - Connection lost

The app automatically attempts to reconnect.

## Customization

### Change UI Colors

Edit `Resources/Styles.xaml` and `MainWindow.xaml`:

```xaml
<!-- Primary color (currently purple gradient) -->
<Border Background="#667eea">

<!-- User message color -->
<Border Background="#667eea">

<!-- Admin message color -->
<Border Background="#10B981">
```

### Change Window Size

In `MainWindow.xaml`:

```xaml
<Window ... Height="700" Width="1000">
```

### Add Sound Notifications

In `MainWindow.xaml.cs`, add to the `ShowNotification` method:

```csharp
private void ShowNotification(string message)
{
    _trayIcon?.ShowBalloonTip("Chat Notification", message, BalloonIcon.Info);

    // Play sound
    System.Media.SystemSounds.Beep.Play();
}
```

## Building for Distribution

### Create Installer

Use a tool like **Inno Setup** or **WiX Toolset** to create an installer.

Example Inno Setup script (`setup.iss`):

```iss
[Setup]
AppName=Chat Tray App
AppVersion=1.0
DefaultDirName={pf}\ChatTrayApp
DefaultGroupName=Chat Tray App
OutputDir=installer
OutputBaseFilename=ChatTrayApp-Setup

[Files]
Source: "bin\Release\net9.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs

[Icons]
Name: "{group}\Chat Tray App"; Filename: "{app}\Mostlylucid.Chat.TrayApp.exe"
Name: "{commonstartup}\Chat Tray App"; Filename: "{app}\Mostlylucid.Chat.TrayApp.exe"
```

Build with:

```bash
iscc setup.iss
```

### Auto-Start on Windows Login

To make the app start automatically:

1. **Via Installer**: Include in `{commonstartup}` (shown above)
2. **Manually**: Copy shortcut to `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`
3. **Registry**: Add to `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`

## Troubleshooting

### Connection Issues

**Problem**: App shows "Disconnected"

**Solutions**:
1. Check that the chat server is running
2. Verify the `_hubUrl` is correct
3. Check firewall settings
4. Ensure SignalR hub is accessible

### Missing Tray Icon

**Problem**: Tray icon doesn't appear

**Solutions**:
1. Check Windows notification area settings
2. Ensure app is running (check Task Manager)
3. Restart Windows Explorer

### Toast Notifications Not Showing

**Problem**: No notifications appear

**Solutions**:
1. Enable notifications in Windows Settings
2. Check app notification permissions
3. Verify notification focus assist settings

## Architecture

### Components

- **MainWindow.xaml** - Main UI layout
- **MainWindow.xaml.cs** - UI logic and SignalR connection
- **App.xaml** - Application configuration
- **Converters.cs** - UI value converters
- **Resources/Styles.xaml** - UI styles

### SignalR Connection

The app uses `Microsoft.AspNetCore.SignalR.Client` to connect to the hub:

```csharp
_connection = new HubConnectionBuilder()
    .WithUrl(_hubUrl)
    .WithAutomaticReconnect()
    .Build();
```

### Data Binding

Uses WPF data binding with `ObservableCollection`:
- `_conversations` - List of conversations
- `_messages` - Messages in current conversation

## Future Enhancements

- [ ] Settings dialog for hub URL and admin name
- [ ] Message search
- [ ] Conversation filtering
- [ ] File attachments
- [ ] Emoji picker
- [ ] Conversation notes
- [ ] Export conversation history
- [ ] Multiple admin support
- [ ] Away/busy status

## License

MIT
