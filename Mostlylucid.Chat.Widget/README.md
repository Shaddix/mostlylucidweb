# Chat Widget - Embeddable Real-Time Chat

A lightweight, embeddable chat widget for real-time communication with your website visitors.

## Features

- 🚀 **Easy Integration** - Add with a single `<script>` tag
- 💬 **Real-Time Messaging** - Powered by SignalR
- 🎨 **Beautiful UI** - Modern, responsive design
- 📱 **Mobile Friendly** - Works on all devices
- 🔔 **Notifications** - Browser notifications for new messages
- 💾 **Conversation History** - Messages persist across sessions
- ⚡ **Lightweight** - Built with Alpine.js for minimal overhead

## Quick Start

### 1. Build the Widget

```bash
cd Mostlylucid.Chat.Widget
npm install
npm run build
```

This creates `dist/widget.js` that you can serve from your chat server.

### 2. Include in Your Website

Add this script tag to any HTML page:

```html
<script
  src="https://your-chat-server.com/widget.js"
  data-chat-widget
  data-hub-url="https://your-chat-server.com/chathub"
></script>
```

That's it! The chat widget will automatically appear in the bottom-right corner.

## Configuration Options

Configure the widget using data attributes:

```html
<script
  src="https://your-chat-server.com/widget.js"
  data-chat-widget
  data-hub-url="https://your-chat-server.com/chathub"
  data-user-name="John Doe"
  data-user-email="john@example.com"
></script>
```

### Available Options

| Attribute | Description | Default |
|-----------|-------------|---------|
| `data-hub-url` | SignalR hub URL | `http://localhost:5100/chathub` |
| `data-user-name` | Pre-fill user name | Empty |
| `data-user-email` | Pre-fill user email | Empty |

## Manual Initialization

For more control, you can initialize the widget programmatically:

```html
<script src="https://your-chat-server.com/widget.js"></script>
<script>
  const widget = new ChatWidget({
    hubUrl: 'https://your-chat-server.com/chathub',
    userName: 'John Doe',
    userEmail: 'john@example.com'
  });
</script>
```

## Features

### Persistent Sessions

User information is saved in localStorage, so returning visitors don't need to re-enter their details.

### Browser Notifications

The widget requests notification permission and shows browser notifications for new messages when the chat is minimized.

### Typing Indicators

See when the admin is typing a response.

### Message History

All messages are preserved and loaded when users return to the site.

### Responsive Design

The widget adapts to mobile screens and works great on all devices.

## Development

### Build for Development

```bash
npm run dev
```

This starts webpack in watch mode, rebuilding on file changes.

### Customization

#### Styling

Edit `src/css/widget.css` to customize the appearance. The widget uses CSS variables for easy theming:

```css
.chat-widget-button {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}
```

#### Behavior

Modify `src/js/widget.js` to change widget behavior. The widget uses Alpine.js for reactivity.

## Browser Support

- Chrome/Edge (latest)
- Firefox (latest)
- Safari (latest)
- Mobile browsers

## Dependencies

- **@microsoft/signalr** - Real-time communication
- **alpinejs** - Reactive UI framework
- **webpack** - Module bundler (dev only)

## License

MIT
