namespace Ntk.Asterisk.Domain.Recording;

public interface IAudioRecordingStorage
{
    Task<string> SaveAsync(string relativePath, Stream audioStream, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
    Task<long> GetFileSizeAsync(string relativePath, CancellationToken cancellationToken = default);
    string GetFullPath(string relativePath);
}

public interface IRecordingMetadataRepository
{
    Task<AudioRecording?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<AudioRecording?> GetByUniqueIdAsync(string uniqueId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AudioRecording>> SearchAsync(
        string? serverId = null,
        string? extension = null,
        string? callerIdNum = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);
    Task AddAsync(AudioRecording recording, CancellationToken cancellationToken = default);
    Task UpdateAsync(AudioRecording recording, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AudioRecording>> GetExpiredRecordingsAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}
