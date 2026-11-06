using Microsoft.EntityFrameworkCore;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Signalization.Model.Dtos.Session.Commands;
using Scadex.Signalization.Model.Utils;

namespace Scadex.Signalization.Services.Concrete;

/// <summary>
/// Manuel elle kapatma: servis yalnızca doğrular ve talebi kabin şeridine bırakır; kapatmayı motor yazar
/// (<c>OperatorSessionEngine.ManualClose.cs</c>). Böylece aynı kabinin SCADA olaylarıyla yarış olmaz.
/// </summary>
public partial class OperatorSessionService
{
    /// <inheritdoc />
    public async Task<Result> CloseAsync(long sessionId, Guid userId, OperatorSessionCloseRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for OperatorSessionCloseRequest");

        // Hızlı ret; asıl karar motorda yeniden verilir (talep kuyruktayken oturum kapanmış olabilir).
        var session = await _db.OperatorSessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new { s.CabinetId, s.EndedAtUtc })
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
            return Result.NotFound(message: "İşlem bulunamadı.");

        if (session.EndedAtUtc != null)
            return AlreadyClosed();

        var work = new ManualCloseWork(session.CabinetId, sessionId, userId, request.Reason);
        _queue.Enqueue(work);

        // Otomatik kilitleme SCADA'yı bekleyebilir; uç, elle komut ucu gibi motor bitirene kadar bloklar.
        return await work.Completion.Task.WaitAsync(cancellationToken);
    }

    /// <summary> 400: <c>Result.Failure</c> 500'e eşlenir, kapanmış işlem bir istemci hatasıdır. Motor da aynı yanıtı kullanır. </summary>
    internal static Result AlreadyClosed()
    {
        const string message = "İşlem zaten kapanmış.";
        return Result.Validation(new Dictionary<string, string[]> { ["SessionId"] = [message] }, message: message);
    }
}
