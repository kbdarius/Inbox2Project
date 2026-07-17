using Inbox2Project.Models;

namespace Inbox2Project.Services;

public interface ISelectionValidationService
{
    OutlookItemSelection ValidateSingleMailItem(IReadOnlyList<OutlookItemSelection> selectedItems);
}