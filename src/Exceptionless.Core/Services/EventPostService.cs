using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues.Models;
using Foundatio.Queues;
using Foundatio.Storage;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services {
    public class EventPostService {
        private readonly IQueue<EventPost> _queue;
        private readonly IFileStorage _storage;
        private readonly ILogger _logger;

        public EventPostService(IQueue<EventPost> queue, IFileStorage storage, ILoggerFactory loggerFactory) {
            _queue = queue;
            _storage = storage;
            _logger = loggerFactory.CreateLogger<EventPostService>();
        }

        public async Task<string> EnqueueAsync(EventPost data, Stream stream, CancellationToken cancellationToken = default) {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (data.ShouldArchive) {
                data.FilePath = GetArchivePath(SystemClock.UtcNow, data.ProjectId, $"{Guid.NewGuid():N}.json");
            } else {
                string fileId = Guid.NewGuid().ToString("N");
                data.FilePath = Path.Combine("q", fileId.Substring(0, 3), $"{fileId}.json");
            }

            var saveTask = data.ShouldArchive ? _storage.SaveObjectAsync(data.FilePath, (EventPostInfo)data, cancellationToken) : Task.FromResult(true);
            var savePayloadTask = _storage.SaveFileAsync(Path.ChangeExtension(data.FilePath, ".payload"), stream, cancellationToken);

            if (!await saveTask.AnyContext()) {
                using (_logger.BeginScope(new ExceptionlessState().Organization(data.OrganizationId).Property(nameof(EventPostInfo), data)))
                    _logger.LogError("Unable to save event post info");

                await savePayloadTask.AnyContext();
                return null;
            }

            if (!await savePayloadTask.AnyContext()) {
                using (_logger.BeginScope(new ExceptionlessState().Organization(data.OrganizationId).Property(nameof(EventPostInfo), data)))
                    _logger.LogError("Unable to save event post payload");

                return null;
            }

            return await _queue.EnqueueAsync(data).AnyContext();
        }

        public async Task<byte[]> GetEventPostPayloadAsync(string path, CancellationToken cancellationToken = default) {
            if (String.IsNullOrEmpty(path))
                return null;

            byte[] data;
            try {
                data = await _storage.GetFileContentsRawAsync(path).AnyContext();
            } catch (Exception ex) {
                _logger.LogError(ex, "Error retrieving event post payload: {Path}.", path);
                return null;
            }

            return data;
        }

        public async Task<bool> CompleteEventPostAsync(string path, string projectId, DateTime created, bool shouldArchive = true) {
            if (String.IsNullOrEmpty(path))
                return false;

            // don't move files that are already in the archive
            if (path.StartsWith("archive"))
                return true;

            try {
                if (shouldArchive) {
                    string archivePath = GetArchivePath(created, projectId, Path.GetFileName(path));
                    var renameTask = _storage.RenameFileAsync(path, archivePath);
                    var renamePayLoadTask = _storage.RenameFileAsync(Path.ChangeExtension(path, ".payload"), Path.ChangeExtension(archivePath, ".payload"));
                    return await renameTask.AnyContext() && await renamePayLoadTask.AnyContext();
                }

                return await _storage.DeleteFileAsync(Path.ChangeExtension(path, ".payload")).AnyContext();
            } catch (Exception ex) {
                _logger.LogError(ex, "Error archiving event post data {Path}.", path);
                return false;
            }
        }

        private string GetArchivePath(DateTime createdUtc, string projectId, string fileName) {
            return Path.Combine("archive", createdUtc.ToString("yy"), createdUtc.ToString("MM"), createdUtc.ToString("dd"), createdUtc.ToString("HH"), createdUtc.ToString("mm"), projectId, fileName);
        }
    }
}