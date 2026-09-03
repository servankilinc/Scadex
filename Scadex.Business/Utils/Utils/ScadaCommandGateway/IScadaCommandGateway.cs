using CabinetOs.Model.Dtos.Scada.Commands;
using static CabinetOs.Model.Enums.EntityEnums;

namespace CabinetOs.Business.Utils.ScadaCommandGateway;

public interface IScadaCommandGateway
{
    Task<ScadaCommandOutcome> SendAsync(string baseUrl, ScadaCommandEnvelope envelope, TimeSpan timeout);
}

/// <summary>
/// SCADA cagrisinin sonucu. <see cref="Message"/> operatore gosterilecek tek teshis metnidir ve <c>DeviceCommand.ResultMessage</c>'a yazilir.
/// </summary>
public readonly record struct ScadaCommandOutcome(CommandStatus Status, string? Message);
