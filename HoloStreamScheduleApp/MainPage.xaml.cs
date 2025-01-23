using HoloStreamScheduleApp.Services;
using System.IO;

namespace HoloStreamScheduleApp;

public partial class MainPage : ContentPage
{
    private const int MaxColumns = 5;
    private const int ThumbnailWidth = 50;
    private const int ThumbnailHeight = 50;
    private const int frameMaxWidth = 600;
    private const int frameMaxHeight = 400;
    private const int minColumns = 2;
    private List<StreamItem> _cachedStreams = new();
    private CancellationTokenSource _resizeCancellationTokenSource;
    private readonly string _logFilePath;
    private WebView? _videoPlayer;
    private bool _isVideoPlayerActive = false;
    private string labelText;
    private CancellationTokenSource _timerCancellationTokenSource;



    public MainPage()
    {
        InitializeComponent();
        _logFilePath = @"C:\Users\kuz\source\repos\HoloStreamScheduleApp\HoloStreamScheduleApp\Logs\AppLog.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
        if (!File.Exists(_logFilePath)) File.WriteAllText(_logFilePath, "");
        LogToFile("Application Started...");
        InitializeStreamsAsync();
    }
    private async void InitializeStreamsAsync()
    {

        Title = string.Empty;
        await FetchStreamsAsync();
        Dispatcher.Dispatch(() =>
        {
            LoadGrid();
            LoadSchedule();
            GridContainer.ForceLayout();
            ScheduleContainer.ForceLayout();
        });
        StartGridReloadTimer();
        SizeChanged += OnPageSizeChanged;
    }
    private async Task FetchStreamsAsync()
    {
        try
        {
            var scraper = new ScheduleScraper();
            var url = "https://hololive.hololivepro.com/en/schedule/";
            _cachedStreams = await scraper.ScrapeStreamsAsync(url);

            LogToFile($"Fetched {_cachedStreams.Count} streams.");

            if (_cachedStreams.Count == 0)
            {
                LogToFile("No streams found.");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Error fetching streams: {ex.Message}");
        }
    }
    private async void StartGridReloadTimer()
    {
        _timerCancellationTokenSource = new CancellationTokenSource();

        try
        {
            while (!_timerCancellationTokenSource.Token.IsCancellationRequested)
            {
                await FetchStreamsAsync();
                await Task.Delay(TimeSpan.FromMinutes(30), _timerCancellationTokenSource.Token);
            }
        }
        catch (TaskCanceledException) { /* Timer stopped */ }
    }

    private void LoadGrid()
    {
        if (_cachedStreams == null || _cachedStreams.Count == 0)
        {
            LogToFile("No streams available to display.");
            return;
        }
        if (GridContainer.Content is IDisposable disposableContent)
        {
            disposableContent.Dispose();
        }
        GridContainer.Content = null;

        var dynamicGrid = new Grid
        {
            BackgroundColor = Colors.White,
            RowDefinitions = new RowDefinitionCollection(),
            ColumnDefinitions = new ColumnDefinitionCollection(),
            VerticalOptions = LayoutOptions.Start
        };


        // Log current Width, Height, and cached streams count
        LogToFile($"GridContainer Width: {Width}, Height: {Height}");


        double screenWidth = Width;
        int columns = Math.Max(minColumns, (int)(screenWidth / frameMaxWidth));
        int rows = (_cachedStreams.Count + columns - 1) / columns;

        for (int i = 0; i < rows; i++)
        {
            dynamicGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (int i = 0; i < columns; i++)
        {
            dynamicGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (int i = 0; i < _cachedStreams.Count; i++)
        {
            var stream = _cachedStreams[i];
            var gridItem = CreateStreamCard(stream);
            Grid.SetRow(gridItem, i / columns);
            Grid.SetColumn(gridItem, i % columns);
            dynamicGrid.Children.Add(gridItem);
        }


        double rowHeight = frameMaxHeight; // Define the height of each row
        dynamicGrid.HeightRequest = rows * rowHeight; // Total height of the grid

        LogToFile("Grid loaded successfully.");

        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(100); // Let the UI stabilize
            if (_videoPlayer == null)
            {
                VideoContainer.Content = dynamicGrid;
                MainContainerGrid.RowDefinitions[0].Height = new GridLength(rowHeight * rows);
            }
            else
            {
                GridContainer.Content = dynamicGrid;
            }
            VideoContainer.ForceLayout();
            GridContainer.ForceLayout();
            LogToFile($"Dynamic Grid Actual Width: {dynamicGrid.Width}, Height: {dynamicGrid.Height}");
        });

    }

    private void LoadSchedule()
    {
        if (_cachedStreams == null || _cachedStreams.Count == 0)
        {
            return;
        }
        if (ScheduleContainer.Content is IDisposable disposableContent)
        {
            disposableContent.Dispose();
        }
        ScheduleContainer.Content = null;

        var stackLayout = new StackLayout
        {
            Padding = 0,
            Spacing = 0,
            VerticalOptions = LayoutOptions.Start
        };

        bool isAlternate = false;
        var tokyoTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        var localTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        foreach (var stream in _cachedStreams)
        {
            string labelText;
            const string dateFormat = "MM.dd HH:mm";

            if (DateTime.TryParseExact(stream.Start, dateFormat, null, System.Globalization.DateTimeStyles.None, out var parsedStartTime))
            {
                var tokyoDateTime = TimeZoneInfo.ConvertTimeToUtc(parsedStartTime, tokyoTimeZone);
                var localDateTime = TimeZoneInfo.ConvertTimeFromUtc(tokyoDateTime, localTimeZone);


                if (stream.LiveStatus == "Live")
                {
                    var liveDuration = DateTime.UtcNow - tokyoDateTime;
                    labelText = $"(LIVE) {liveDuration.Hours:D2}:{liveDuration.Minutes:D2} - {stream.Name} - {stream.Text}";
                }
                else
                {
                    labelText = $"{localDateTime:MM.dd HH:mm} (EST) - {stream.Name} - {stream.Text}";
                }
            }
            else
            {
                labelText = $"Invalid Start Time - {stream.Name} - {stream.Text}";
            }

            var label = new Label
            {
                Text = labelText,
                FontSize = 16,
                TextColor = Colors.Black,
                LineBreakMode = LineBreakMode.TailTruncation,
                VerticalTextAlignment = TextAlignment.Center,
                Padding = new Thickness(10, 5) // Add consistent padding inside the label
            };

            var frame = new Frame
            {
                Content = label,
                BackgroundColor = isAlternate ? Colors.LightGray : Colors.White,
                Padding = 0, // No extra padding for the frame
                Margin = 0, // No margins for the frame
                CornerRadius = 0, // Remove rounded corners
                HasShadow = false // Disable shadows for cleaner appearance
            };

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(stream.Link))
                {
                    if (_videoPlayer != null)
                    {
                        CloseVideoPlayer();
                    }

                    LoadVideoPlayer(stream.Link);
                }
            };
            frame.GestureRecognizers.Add(tapGesture);

            stackLayout.Children.Add(frame);
            isAlternate = !isAlternate;
        }

