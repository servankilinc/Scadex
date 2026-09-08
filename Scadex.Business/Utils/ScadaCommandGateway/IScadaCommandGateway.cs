using Scadex.Model.Dtos.Scada.Commands;

namespace Scadex.Business.Utils.ScadaCommandGateway;

public interface IScadaCommandGateway
{
    public const string HttpClientName = "http_client_scada";
    Task<ScadaCommandResponse> SendAsync(string baseUrl, ScadaCommandEnvelope envelope, TimeSpan timeout);
}
