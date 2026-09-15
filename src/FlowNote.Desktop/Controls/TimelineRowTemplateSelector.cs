using System.Windows;
using System.Windows.Controls;
using FlowNote.Desktop.ViewModels;

namespace FlowNote.Desktop.Controls;

public sealed class TimelineRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? EntryTemplate { get; set; }

    public DataTemplate? EventTemplate { get; set; }

    public DataTemplate? GroupTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is TimelineRow row)
        {
            if (row.IsGroup)
            {
                return GroupTemplate;
            }

            return row.IsEvent ? EventTemplate : EntryTemplate;
        }

        return base.SelectTemplate(item, container);
    }
}
