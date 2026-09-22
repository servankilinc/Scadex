using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.OutputPolarity;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.IoChannel.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Concrete;

public class IoChannelService : IIoChannelService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IOutputPolarityResolver _polarityResolver;
    public IoChannelService(IUnitOfWork unitOfWork, IMapper mapper, IOutputPolarityResolver polarityResolver)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _polarityResolver = polarityResolver;
    }

    #region Get
    public async Task<Result<IoChannel>> GetAsync(Expression<Func<IoChannel, bool>> where, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.IoChannels.GetAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<IoChannel>.NotFound();
        return Result<IoChannel>.Success(result);
    }

    public async Task<Result<IoChannel>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.IoChannels.GetAsync(where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<IoChannel>.NotFound();
        return Result<IoChannel>.Success(result);
    }

    public async Task<Result<IoChannelDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.IoChannels.GetAsync<IoChannelDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<IoChannelDto>.NotFound();
        return Result<IoChannelDto>.Success(result);
    }
    #endregion

    #region List
    public async Task<Result<ICollection<IoChannel>>> GetListAsync(Expression<Func<IoChannel, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.IoChannels.GetAllAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<IoChannel>>.NotFound();
        return Result<ICollection<IoChannel>>.Success(result);
    }

    public async Task<Result<ICollection<IoChannel>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.IoChannels.GetAllAsync(filter: request?.Filter, sorts: request?.Sorts, tracking: false, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<IoChannel>>.NotFound();
        return Result<ICollection<IoChannel>>.Success(result);
    }

    public async Task<Result<ICollection<IoChannelDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.IoChannels.GetAllAsync<IoChannelDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<IoChannelDto>>.NotFound();
        return Result<ICollection<IoChannelDto>>.Success(result);
    }
    #endregion

    #region Output State
    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, OutputChannelStateDto>> GetOutputStatesAsync(IReadOnlyCollection<Guid> ioChannelIds, CancellationToken cancellationToken = default)
    {
        var channelIds = ioChannelIds.Distinct().ToList();
        if (channelIds.Count == 0)
            return new Dictionary<Guid, OutputChannelStateDto>();

        var channels = (await _unitOfWork.IoChannels.GetAllAsync(
            select: c => new { 
                c.Id, 
                c.CurrentValue, 
                c.ValueUpdatedAt 
            },
            where: c => 
                channelIds.Contains(c.Id) && 
                c.IsEnabled && 
                c.Direction == PinDirection.Output,
            cancellationToken: cancellationToken
        ) ?? []).ToDictionary(c => c.Id);

        // Polarity yalnızca değeri bilinen kanallar için çözülür: bilinmeyen değerin yorumu zaten "bilinmiyor"dur.
        var polarities = await _polarityResolver.ResolveManyAsync(
            channels.Values.Where(c => c.CurrentValue != null).Select(c => c.Id).ToList(),
            cancellationToken
        );

        var result = new Dictionary<Guid, OutputChannelStateDto>(channelIds.Count);
        foreach (var channelId in channelIds)
        {
            if (!channels.TryGetValue(channelId, out var channel))
            {
                result[channelId] = new OutputChannelStateDto { IoChannelId = channelId };
                continue;
            }

            bool? isOn = null;
            if (polarities.TryGetValue(channelId, out var polarity) && polarity.IsSuccess)
            {
                // Karta giden değer yalnızca "1"/"0"dır (DeviceCommandService.SendAsync); başka bir şey "bilinmiyor"dur.
                bool? physicalOn = channel.CurrentValue switch { "1" => true, "0" => false, _ => null };
                isOn = physicalOn is bool on ? polarity.ToLogical(on) : null;
            }

            result[channelId] = new OutputChannelStateDto
            {
                IoChannelId = channelId,
                CurrentValue = channel.CurrentValue,
                ValueUpdatedAt = channel.ValueUpdatedAt,
                IsOn = isOn
            };
        }

        return result;
    }
    #endregion
}
