using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LoxNet;

/// <summary>
/// Helper methods for managing file locks with stale lock cleanup.
/// </summary>
internal static class FileLockHelper
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StaleLockAge = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Acquires an exclusive file lock, cleaning up stale locks if necessary.
    /// </summary>
    /// <param name="lockFilePath">Path to the lock file.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A disposable <see cref="FileStream"/> representing the lock.</returns>
    public static async Task<FileStream> AcquireLockAsync(
        string lockFilePath,
        ILogger? logger,
        CancellationToken cancellationToken = default)
    {
        CleanupStaleLock(lockFilePath, logger);

        var startTime = DateTime.UtcNow;
        while (true)
        {
            try
            {
                var lockStream = new FileStream(
                    lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    useAsync: true);

                logger?.LogDebug("[StructureLoad] Acquired file lock: {LockFile}", lockFilePath);
                return lockStream;
            }
            catch (IOException) when (DateTime.UtcNow - startTime < LockTimeout)
            {
                // Lock held by another process, wait and retry
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                logger?.LogError(ex, "[StructureLoad] Failed to acquire lock after {Timeout}s: {LockFile}",
                    LockTimeout.TotalSeconds, lockFilePath);
                throw new TimeoutException(
                    $"Failed to acquire file lock within {LockTimeout.TotalSeconds} seconds: {lockFilePath}", ex);
            }
        }
    }

    /// <summary>
    /// Cleans up a stale lock file if it's older than the configured threshold.
    /// </summary>
    private static void CleanupStaleLock(string lockFilePath, ILogger? logger)
    {
        try
        {
            if (!File.Exists(lockFilePath))
                return;

            var fileInfo = new FileInfo(lockFilePath);
            var age = DateTime.UtcNow - fileInfo.LastWriteTimeUtc;

            if (age > StaleLockAge)
            {
                logger?.LogWarning("[StructureLoad] Deleting stale lock file (age: {Age}): {LockFile}",
                    age, lockFilePath);
                File.Delete(lockFilePath);
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[StructureLoad] Failed to cleanup stale lock: {LockFile}", lockFilePath);
            // Non-critical, continue
        }
    }

    /// <summary>
    /// Releases and deletes a lock file.
    /// </summary>
    public static void ReleaseLock(FileStream lockStream, ILogger? logger)
    {
        var lockFilePath = lockStream.Name;
        try
        {
            lockStream.Dispose();
            if (File.Exists(lockFilePath))
            {
                File.Delete(lockFilePath);
                logger?.LogDebug("[StructureLoad] Released and deleted lock file: {LockFile}", lockFilePath);
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[StructureLoad] Failed to delete lock file: {LockFile}", lockFilePath);
            // Non-critical
        }
    }
}
