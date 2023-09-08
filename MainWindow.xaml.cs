using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace signalr;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    HubConnection? connection = null;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void Log(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Log(message));
            return;
        }

        debuggingText.Text += message + "\r\n";
    }

    private Task ConnectionOnClosed(Exception exception)
    {
        Log($"ConnectionOnClosed: {exception?.Message}");
        UpdateButtons();
        return Task.CompletedTask;
    }

    private void UpdateButtons()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => UpdateButtons());
            return;
        }

        if (connection == null || connection.State == HubConnectionState.Disconnected)
        {
            connectButton.IsEnabled = true;
            connectButton.Content = "Connect";
            //buttonSend.IsEnabled = false;
        }
        else if (connection.State == HubConnectionState.Connected)
        {
            connectButton.IsEnabled = true;
            connectButton.Content = "Disconnect";
            //buttonSend.IsEnabled = true;
        }
        else if (connection.State == HubConnectionState.Connecting || connection.State == HubConnectionState.Reconnecting)
        {
            connectButton.IsEnabled = false;
            connectButton.Content = "Connecting...";
            //buttonSend.IsEnabled = false;
        }
    }

    public async void connectButtonClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(textBoxServerUrl.Text))
        {
            Log("No Server URL!");
            return;
        }

        try
        {
            IsEnabled = false;

            connection = new HubConnectionBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Information);
                    logging.AddProvider(new LoggingProvider(this));
                })
                .WithUrl($"{textBoxServerUrl.Text}/chatHub", options =>
            //https://www.piesocket.com/websocket-tester
            //.WithUrl("wss://demo.websocket.me/v3/channel_1?api_key=oCdCMcMPQpbvNjUIzqtvF1d2X2okWpDQj4AwARJuAgtjhzKxVEjQU6IdCjwm&notify_self", options =>
            {
                // We only support websockets - and non sticky sessions
                options.Transports = HttpTransportType.WebSockets;
                    options.SkipNegotiation = true;
                    options.DefaultTransferFormat = TransferFormat.Text;
                    //options.AccessTokenProvider = AccessTokenProvider;

                //var proxy = new WebProxy("http://localhost:8888", false);
                //proxy.UseDefaultCredentials = false;
                //options.Proxy = proxy;
            })
                .Build();

            connection.HandshakeTimeout = TimeSpan.FromSeconds(10);
            connection.Closed += ConnectionOnClosed;

            connection.On<string, string>("ReceiveMessage", (user, message) =>
            {
                Log($"Rx => {user}: {message}");
            });

            Log("Attempting to connect...");
            await connection.StartAsync();
            Log("Connected");
        }
        catch (Exception ex)
        {
            Log("Error Connecting: " + ex.Message);
        }
        finally
        {
            UpdateButtons();
            IsEnabled = true;
        }
    }
}

public class LoggingProvider : ILoggerProvider
{
    private MainWindow form;

    public LoggingProvider(MainWindow form)
    {
        this.form = form;
    }

    public void Dispose()
    {
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new TraceLogger(form);
    }
}

public class TraceLogger : ILogger
{
    private MainWindow form;

    public TraceLogger(MainWindow form)
    {
        this.form = form;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        var message = $"{logLevel} {state} {exception?.Message}";
        form.Log(message);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return new LoggingProvider(form);
    }
}