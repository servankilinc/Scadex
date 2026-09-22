using Scadex.DataAccess.UoW;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.OutputPolarity;

/// <inheritdoc />
public class OutputPolarityResolver : IOutputPolarityResolver
{
    private readonly IUnitOfWork _unitOfWork;

    public OutputPolarityResolver(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    /// <inheritdoc />
    public async Task<PolarityResolution> ResolveAsync(Guid ioChannelId, CancellationToken cancellationToken = default)
    {
        var resolutions = await ResolveManyAsync([ioChannelId], cancellationToken);
        return resolutions[ioChannelId];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, PolarityResolution>> ResolveManyAsync(IReadOnlyCollection<Guid> ioChannelIds, CancellationToken cancellationToken = default)
    {
        var channelIds = ioChannelIds.Distinct().ToList();
        if (channelIds.Count == 0)
            return new Dictionary<Guid, PolarityResolution>();

        // 1) Kanalların NO/NC pinleri tek sorguda
        var nc_no_pins = await _unitOfWork.Pins.GetAllAsync(
            select: p => new { p.Id, p.IoChannelId, p.Function },
            where: p => p.IoChannelId != null && channelIds.Contains(p.IoChannelId.Value) && (p.Function == PinFunction.NO || p.Function == PinFunction.NC),
            cancellationToken: cancellationToken
        ) ?? [];

        var pinsByChannel = nc_no_pins.GroupBy(p => p.IoChannelId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        // 2) Yalnızca hem NO hem NC pini olan kanalların kabloları gerekir; onlar da tek sorguda
        var ambiguousPinIds = pinsByChannel.Values
            .Where(pins => pins.Select(p => p.Function).Distinct().Count() > 1)
            .SelectMany(pins => pins.Select(p => p.Id))
            .ToList();

        var wired_nc_no_pin_ids = new HashSet<Guid>();
        if (ambiguousPinIds.Count > 0)
        {
            var connections = await _unitOfWork.Connections.GetAllAsync(
                select: c => new { c.SourcePinId, c.TargetPinId },
                where: c => ambiguousPinIds.Contains(c.SourcePinId) || ambiguousPinIds.Contains(c.TargetPinId),
                cancellationToken: cancellationToken
            ) ?? [];

            foreach (var connection in connections)
            {
                wired_nc_no_pin_ids.Add(connection.SourcePinId);
                wired_nc_no_pin_ids.Add(connection.TargetPinId);
            }
        }

        var result = new Dictionary<Guid, PolarityResolution>(channelIds.Count);
        foreach (var channelId in channelIds)
        {
            // NC/NO barındırmayan kanal: LED, duz dijital cikis vs. olabilir
            if (!pinsByChannel.TryGetValue(channelId, out var pins))
            {
                result[channelId] = PolarityResolution.Resolved(null);
                continue;
            }

            // Kanalın NC/NO pinlerinden sadece biri var
            var distinct = pins.Select(p => p.Function).Distinct().ToList();
            if (distinct.Count == 1)
            {
                result[channelId] = PolarityResolution.Resolved(distinct[0]);
                continue;
            }

            // Hem NO hem NC pini var -> kablolu olanlara bakılır. Yalnızca biri kabloluysa o pin ile devam edilir.
            var wiredFunctions = pins.Where(p => wired_nc_no_pin_ids.Contains(p.Id)).Select(p => p.Function).Distinct().ToList();
            result[channelId] = wiredFunctions.Count switch
            {
                1 => PolarityResolution.Resolved(wiredFunctions[0]),
                > 1 => PolarityResolution.Reject($"Hem NO hem NC pini kablolu; hangisinin yükü taşıdığı belirsiz. Kullanılmayan kabloyu kaldırın. Kanal {channelId}"),
                _ => PolarityResolution.Reject($"Output pini çözülemedi. Kanal {channelId}")
            };
        }

        return result;
    }
}
