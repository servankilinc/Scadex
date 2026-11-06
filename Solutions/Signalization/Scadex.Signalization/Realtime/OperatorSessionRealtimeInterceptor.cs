using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Scadex.Signalization.Model.Entities;

namespace Scadex.Signalization.Realtime;

/// <summary>
/// <see cref="OperatorSession"/> eklenip/guncellendigi her kayittan SONRA <see cref="ISignalizationNotifier.OperatorSessionChangedAsync"/> cagirir.
/// </summary>
/// <remarks>
/// <para>
/// Neden motorun icinde degil de burada: oturumu degistiren ~15 ayri <c>SaveChangesAsync</c> noktasi var (kanal, kart, siren,
/// zamanlayici). Her birine yayin satiri eklemek, yenisi eklendiginde unutulacak bir kural olurdu; interceptor hepsini tek yerden yakalar.
/// </para>
/// <para>
/// Yayin kayit BASARILI olduktan sonra yapilir — yazilmamis bir durumu istemciye duyurmamak icin. Yeni oturumun <c>Id</c>'si
/// ancak kayittan sonra olusur; bu yuzden varliklar kayit ONCESI toplanir, alanlar kayit SONRASI okunur.
/// </para>
/// <para>
/// Singleton'dir (cekirdek interceptor'lari gibi): kayit basina durum <see cref="ConditionalWeakTable{TKey,TValue}"/> ile context'e
/// baglanir, context toplaninca kendiliginden duser.
/// </para>
/// </remarks>
public sealed class OperatorSessionRealtimeInterceptor : SaveChangesInterceptor
{
    private readonly ISignalizationNotifier _notifier;
    private readonly ConditionalWeakTable<DbContext, List<OperatorSession>> _pending = new();

    public OperatorSessionRealtimeInterceptor(ISignalizationNotifier notifier) => _notifier = notifier;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        // Senkron yolda beklenemez; notifier zaten hata firlatmaz. Motor yalnizca async kayit kullanir.
        _ = PublishAsync(eventData.Context, CancellationToken.None);
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await PublishAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Discard(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Collect(DbContext? context)
    {
        if (context == null)
            return;

        var sessions = context.ChangeTracker.Entries<OperatorSession>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .Select(e => e.Entity)
            .ToList();

        if (sessions.Count > 0)
            _pending.AddOrUpdate(context, sessions);
        else
            _pending.Remove(context);
    }

    private void Discard(DbContext? context)
    {
        if (context != null)
            _pending.Remove(context);
    }

    private async Task PublishAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context == null || !_pending.TryGetValue(context, out var sessions))
            return;

        _pending.Remove(context);

        // Ayni kayitta ayni oturum iki kez gelmez (ChangeTracker kimlik basina tek giris tutar); yine de Id'ye gore tekillestirilir.
        foreach (var session in sessions.DistinctBy(s => s.Id))
        {
            var message = new OperatorSessionChangedMessage(session.Id, session.CabinetId, session.Status, IsOpen: session.EndedAtUtc == null);
            await _notifier.OperatorSessionChangedAsync(message, cancellationToken);
        }
    }
}