        ScheduleContainer.Content = stackLayout;
        ScheduleContainer.ForceLayout();
    }
    private Frame CreateStreamCard(StreamItem stream)
    {
        var backgroundImage = new Image
        {
            Source = stream.BackgroundThumbnailUrl,
            Aspect = Aspect.AspectFill,
            Opacity = 0.5
        };

        var profileImage = new Image
        {
            Source = stream.ProfilePictureUrl,
            Aspect = Aspect.AspectFill,
            WidthRequest = ThumbnailWidth,
            HeightRequest = ThumbnailHeight
        };

        var profileBorder = new Frame
        {
            WidthRequest = ThumbnailWidth,
            HeightRequest = ThumbnailHeight,
            CornerRadius = ThumbnailWidth / 2,
            Content = profileImage,
            Padding = 0,
            Margin = 0,
            BackgroundColor = Colors.Transparent,
            HasShadow = false
        };

        var liveLabel = new Frame
        {
            CornerRadius = 10,
            BackgroundColor = Colors.Red,
            Padding = 0,
            Content = new Label
            {
                Text = "LIVE",
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                VerticalOptions = LayoutOptions.FillAndExpand
            },
            WidthRequest = 50,
            HeightRequest = 20,
            IsVisible = stream.LiveStatus == "Live",
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start
        };

        var nameLabel = new Label
        {
            Text = stream.Name + " - " + TimeZoneInfo.ConvertTimeFromUtc(DateTime.ParseExact(stream.Start, "MM.dd HH:mm", null), TimeZoneInfo.Local).ToString("MM.dd HH:mm"),
            TextColor = Colors.Black,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Start,
            VerticalTextAlignment = TextAlignment.Start,
            Margin = new Thickness(5, 0, 0, 0)
        };

        var titleLabel = new Label
        {
            Text = stream.Text,
            TextColor = Colors.Black,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.End,
            Margin = 0,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        };

        var durationLabel = new Label
        {
            TextColor = Colors.Black,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Start,
            Margin = new Thickness(0, 20, 0, 0),
            IsVisible = stream.LiveStatus == "Live"
        };

        var grid = new Grid();
        grid.Children.Add(backgroundImage);
        grid.Children.Add(profileBorder);
        grid.Children.Add(liveLabel);
        grid.Children.Add(titleLabel);
        grid.Children.Add(nameLabel);
        grid.Children.Add(durationLabel);

        var frame = new Frame
        {
            Content = grid,
            CornerRadius = 0,
            Padding = 0,
            Margin = 0,
            BorderColor = Colors.Transparent,
            BackgroundColor = Colors.Transparent,
            HasShadow = false
        };

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(stream.Link))
            {
                if (_videoPlayer != null)
                {
                    CloseVideoPlayer();
                }

                LoadVideoPlayer(stream.Link);
            }
        };

        frame.GestureRecognizers.Add(tapGesture);

        if (stream.LiveStatus == "Live")
        {
            profileImage.Rotation = 0;
            profileImage.Animate("Spin", new Animation(v => profileImage.Rotation = v, 0, 360),
                length: 6000,
                repeat: () => true);
        }

        return frame;
    }

    private void OnPageSizeChanged(object sender, EventArgs e)
    {
        if (_isVideoPlayerActive)
        {
            return; // Skip reloading the grid when the video player is active
        }

        _resizeCancellationTokenSource?.Cancel();
        _resizeCancellationTokenSource?.Dispose();
        _resizeCancellationTokenSource = new CancellationTokenSource();

        _ = Task.Delay(200, _resizeCancellationTokenSource.Token).ContinueWith(task =>
        {
            if (!task.IsCanceled)
            {
                Dispatcher.Dispatch(() =>
                {
                    LoadGrid();
                    LoadSchedule();
                });
            }
        });
    }
    private string ExtractVideoId(string url)
    {
        var match = System.Text.RegularExpressions.Regex.Match(url, @"v=([^&]+)");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
    private void LogToFile(string message)
    {
        try
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
            File.AppendAllText(_logFilePath, logMessage);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing to log file: {ex.Message}");
        }
    }

    private void LoadVideoPlayer(string videoUrl)
    {
        CloseVideoPlayer();

        _isVideoPlayerActive = true; // Set the flag
        if (MainContainerGrid.RowDefinitions.Count == 1)
        {
            MainContainerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }

        var codepen = "https://cdpn.io/pen/debug/oNPzxKo?v=" + ExtractVideoId(videoUrl) + "&autoplay=0&mute=1";

        _videoPlayer = new WebView { Source = new UrlWebViewSource { Url = codepen } };

        VideoContainer.Content = _videoPlayer;
        LoadGrid();
        MainContainerGrid.RowDefinitions[0].Height = new GridLength(3, GridUnitType.Star); // First row takes 75%
        MainContainerGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star); // Second row takes 25%
        LogToFile("Video player loaded successfully.");
    }

    private void CloseVideoPlayer()
    {
        if (_videoPlayer != null)
        {
            _videoPlayer.Source = new UrlWebViewSource { Url = "about:blank" };
            MainContainerGrid.Children.Remove(_videoPlayer);
            VideoContainer.Content = null;
            _videoPlayer.Handler?.DisconnectHandler();
            _videoPlayer = null;
            LogToFile("Video player closed.");
        }

        _isVideoPlayerActive = false; // Reset the flag

        // Reset grid layout
        MainContainerGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
        MainContainerGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
    }
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        SizeChanged -= OnPageSizeChanged;
        GridContainer.SizeChanged -= (s, e) => Console.WriteLine($"GridContainer size changed: {GridContainer.Width}x{GridContainer.Height}");
    }
}
