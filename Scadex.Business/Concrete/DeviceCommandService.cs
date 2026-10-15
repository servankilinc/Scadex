using AutoMapper;
using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.DiagramNotifier;
using Scadex.Business.Utils.OutputPolarity;
using Scadex.Business.Utils.ScadaCommandGateway;
using Scadex.Business.Utils.ScadaObserver;
using Scadex.Core.Utils.HttpContextManager;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;

namespace Scadex.Business.Concrete;

public partial class DeviceCommandService : IDeviceCommandService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IMapper _mapper;
    private readonly IScadaCommandGateway _scadaCommandGateway;
    private readonly IDiagramNotifier _notifier;
    private readonly IHttpContextManager _httpContextManager;
    private readonly ILogger<DeviceCommandService> _logger;
    private readonly IOutputPolarityResolver _polarityResolver;
    private readonly IEnumerable<IScadaEventObserver> _observers;
    private readonly ICabinetStatusService _cabinetStatusService;

    public DeviceCommandService(IUnitOfWork unitOfWork, IValidationService validationService, IMapper mapper, IScadaCommandGateway scadaCommandGateway, IDiagramNotifier notifier, IHttpContextManager httpContextManager, ILogger<DeviceCommandService> logger, IOutputPolarityResolver polarityResolver, IEnumerable<IScadaEventObserver> observers, ICabinetStatusService cabinetStatusService)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _mapper = mapper;
        _scadaCommandGateway = scadaCommandGateway;
        _notifier = notifier;
        _httpContextManager = httpContextManager;
        _logger = logger;
        _polarityResolver = polarityResolver;
        _observers = observers;
        _cabinetStatusService = cabinetStatusService;
    }
}