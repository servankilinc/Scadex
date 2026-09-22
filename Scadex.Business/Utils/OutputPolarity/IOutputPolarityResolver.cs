namespace Scadex.Business.Utils.OutputPolarity;

/// <summary> Output kanalının NO/NC kutbunu şemanın ŞU ANKİ kablolamasından çözer. </summary>
public interface IOutputPolarityResolver
{
    Task<PolarityResolution> ResolveAsync(Guid ioChannelId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, PolarityResolution>> ResolveManyAsync(IReadOnlyCollection<Guid> ioChannelIds, CancellationToken cancellationToken = default);
}
