using System.Text;
using System.Text.RegularExpressions;
using Inbox2Project.Models;

namespace Inbox2Project.Services;

public sealed class ExportWorkflowService : IExportWorkflowService
{
    private static readonly Regex DatePrefixRegex = new(@"^\d{8}_", RegexOptions.Compiled);

    private readonly ISettingsService _settingsService;
    private readonly IProjectDiscoveryService _projectDiscoveryService;
    private readonly IProjectSelectorUi _projectSelectorUi;
    private readonly IAttachmentPromptService _attachmentPromptService;
    private readonly IPathSafetyService _pathSafetyService;
    private readonly IAiFolderNameService _aiFolderNameService;
    private readonly IDuplicateSubjectPromptService _duplicateSubjectPromptService;
    private readonly ILoggingService _loggingService;

    public ExportWorkflowService(
        ISettingsService settingsService,
        IProjectDiscoveryService projectDiscoveryService,
        IProjectSelectorUi projectSelectorUi,
        IAttachmentPromptService attachmentPromptService,
        IPathSafetyService pathSafetyService,
        IAiFolderNameService? aiFolderNameService,
        ILoggingService loggingService,
        IDuplicateSubjectPromptService? duplicateSubjectPromptService = null)
    {
        _settingsService = settingsService;
        _projectDiscoveryService = projectDiscoveryService;
        _projectSelectorUi = projectSelectorUi;
        _attachmentPromptService = attachmentPromptService;
        _pathSafetyService = pathSafetyService;
        _aiFolderNameService = aiFolderNameService ?? new NoOpAiFolderNameService();
        _loggingService = loggingService;
        _duplicateSubjectPromptService = duplicateSubjectPromptService ?? new DefaultDuplicateSubjectPromptService();
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

            var sanitizedSubject = _pathSafetyService.SanitizeName(item.Subject);
            var aiSubject = await TryGetAiNameAsync(settings, item, cancellationToken);
            var suggestedBaseName = string.IsNullOrWhiteSpace(aiSubject) ? sanitizedSubject : aiSubject;

            var selection = await _projectSelectorUi.SelectProjectAsync(
                projects,
                settings.LastSelectedProject,
                suggestedBaseName,
                item.Sender,
                item.ReceivedAt,
                cancellationToken);
            if (selection is null || string.IsNullOrWhiteSpace(selection.ProjectPath))
            {
                return OperationResult.Cancel();
            }

            var selectedProject = selection.ProjectPath;
            if (!Directory.Exists(selectedProject))
            {
                throw new DirectoryNotFoundException("The selected destination folder no longer exists.");
            }

            var destinationPath = selectedProject;

            var baseName = _pathSafetyService.SanitizeName(selection.FinalBaseName, suggestedBaseName);
            var datePrefix = $"{item.ReceivedAt:yyyyMMdd}_";
            var prefixedBaseName = $"{datePrefix}{baseName}";
            var attachmentCount = item.Attachments.Count;
            var hasAttachments = attachmentCount > 0;

            var overwriteExisting = false;
            string? overwriteTargetDir = null;
            var existingMatch = FindExistingSubjectMatch(destinationPath, sanitizedSubject);
            if (existingMatch is not null)
            {
                var decision = await _duplicateSubjectPromptService.ResolveAsync(existingMatch.Name, item.Subject, cancellationToken);
                if (decision == DuplicateSubjectDecision.Cancel)
                {
                    return OperationResult.Cancel();
                }

                if (decision == DuplicateSubjectDecision.UseExistingFolder)
                {
                    overwriteExisting = true;
                    overwriteTargetDir = existingMatch.IsDirectory ? existingMatch.FullPath : destinationPath;
                }
                else if (!string.IsNullOrWhiteSpace(item.Sender))
                {
                    // Creating a distinct new folder: make sure the sender name is included
                    // so this export is distinguishable from the earlier one(s) with the same subject.
                    var sanitizedSender = _pathSafetyService.SanitizeName(item.Sender);
                    var prefix = sanitizedSender + "_";
                    var legacySuffix = "_-_" + sanitizedSender;
                    if (!baseName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        && !baseName.EndsWith(legacySuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        baseName = string.IsNullOrWhiteSpace(baseName)
                            ? sanitizedSender
                            : $"{sanitizedSender}_{baseName}";
                        prefixedBaseName = $"{datePrefix}{baseName}";
                    }
                }
            }

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
            if (overwriteExisting && overwriteTargetDir is not null)
            {
                outputDir = overwriteTargetDir;
                Directory.CreateDirectory(outputDir);
            }
            else if (selectedAttachments.Count > 0)
            {
                var folderName = prefixedBaseName;
                var candidateFolder = _pathSafetyService.GetUniquePath(destinationPath, folderName);
                outputDir = _pathSafetyService.EnsureSafePathLength(candidateFolder);
                Directory.CreateDirectory(outputDir);
            }

            var outputs = new List<string>();
            string? txtPath = null;
            var savedAsMsg = false;
            if (attachmentChoice.IncludeEmail)
            {
                if (selection.SaveEmailAsMsg && item.SaveAsOutlookMessage is not null)
                {
                    var msgFileName = $"{prefixedBaseName}.msg";
                    txtPath = overwriteExisting
                        ? Path.Combine(outputDir, msgFileName)
                        : _pathSafetyService.GetUniquePath(outputDir, msgFileName);
                    txtPath = _pathSafetyService.EnsureSafePathLength(txtPath);
                    try
                    {
                        item.SaveAsOutlookMessage(txtPath);
                        savedAsMsg = true;
                        outputs.Add(txtPath);
                    }
                    catch (Exception msgException)
                    {
                        var (msgUserMessage, _) = ErrorCatalog.Lookup(AppErrorId.TxtExportFailed);
                        throw new AppException(AppErrorId.TxtExportFailed, msgUserMessage, $"Failed to save email as .msg: {txtPath}", msgException);
                    }
                }
                else
                {
                    var txtFileName = $"{prefixedBaseName}.txt";
                    txtPath = overwriteExisting
                        ? Path.Combine(outputDir, txtFileName)
                        : _pathSafetyService.GetUniquePath(outputDir, txtFileName);
                    txtPath = _pathSafetyService.EnsureSafePathLength(txtPath);
                    var txtPayload = BuildTextPayload(item);
                    await File.WriteAllTextAsync(txtPath, txtPayload, Encoding.UTF8, cancellationToken);
                    outputs.Add(txtPath);
                }
            }
            if (selectedAttachments.Count > 0)
            {
                foreach (var attachment in selectedAttachments)
                {
                    var safeName = _pathSafetyService.SanitizeName(attachment.FileName, "attachment.bin");
                    var attachmentPath = overwriteExisting
                        ? Path.Combine(outputDir, safeName)
                        : _pathSafetyService.GetUniquePath(outputDir, safeName);
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
                    usedAiName = aiSubject is not null,
                    aiModelAvailable = settings.UseLocalAiFolderNaming,
                    duplicateSubjectDetected = existingMatch is not null,
                    overwroteExistingFolder = overwriteExisting,
                    savedAsMsg,
                },
                cancellationToken);

            var emailSavedText = savedAsMsg ? "Saved email (.msg) successfully." : "Saved email text successfully.";
            var userMessage = attachmentChoice.IncludeEmail && selectedAttachments.Count > 0
                ? $"Saved the email{(savedAsMsg ? " (.msg)" : string.Empty)} and {selectedAttachments.Count} selected attachment(s)."
                : attachmentChoice.IncludeEmail
                    ? emailSavedText
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

    private sealed record ExistingSubjectMatch(string Name, string FullPath, bool IsDirectory);

    private static ExistingSubjectMatch? FindExistingSubjectMatch(string destinationPath, string normalizedSubject)
    {
        if (string.IsNullOrWhiteSpace(normalizedSubject) || !Directory.Exists(destinationPath))
        {
            return null;
        }

        var candidates = Directory.GetDirectories(destinationPath)
            .Select(dir => (Name: Path.GetFileName(dir), FullPath: dir, IsDirectory: true))
            .Concat(Directory.GetFiles(destinationPath, "*.txt")
                .Select(file => (Name: Path.GetFileNameWithoutExtension(file), FullPath: file, IsDirectory: false)))
            .Concat(Directory.GetFiles(destinationPath, "*.msg")
                .Select(file => (Name: Path.GetFileNameWithoutExtension(file), FullPath: file, IsDirectory: false)));

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Name))
            {
                continue;
            }

            var withoutDate = DatePrefixRegex.Replace(candidate.Name, string.Empty);
            var isMatch = string.Equals(withoutDate, normalizedSubject, StringComparison.OrdinalIgnoreCase)
                || withoutDate.StartsWith(normalizedSubject + "_-_", StringComparison.OrdinalIgnoreCase)
                || withoutDate.EndsWith("_" + normalizedSubject, StringComparison.OrdinalIgnoreCase);

            if (isMatch)
            {
                return new ExistingSubjectMatch(candidate.Name, candidate.FullPath, candidate.IsDirectory);
            }
        }

        return null;
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

    private async Task<string?> TryGetAiNameAsync(SettingsModel settings, OutlookItemSelection item, CancellationToken cancellationToken)
    {
        if (!settings.UseLocalAiFolderNaming)
        {
            return null;
        }

        return await _aiFolderNameService.SuggestFolderNameAsync(item.Subject, item.BodyText ?? string.Empty, cancellationToken);
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
