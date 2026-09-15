using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace FlowNote.Desktop.Controls;

public partial class LiveDots : UserControl
{
    public static readonly DependencyProperty DotSizeProperty = DependencyProperty.Register(
        nameof(DotSize),
        typeof(double),
        typeof(LiveDots),
        new PropertyMetadata(6.0, OnDotSizeChanged));

    public static readonly DependencyProperty IsAnimatingProperty = DependencyProperty.Register(
        nameof(IsAnimating),
        typeof(bool),
        typeof(LiveDots),
        new PropertyMetadata(true, OnIsAnimatingChanged));

    private Storyboard? _loop;

    public LiveDots()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public double DotSize
    {
        get => (double)GetValue(DotSizeProperty);
        set => SetValue(DotSizeProperty, value);
    }

    public bool IsAnimating
    {
        get => (bool)GetValue(IsAnimatingProperty);
        set => SetValue(IsAnimatingProperty, value);
    }

    private static void OnDotSizeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is LiveDots dots)
        {
            dots.ApplySize();
        }
    }

    private static void OnIsAnimatingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is LiveDots { IsLoaded: true } dots)
        {
            dots.Restart();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Restart();

    private void OnUnloaded(object sender, RoutedEventArgs e) => StopLoop();

    private void Restart()
    {
        ApplySize();
        StopLoop();
        if (!IsAnimating || !SystemParameters.ClientAreaAnimation)
        {
            RestPose();
            return;
        }

        _loop = BuildLoop();
        _loop.Begin(this, true);
    }

    private void StopLoop()
    {
        _loop?.Stop(this);
        _loop = null;
    }

    private void RestPose()
    {
        Dot0.Opacity = 0.72;
        Dot1.Opacity = 0.72;
        Dot2.Opacity = 0.72;
        SetScale(Dot0, 1);
        SetScale(Dot1, 1);
        SetScale(Dot2, 1);
    }

    private void ApplySize()
    {
        if (Dot0 is null)
        {
            return;
        }

        var size = DotSize;
        var gap = Math.Max(3, Math.Round(size * 0.55));
        Height = Math.Ceiling(size * 1.4 + 2);
        Width = (size * 3) + (gap * 2);
        ApplyDot(Dot0, size, gap);
        ApplyDot(Dot1, size, gap);
        ApplyDot(Dot2, size, 0);
    }

    private static void ApplyDot(FrameworkElement dot, double size, double gap)
    {
        dot.Width = size;
        dot.Height = size;
        dot.Margin = new Thickness(0, 0, gap, 0);
    }

    private Storyboard BuildLoop()
    {
        var board = new Storyboard
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromMilliseconds(1200)
        };
        AddCycle(board, Dot0, 0);
        AddCycle(board, Dot1, 1);
        AddCycle(board, Dot2, 2);
        return board;
    }

    private static void AddCycle(Storyboard board, Ellipse dot, int slot)
    {
        var peak = slot / 3.0;
        board.Children.Add(CycleAnimation(dot, peak, OpacityProperty, 0.26, 1));
        board.Children.Add(CycleAnimation(dot, peak, ScaleTransform.ScaleXProperty, 0.9, 1.16, isScale: true));
        board.Children.Add(CycleAnimation(dot, peak, ScaleTransform.ScaleYProperty, 0.9, 1.16, isScale: true));
    }

    private static DoubleAnimationUsingKeyFrames CycleAnimation(
        Ellipse dot,
        double peak,
        DependencyProperty property,
        double rest,
        double active,
        bool isScale = false)
    {
        var rise = Math.Max(0, peak);
        var hold = Math.Min(1, peak + 0.18);
        var fall = Math.Min(1, peak + 0.33);
        var frames = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(1200)
        };
        frames.KeyFrames.Add(new DiscreteDoubleKeyFrame(rest, KeyTime.FromPercent(0)));
        if (rise > 0)
        {
            frames.KeyFrames.Add(new DiscreteDoubleKeyFrame(rest, KeyTime.FromPercent(rise)));
        }

        frames.KeyFrames.Add(new EasingDoubleKeyFrame(active, KeyTime.FromPercent(Math.Min(1, rise + 0.08)), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        frames.KeyFrames.Add(new DiscreteDoubleKeyFrame(active, KeyTime.FromPercent(hold)));
        frames.KeyFrames.Add(new EasingDoubleKeyFrame(rest, KeyTime.FromPercent(fall), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
        if (fall < 1)
        {
            frames.KeyFrames.Add(new DiscreteDoubleKeyFrame(rest, KeyTime.FromPercent(1)));
        }

        Storyboard.SetTarget(frames, dot);
        Storyboard.SetTargetProperty(frames, new PropertyPath(
            isScale
                ? $"(UIElement.RenderTransform).(ScaleTransform.{property.Name})"
                : property.Name));
        return frames;
    }

    private static void SetScale(Ellipse dot, double value)
    {
        if (dot.RenderTransform is ScaleTransform scale)
        {
            scale.ScaleX = value;
            scale.ScaleY = value;
        }
    }
}
