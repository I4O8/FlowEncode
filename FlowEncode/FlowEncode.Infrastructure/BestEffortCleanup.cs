namespace FlowEncode.Infrastructure;

internal static class BestEffortCleanup
{
    public static void DeleteFile(
        string? path,
        string description,
        Action<string>? onFailure = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        ExecuteWithRetry(
            shouldSkip: () => !File.Exists(path),
            action: () => File.Delete(path),
            description,
            onFailure);
    }

    public static void DeleteDirectoryRecursively(
        string? directory,
        string description,
        Action<string>? onFailure = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        ExecuteWithRetry(
            shouldSkip: () => !Directory.Exists(directory),
            action: () => Directory.Delete(directory, recursive: true),
            description,
            onFailure);
    }

    public static void DeleteDirectoryIfEmpty(
        string? directory,
        Action<string>? onFailure = null)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory, recursive: false);
            }
        }
        catch (Exception ex)
        {
            onFailure?.Invoke(
                $"Failed to delete empty directory '{directory}'. {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ExecuteWithRetry(
        Func<bool> shouldSkip,
        Action action,
        string description,
        Action<string>? onFailure)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (shouldSkip())
                {
                    return;
                }

                action();
                return;
            }
            catch (Exception ex) when (attempt < 2)
            {
                lastError = ex;
                Thread.Sleep(150 * (attempt + 1));
            }
            catch (Exception ex)
            {
                lastError = ex;
                break;
            }
        }

        if (lastError is not null)
        {
            onFailure?.Invoke(
                $"Failed to delete {description}. {lastError.GetType().Name}: {lastError.Message}");
        }
    }
}
