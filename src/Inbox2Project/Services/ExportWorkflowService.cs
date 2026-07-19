using System.Text;
using Inbox2Project.Models;

namespace Inbox2Project.Services;

public sealed class ExportWorkflowService : IExportWorkflowService
{
    private readonly ISettingsService _settingsService;
    private readonly IProjectDiscoveryService _projectDiscoveryService;
    private readonly IProjectSelectorUi _projectSelectorUi;
    private readonly IAttachmentPromptService _attachmentPromptService;
    private readonly IPathSafetyService _pathSafetyService;
    private readonly ILoggingService _loggingService;

    public ExportWorkflowService(
        ISettingsService settingsService,
        IProjectDiscoveryService projectDiscoveryService,
        IProjectSelectorUi projectSelectorUi,
        IAttachmentPromptService attachmentPromptService,
        IPathSafetyService pathSafetyService,
        ILoggingService loggingService)
    {
        _settingsService = settingsService;
        _projectDiscoveryService = projectDiscoveryService;
        _projectSelectorUi = projectSelectorUi;
        _attachmentPromptService = attachmentPromptService;
        _pathSafetyService = pathSafetyService;
        _loggingService = loggingService;
    }

    public async Task<OperationResult> ExportAsync(OutlookItemSelection item, CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _settingsService.LoadAsync(cancellationToken);
            var projects = settings.SavedProjects.Select(project => project.ProjectPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var selectedProject = await _projectSelectorUi.SelectProjectAsync(projects, settings.LastSelectedProject, cancellationToken);
            if (string.IsNullOrWhiteSpace(selectedProject))
            {
                return OperationResult.Cancel();
            }

            if (!Directory.Exists(selectedProject))
            {
                throw new DirectoryNotFoundException("The selected destination folder no longer exists.");
            }

            var destinationPath = selectedProject;

            var sanitizedSubject = _pathSafetyService.SanitizeName(item.Subject);
            var attachmentCount = item.Attachments.Count;
            var hasAttachments = attachmentCount > 0;

            var attachmentChoice = new AttachmentSaveChoice(false, true, Array.Empty<AttachmentData>());
            if (hasAttachments)
            {
                try
                {
                    attachmentChoice = await _attachmentPromptService.SelectAttachmentsAsync(item, cancellationToken);
                }
                catch (Exception promptException)
                {
                    var (promptUserMessage, _) = ErrorCatalog.Lookup(AppErrorId.PromptFailed);
                    throw new AppException(AppErrorId.PromptFailed, promptUserMessage, "Attachment prompt failed.", promptException);
                }
            }

            if (attachmentChoice.Cancelled)
            {
                return OperationResult.Cancel();
            }

            var selectedAttachments = attachmentChoice.Attachments;
            var outputDir = destinationPath;
            if (selectedAttachments.Count > 0)
            {
                var folderName = _pathSafetyService.SanitizeName(sanitizedSubject);
                var candidateFolder = _pathSafetyService.GetUniquePath(destinationPath, folderName);
                outputDir = _pathSafetyService.EnsureSafePathLength(candidateFolder);
                Directory.CreateDirectory(outputDir);
            }

            var outputs = new List<string>();
            string? txtPath = null;
            if (attachmentChoice.IncludeEmail)
            {
                var txtFileName = _pathSafetyService.SanitizeName(sanitizedSubject) + ".txt";
                txtPath = _pathSafetyService.GetUniquePath(outputDir, txtFileName);
                txtPath = _pathSafetyService.EnsureSafePathLength(txtPath);
                var txtPayload = BuildTextPayload(item);
                await File.WriteAllTextAsync(txtPath, txtPayload, Encoding.UTF8, cancellationToken);
                outputs.Add(txtPath);
            }
            if (selectedAttachments.Count > 0)
            {
                foreach (var attachment in selectedAttachments)
                {
                    var safeName = _pathSafetyService.SanitizeName(attachment.FileName, "attachment.bin");
                    var attachmentPath = _pathSafetyService.GetUniquePath(outputDir, safeName);
                    attachmentPath = _pathSafetyService.EnsureSafePathLength(attachmentPath);
                    try
                    {
                        await File.WriteAllBytesAsync(attachmentPath, attachment.Content, cancellationToken);
                        outputs.Add(attachmentPath);
                    }
                    catch (UnauthorizedAccessException writeDenied)
                    {
                        var (writeDeniedUserMessage, _) = ErrorCatalog.Lookup(AppErrorId.FsWriteDenied);
                        throw new AppException(AppErrorId.FsWriteDenied, writeDeniedUserMessage, $"Access denied while writing attachment: {attachmentPath}", writeDenied);
                    }
                    catch (Exception attachmentException)
                    {
                        var (attachmentUserMessage, _) = ErrorCatalog.Lookup(AppErrorId.AttSaveFailed);
                        throw new AppException(AppErrorId.AttSaveFailed, attachmentUserMessage, $"Failed to save attachment: {attachment.FileName}", attachmentException);
                    }
                }
            }

            // Persist only after the export succeeds, so "last used" always means
            // the last project that actually received a saved email.
            await _settingsService.SaveLastSelectedProjectAsync(selectedProject, cancellationToken);

            await _loggingService.LogInfoAsync(
                "export_completed",
                new
                {
                    selectedProject,
                    destinationPath,
                    txtPath,
                    hasAttachments,
                    selectedAttachmentCount = selectedAttachments.Count,
                    attachmentCount,
                    outputs,
                },
                cancellationToken);

            var userMessage = attachmentChoice.IncludeEmail && selectedAttachments.Count > 0
                ? $"Saved the email and {selectedAttachments.Count} selected attachment(s)."
                : attachmentChoice.IncludeEmail
                    ? "Saved email text successfully."
                    : $"Saved {selectedAttachments.Count} selected attachment(s).";

            return OperationResult.Success(userMessage, outputs.ToArray());
        }
        catch (UnauthorizedAccessException writeDenied)
        {
            var (userMessage, _) = ErrorCatalog.Lookup(AppErrorId.FsWriteDenied);
            return await HandleFailureAsync(AppErrorId.FsWriteDenied, userMessage, writeDenied.ToString(), cancellationToken);
        }
        catch (PathTooLongException pathTooLong)
        {
            var (userMessage, _) = ErrorCatalog.Lookup(AppErrorId.FsPathInvalid);
            return await HandleFailureAsync(AppErrorId.FsPathInvalid, userMessage, pathTooLong.ToString(), cancellationToken);
        }
        catch (AppException appException)
        {
            return await HandleFailureAsync(appException.ErrorId, appException.UserMessage, appException.Message, cancellationToken);
        }
        catch (Exception exception)
        {
            var (userMessage, _) = ErrorCatalog.Lookup(AppErrorId.Unknown);
            return await HandleFailureAsync(AppErrorId.Unknown, userMessage, exception.ToString(), cancellationToken);
        }
    }

    private static string BuildTextPayload(OutlookItemSelection item)
    {
        var separator = new string('-', 72);
        return string.Join(
            Environment.NewLine,
            "Inbox2Project Email Export",
            separator,
            $"Subject: {item.Subject}",
            $"Sender: {item.Sender}",
            $"ReceivedAt: {item.ReceivedAt:O}",
            $"ConversationTopic: {item.ConversationTopic}",
            $"AttachmentCount: {item.Attachments.Count}",
            separator,
            item.BodyText ?? string.Empty,
            string.Empty);
    }

    private async Task<OperationResult> HandleFailureAsync(
        AppErrorId errorId,
        string userMessage,
        string technicalMessage,
        CancellationToken cancellationToken)
    {
        await _loggingService.LogErrorAsync(
            "export_failed",
            new
            {
                errorId = errorId.ToString(),
                userMessage,
                technicalMessage,
            },
            cancellationToken);

        return OperationResult.Failure(errorId, userMessage, technicalMessage);
    }
}
