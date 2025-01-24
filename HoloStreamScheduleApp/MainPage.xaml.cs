using HoloStreamScheduleApp.Services;
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
    private readonly string _logFilePath;

    public MainPage()
    {
        InitializeComponent();
        InitializeApp();
    }

    private void InitializeApp()
    {
        Layout1.IsVisible = true;
        Layout2.IsVisible = false;
        Title = string.Empty;
        InitializeStreamsAsync();
    }
    private async Task InitializeStreamsAsync()
    {
        await FetchStreamsAsync();
        LoadGrid(Layout1Mode: true); // Initial grid load for Layout1
        LoadSchedule(ScheduleContainer); // Load schedule for Layout1
    }
    private async Task FetchStreamsAsync()
    {
        try
        {
            var scraper = new ScheduleScraper();
            var url = "https://hololive.hololivepro.com/en/schedule/";
            _cachedStreams = await scraper.ScrapeStreamsAsync(url);

            LogToFile($"Fetched {_cachedStreams.Count} streams.");

            // Filter the streams: remove streams that have passed and are not live
            var now = DateTime.UtcNow;
            var filteredStreams = _cachedStreams.Where(stream =>
            {
                if (DateTime.TryParseExact(stream.Start, "MM.dd HH:mm", null, System.Globalization.DateTimeStyles.None, out var startTime))
                {
                    var utcStartTime = TimeZoneInfo.ConvertTimeToUtc(startTime, TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"));
                    return utcStartTime >= now.AddMinutes(-15) || stream.LiveStatus == "Live";
                }
                else
                {
                    LogToFile($"Invalid start time format for stream: {stream.Name} - {stream.Start}");
                    return false;
                }
            }).ToList();

            LogToFile($"Filtered streams: {filteredStreams.Count} streams remaining after filtering.");

            _cachedStreams = filteredStreams;

            if (_cachedStreams.Count == 0)
            {
                LogToFile("No valid streams found after filtering.");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Error fetching streams: {ex.Message}");
        }
    }


    private void OnReturnButtonClicked(object sender, EventArgs e)
    {
        CloseVideoPlayer(); // Ensures the video player is cleaned up
        Layout1.IsVisible = true; // Show the first layout
        Layout2.IsVisible = false; // Hide the video view
    }

    private void LoadGrid(bool Layout1Mode)
    {
        var container = Layout1Mode ? GridContainer : GridContainerLayout2;

        if (_cachedStreams == null || _cachedStreams.Count == 0)
        {
            LogToFile("No streams available to display.");
            return;
        }

        // Clear existing content
        if (container.Content is IDisposable disposableContent)
        {
            disposableContent.Dispose();
        }
        container.Content = null;

        // Create and populate dynamic grid
        var dynamicGrid = new Grid
        {
            RowDefinitions = new RowDefinitionCollection(),
            ColumnDefinitions = new ColumnDefinitionCollection(),
            VerticalOptions = LayoutOptions.Start
        };

        int columns = Math.Max(minColumns, (int)(Width / frameMaxWidth));
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

        container.Content = dynamicGrid;
        container.ForceLayout();
    }
    private void LoadSchedule(ScrollView container)
    {
        if (_cachedStreams == null || _cachedStreams.Count == 0)
        {
            LogToFile("No schedule available.");
            return;
        }

        if (container.Content is IDisposable disposableContent)
        {
            disposableContent.Dispose();
        }
        container.Content = null;

        var stackLayout = new StackLayout
        {
            Padding = 0,
            Spacing = 0,
            VerticalOptions = LayoutOptions.Start
        };

        var tokyoTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        var localTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

        bool isAlternate = false; // Used to alternate colors

        foreach (var stream in _cachedStreams)
        {
            var innerStack = new StackLayout
            {
                Padding = new Thickness(10, 5),
                Spacing = 2,
                VerticalOptions = LayoutOptions.Start
            };

            if (DateTime.TryParseExact(stream.Start, "MM.dd HH:mm", null, System.Globalization.DateTimeStyles.None, out var parsedStartTime))
            {
                var tokyoDateTime = TimeZoneInfo.ConvertTimeToUtc(parsedStartTime, tokyoTimeZone);
                var localDateTime = TimeZoneInfo.ConvertTimeFromUtc(tokyoDateTime, localTimeZone);

                if (stream.LiveStatus == "Live")
                {
                    var liveDuration = DateTime.UtcNow - tokyoDateTime;

                    // Line 1: "(LIVE)" with stream duration
                    innerStack.Children.Add(new Label
                    {
                        Text = $"(LIVE) {liveDuration.Hours:D2}:{liveDuration.Minutes:D2}",
                        FontSize = 16,
                        TextColor = Colors.Red,
                        FontAttributes = FontAttributes.Bold
                    });

                    // Line 2: Stream name and description
                    innerStack.Children.Add(new Label
                    {
                        Text = $"{stream.Name} - {stream.Text}",
                        FontSize = 16
                    });
                }
                else
                {
                    // Line 1: Localized time
                    innerStack.Children.Add(new Label
                    {
                        Text = $"{localDateTime:h:mm tt} (EST)",
                        FontSize = 16
                    });

                    // Line 2: Stream name and description
                    innerStack.Children.Add(new Label
                    {
                        Text = $"{stream.Name} - {stream.Text}",
                        FontSize = 16
                    });
                }
            }
            else
            {
                // Handle invalid start time
                innerStack.Children.Add(new Label
                {
                    Text = "Invalid Start Time",
                    FontSize = 16
                });

                innerStack.Children.Add(new Label
                {
                    Text = $"{stream.Name} - {stream.Text}",
                    FontSize = 16
                });
            }

            // Alternate background colors
            var backgroundColor = isAlternate ? Colors.WhiteSmoke : Colors.LightGray;
            isAlternate = !isAlternate; // Toggle the alternate flag

            var frame = new Frame
            {
                Content = innerStack,
                CornerRadius = 0,
                BorderColor = Colors.Transparent,
                BackgroundColor = backgroundColor,
                HasShadow = false,
                Margin = 0,
                Padding = 0
            };

            stackLayout.Children.Add(frame);
        }

        container.Content = stackLayout;
        container.ForceLayout();
    }


    private void LoadVideo(string videoUrl)
    {
        CloseVideoPlayer(); // Ensure any previous player is cleaned up

        Layout1.IsVisible = false;
        Layout2.IsVisible = true;

        var codepen = "https://cdpn.io/pen/debug/oNPzxKo?v=" + System.Text.RegularExpressions.Regex.Match(videoUrl, @"v=([^&]+)").Groups[1].Value + "&autoplay=0&mute=1";

        var videoPlayer = new WebView
        {
            Source = new UrlWebViewSource { Url = codepen }
        };

        VideoContainer.Content = videoPlayer;

        // Populate Grid and Schedule for Layout2
        LoadGrid(Layout1Mode: false);
        LoadSchedule(ScheduleContainerLayout2);
    }

    private void CloseVideoPlayer()
    {
        if (VideoContainer.Content is WebView videoPlayer)
        {
            videoPlayer.Source = new UrlWebViewSource { Url = "about:blank" };
            videoPlayer.Handler?.DisconnectHandler();
            VideoContainer.Content = null;
        }

        Layout1.IsVisible = true;
        Layout2.IsVisible = false;
    }

    private void OnVideoTapped(object sender, EventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is StreamItem stream)
        {
            LoadVideo(stream.Link);
        }
        else if (sender is Frame frame && frame.BindingContext is StreamItem streamFromFrame)
        {
            LoadVideo(streamFromFrame.Link);
        }
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
            BorderColor = stream.LiveStatus == "Live" ? Colors.Red : Colors.Transparent,
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
            TextColor = Colors.Black,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Start,
            VerticalTextAlignment = TextAlignment.Start,
            Margin = new Thickness(5, 0, 0, 0)
        };

        // Corrected time conversion
        var tokyoTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        var localTimeZone = TimeZoneInfo.Local;

        if (DateTime.TryParseExact(stream.Start, "MM.dd HH:mm", null, System.Globalization.DateTimeStyles.None, out var parsedStartTime))
        {
            var tokyoDateTime = TimeZoneInfo.ConvertTimeToUtc(parsedStartTime, tokyoTimeZone);
            var localDateTime = TimeZoneInfo.ConvertTimeFromUtc(tokyoDateTime, localTimeZone);

            nameLabel.Text = $"{stream.Name} - {localDateTime:h:mm tt}";
        }
        else
        {
            nameLabel.Text = $"{stream.Name} - Invalid Time";
        }

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

        // Add the BindingContext for the frame or grid
        grid.BindingContext = stream;
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += OnVideoTapped; // Simplified event handler
        grid.GestureRecognizers.Add(tapGesture);

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

        if (stream.LiveStatus == "Live")
        {
            profileImage.Rotation = 0;
            profileImage.Animate("Spin", new Animation(v => profileImage.Rotation = v, 0, 360),
                length: 6000,
                repeat: () => true);
        }

        return frame;
    }
}
