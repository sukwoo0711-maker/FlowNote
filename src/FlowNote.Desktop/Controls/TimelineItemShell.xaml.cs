using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace FlowNote.Desktop.Controls;

[ContentProperty(nameof(Body))]
public partial class TimelineItemShell : UserControl
{
    public static readonly DependencyProperty TimeLabelProperty = DependencyProperty.Register(
        nameof(TimeLabel), typeof(string), typeof(TimelineItemShell));

    public static readonly DependencyProperty NodeKindProperty = DependencyProperty.Register(
        nameof(NodeKind), typeof(string), typeof(TimelineItemShell), new PropertyMetadata("note"));

    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
        nameof(Body), typeof(object), typeof(TimelineItemShell));

    public TimelineItemShell()
    {
        InitializeComponent();
    }

    public string? TimeLabel
    {
        get => (string?)GetValue(TimeLabelProperty);
        set => SetValue(TimeLabelProperty, value);
    }

    public string NodeKind
    {
        get => (string)GetValue(NodeKindProperty);
        set => SetValue(NodeKindProperty, value);
    }

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }
}
