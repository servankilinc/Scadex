using Scadex.Model.Dtos.IoChannel.Queries;

namespace Scadex.Business.Abstract;

public interface IIoChannelService
{
    /// <summary> Çıkış kanallarının mantıksal durumu ("yük açık mı") sorgular </summary>
    Task<IReadOnlyDictionary<Guid, OutputChannelStateDto>> GetOutputStatesAsync(IReadOnlyCollection<Guid> ioChannelIds, CancellationToken cancellationToken = default);
}
