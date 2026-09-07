using Scadex.Model.Dtos.Scada.Commands;

namespace Scadex.Business.Utils.ScadaCommandGateway;

public interface IScadaCommandGateway
{
    Task<ScadaCommandResponse> SendAsync(string baseUrl, ScadaCommandEnvelope envelope, TimeSpan timeout);
}
