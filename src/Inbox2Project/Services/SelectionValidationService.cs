using Inbox2Project.Models;

namespace Inbox2Project.Services;

public sealed class SelectionValidationService : ISelectionValidationService
{
    public OutlookItemSelection ValidateSingleMailItem(IReadOnlyList<OutlookItemSelection> selectedItems)
    {
        if (selectedItems.Count == 0)
        {
            var (userMessage, _) = ErrorCatalog.Lookup(AppErrorId.SelEmpty);
            throw new AppException(AppErrorId.SelEmpty, userMessage, "No items were selected in Outlook.");
        }

        if (selectedItems.Count != 1)
        {
            var (userMessage, _) = ErrorCatalog.Lookup(AppErrorId.SelUnsupported);
            throw new AppException(AppErrorId.SelUnsupported, userMessage, $"Expected one item, got {selectedItems.Count}.");
        }

        var item = selectedItems[0];
        if (item.ItemType != OutlookItemType.MailItem)
        {
            var (userMessage, _) = ErrorCatalog.Lookup(AppErrorId.SelUnsupported);
            throw new AppException(AppErrorId.SelUnsupported, userMessage, $"Unsupported item type {item.ItemType}.");
        }

        return item;
    }
}