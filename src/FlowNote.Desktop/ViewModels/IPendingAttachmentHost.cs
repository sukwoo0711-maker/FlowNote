using FlowNote.Core.Models;

namespace FlowNote.Desktop.ViewModels;

public interface IPendingAttachmentHost
{
    void RemovePending(PendingAttachment attachment);
}
