using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.AspNetCore.SignalR.Client;
using Mostlylucid.Chat.Shared.Models;

namespace Mostlylucid.Chat.TrayApp;

public partial class MainWindow : Window
{
    private HubConnection? _connection;
    private ObservableCollection<Conversation> _conversations = new();
    private ObservableCollection<MessageViewModel> _messages = new();
    private Conversation? _currentConversation;
    private TaskbarIcon? _trayIcon;
    private readonly string _hubUrl = "http://localhost:5100/chathub";
    private readonly string _adminName = "Scott";

    public MainWindow()
    {
        InitializeComponent();
        ConversationsList.ItemsSource = _conversations;
        MessagesList.ItemsSource = _messages;

        // Setup tray icon
        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");

        // Connect to SignalR hub
        ConnectToHub();
    }

    private async void ConnectToHub()
    {
        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(_hubUrl)
                .WithAutomaticReconnect()
                .Build();

            // Event handlers
            _connection.On<List<Conversation>>("ActiveConversations", (conversations) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _conversations.Clear();
                    foreach (var conv in conversations)
                    {
                        _conversations.Add(conv);
                    }
                });
            });

            _connection.On<ConnectionInfo, Conversation>("NewUserConnected", (connectionInfo, conversation) =>
            {
                Dispatcher.Invoke(() =>
                {
                    var existing = _conversations.FirstOrDefault(c => c.Id == conversation.Id);
                    if (existing == null)
                    {
                        _conversations.Insert(0, conversation);
                        ShowNotification($"New chat from {conversation.UserName}");
                    }
                });
            });

            _connection.On<ChatMessage>("UserMessage", (message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    var conversation = _conversations.FirstOrDefault(c => c.Id == message.ConversationId);
                    if (conversation != null)
                    {
                        conversation.Messages.Add(message);
                        conversation.LastMessageAt = message.Timestamp;
                        conversation.UnreadCount++;

                        // Update messages if this is the current conversation
                        if (_currentConversation?.Id == message.ConversationId)
                        {
                            _messages.Add(new MessageViewModel(message));
                            ScrollToBottom();
                        }

                        // Show notification
                        ShowNotification($"{message.SenderName}: {message.Content}");
                    }
                });
            });

            _connection.On<ChatMessage>("MessageReceived", (message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_currentConversation?.Id == message.ConversationId)
                    {
                        _messages.Add(new MessageViewModel(message));
                        ScrollToBottom();
                    }
                });
            });

            _connection.On<List<ChatMessage>>("ConversationHistory", (messages) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _messages.Clear();
                    foreach (var msg in messages)
                    {
                        _messages.Add(new MessageViewModel(msg));
                    }
                    ScrollToBottom();
                });
            });

            _connection.Reconnecting += error =>
            {
                Dispatcher.Invoke(() =>
                {
                    ConnectionStatus.Text = "Reconnecting...";
                });
                return Task.CompletedTask;
            };

            _connection.Reconnected += connectionId =>
            {
                Dispatcher.Invoke(() =>
                {
                    ConnectionStatus.Text = "Connected";
                });
                return Task.CompletedTask;
            };

            _connection.Closed += error =>
            {
                Dispatcher.Invoke(() =>
                {
                    ConnectionStatus.Text = "Disconnected";
                });
                return Task.CompletedTask;
            };

            // Start connection
            await _connection.StartAsync();
            await _connection.InvokeAsync("RegisterAdmin", _adminName);

            ConnectionStatus.Text = "Connected";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to connect: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ConversationsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ConversationsList.SelectedItem is Conversation conversation)
        {
            _currentConversation = conversation;
            LoadConversation(conversation);
        }
    }

    private async void LoadConversation(Conversation conversation)
    {
        ChatHeader.Visibility = Visibility.Visible;
        ChatInput.Visibility = Visibility.Visible;

        ChatUserName.Text = conversation.UserName;
        ChatUserEmail.Text = conversation.UserEmail;
        ChatSourceUrl.Text = conversation.SourceUrl;

        // Join conversation
        if (_connection != null)
        {
            await _connection.InvokeAsync("JoinConversation", conversation.Id);
        }

        // Mark as read
        conversation.UnreadCount = 0;
        if (_connection != null)
        {
            await _connection.InvokeAsync("MarkAsRead", conversation.Id);
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await SendMessage();
    }

    private async void MessageInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(MessageInput.Text))
        {
            await SendMessage();
        }
    }

    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(MessageInput.Text) || _connection == null || _currentConversation == null)
            return;

        try
        {
            await _connection.InvokeAsync("SendMessage", MessageInput.Text.Trim());
            MessageInput.Clear();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to send message: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ScrollToBottom()
    {
        MessagesScrollViewer.ScrollToEnd();
    }

    private void ShowNotification(string message)
    {
        _trayIcon?.ShowBalloonTip("Chat Notification", message, BalloonIcon.Info);
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void TrayIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void MenuOpen_Click(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        _trayIcon?.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        WindowState = WindowState.Minimized;
    }
}

public class MessageViewModel
{
    public MessageViewModel(ChatMessage message)
    {
        Content = message.Content;
        SenderName = message.SenderName;
        SenderType = message.SenderType;
        Timestamp = message.Timestamp;
        Initials = GetInitials(message.SenderName);
    }

    public string Content { get; set; }
    public string SenderName { get; set; }
    public string SenderType { get; set; }
    public DateTime Timestamp { get; set; }
    public string Initials { get; set; }

    private static string GetInitials(string name)
    {
        return string.Join("", name.Split(' ').Select(n => n.FirstOrDefault())).ToUpper().Substring(0, Math.Min(2, name.Length));
    }
}
