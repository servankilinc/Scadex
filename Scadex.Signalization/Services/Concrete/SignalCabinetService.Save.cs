using Microsoft.EntityFrameworkCore;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Signalization.Dtos.Config.Commands;
using Scadex.Signalization.Entities;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Signalization.Services.Concrete;

/// <summary> Yapilandirma agacinin tek yazim yolu. </summary>
public partial class SignalCabinetService
{
    /// <inheritdoc />
    public async Task<Result> SaveAsync(Guid cabinetId, SignalCabinetSaveRequest request, CancellationToken cancellationToken = default)
    {
        // 1) Bicim dogrulamasi
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for SignalCabinetSaveRequest");

        var cabinet = await _unitOfWork.Cabinets.GetAsync(select: c => new { c.Id, c.IsActive }, where: c => c.Id == cabinetId, cancellationToken: cancellationToken);
        if (cabinet == null || !cabinet.IsActive)
            return Result.NotFound(message: "Kabin bulunamadı veya pasif durumda.");

        // 2) Referans dogrulamasi — transaction ACILMADAN once (acip geri almak yerine hic acmamak).
        var errors = await ValidateReferencesAsync(cabinetId, request, cancellationToken);
        if (errors.Count > 0)
            return Result.Validation(errors, message: errors.Values.First()[0]);

        // 3) Yazim: tek transaction
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var config = await _db.Cabinets.FirstOrDefaultAsync(c => c.CabinetId == cabinetId, cancellationToken);
        if (config == null)
        {
            config = new SignalCabinet { CabinetId = cabinetId };
            _db.Cabinets.Add(config);
        }

        config.IsEnabled = request.IsEnabled;
        config.SirenIoChannelId = request.SirenIoChannelId;
        config.SirenDurationSec = request.SirenDurationSec;
        config.EntrySnapshotCount = request.EntrySnapshotCount;
        config.EntrySnapshotIntervalMs = request.EntrySnapshotIntervalMs;
        config.AwaitingCardTimeoutSec = request.AwaitingCardTimeoutSec;
        config.SessionMaxDurationMin = request.SessionMaxDurationMin;

        var existingOuter = await _db.OuterDoors.Where(d => d.CabinetId == cabinetId).ToListAsync(cancellationToken);
        var existingOuterIds = existingOuter.Select(d => d.Id).ToList();
        var existingInner = await _db.InnerDoors.Where(i => existingOuterIds.Contains(i.OuterDoorId)).ToListAsync(cancellationToken);

        var requestedOuterIds = request.OuterDoors.Select(d => d.Id).ToHashSet();
        var requestedInnerIds = request.OuterDoors.SelectMany(d => d.InnerDoors).Select(i => i.Id).ToHashSet();

        // Gövdede olmayan kapi pasife alinir; fiziksel silme yok (oturum olaylari kapi kimligini gosterir).
        foreach (var door in existingOuter.Where(d => !requestedOuterIds.Contains(d.Id)))
            door.IsActive = false;
        foreach (var door in existingInner.Where(i => !requestedInnerIds.Contains(i.Id)))
            door.IsActive = false;

        foreach (var outerDraft in request.OuterDoors)
        {
            var outer = existingOuter.FirstOrDefault(d => d.Id == outerDraft.Id);
            if (outer == null)
            {
                outer = new SignalOuterDoor { Id = outerDraft.Id, CabinetId = cabinetId };
                _db.OuterDoors.Add(outer);
            }

            outer.Name = outerDraft.Name.Trim();
            outer.SwitchIoChannelId = outerDraft.SwitchIoChannelId;
            outer.SwitchOpenValue = outerDraft.SwitchOpenValue.Trim();
            outer.CameraId = outerDraft.CameraId;
            outer.IsActive = true;

            foreach (var innerDraft in outerDraft.InnerDoors)
            {
                var inner = existingInner.FirstOrDefault(i => i.Id == innerDraft.Id);
                if (inner == null)
                {
                    inner = new SignalInnerDoor { Id = innerDraft.Id };
                    _db.InnerDoors.Add(inner);
                }

                // Ic kapi ayni kabin icinde baska bir dis kapinin ardina tasinabilir.
                inner.OuterDoorId = outer.Id;
                inner.Name = innerDraft.Name.Trim();
                inner.AuthorityId = innerDraft.AuthorityId;
                inner.SwitchIoChannelId = innerDraft.SwitchIoChannelId;
                inner.SwitchOpenValue = innerDraft.SwitchOpenValue.Trim();
                inner.LockIoChannelId = innerDraft.LockIoChannelId;
                inner.UnlockTurnsOn = innerDraft.UnlockTurnsOn;
                inner.IsActive = true;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Cekirdege referanslar FK olmadigi icin butunluk burada kurulur. Anahtarlari PascalCase gövde yolu
    /// (<c>OuterDoors[0].InnerDoors[1].LockIoChannelId</c>) — FluentValidation hatalariyla ayni bicim.
    /// </summary>
    private async Task<Dictionary<string, string[]>> ValidateReferencesAsync(Guid cabinetId, SignalCabinetSaveRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string key, string message) => errors.TryAdd(key, [message]);

        // Kabinin kanallari (silinmemis, aktif kartlarda)
        var channels = await _unitOfWork.IoChannels.GetAllAsync(
            select: c => new { c.Id, c.Direction, DeviceIsActive = c.Device!.IsActive },
            where: c => c.CabinetId == cabinetId,
            cancellationToken: cancellationToken) ?? [];
        var channelById = channels.Where(c => c.DeviceIsActive).ToDictionary(c => c.Id, c => c.Direction);

        var cameraIds = (await _unitOfWork.Cameras.GetAllAsync(select: c => c.Id, where: c => c.CabinetId == cabinetId, cancellationToken: cancellationToken) ?? []).ToHashSet();
        var activeAuthorityIds = (await _db.Authorities.AsNoTracking().Where(a => a.IsActive).Select(a => a.Id).ToListAsync(cancellationToken)).ToHashSet();

        void CheckChannel(string key, Guid channelId, PinDirection expected, string label)
        {
            if (!channelById.TryGetValue(channelId, out var direction))
                Add(key, $"{label} bu kabinde bulunamadı (ya da kartı silinmiş).");
            else if (direction != expected)
                Add(key, $"{label} {(expected == PinDirection.Input ? "giriş" : "çıkış")} kanalı olmalı.");
        }

        // Siren: kabinin ortak cikis kanali
        if (request.SirenIoChannelId is Guid sirenId)
            CheckChannel(nameof(request.SirenIoChannelId), sirenId, PinDirection.Output, "Siren kanalı");

        // Bir anahtar ya da kilit kanali yalnizca bir kapida kullanilabilir; siren hicbir kilitle ayni olamaz.
        var usedChannels = new Dictionary<Guid, string>();
        if (request.SirenIoChannelId is Guid siren)
            usedChannels[siren] = "kabin sireni";

        void CheckUnique(string key, Guid channelId, string doorName)
        {
            if (usedChannels.TryGetValue(channelId, out var owner))
                Add(key, $"Bu kanal zaten kullanılıyor: {owner}.");
            else
                usedChannels[channelId] = doorName;
        }

        var authorityOwner = new Dictionary<Guid, string>();

        for (int i = 0; i < request.OuterDoors.Count; i++)
        {
            var outer = request.OuterDoors[i];
            string outerKey = $"OuterDoors[{i}]";

            CheckChannel($"{outerKey}.SwitchIoChannelId", outer.SwitchIoChannelId, PinDirection.Input, "Dış kapı anahtar kanalı");
            CheckUnique($"{outerKey}.SwitchIoChannelId", outer.SwitchIoChannelId, outer.Name);

            if (outer.CameraId is Guid cameraId && !cameraIds.Contains(cameraId))
                Add($"{outerKey}.CameraId", "Kamera bu kabine ait değil.");

            for (int j = 0; j < outer.InnerDoors.Count; j++)
            {
                var inner = outer.InnerDoors[j];
                string innerKey = $"{outerKey}.InnerDoors[{j}]";

                if (!activeAuthorityIds.Contains(inner.AuthorityId))
                    Add($"{innerKey}.AuthorityId", "Kurum bulunamadı veya pasif durumda.");
                else if (authorityOwner.TryGetValue(inner.AuthorityId, out var ownerDoor))
                    // Kart okuma kurumdan tek kapiya cozulur: ayni kurumun ikinci kapisi belirsizlik olurdu.
                    Add($"{innerKey}.AuthorityId", $"Bu kurumun kabinde zaten bir iç kapısı var: {ownerDoor}.");
                else
                    authorityOwner[inner.AuthorityId] = inner.Name;

                CheckChannel($"{innerKey}.SwitchIoChannelId", inner.SwitchIoChannelId, PinDirection.Input, "İç kapı anahtar kanalı");
                CheckUnique($"{innerKey}.SwitchIoChannelId", inner.SwitchIoChannelId, $"{inner.Name} (anahtar)");

                CheckChannel($"{innerKey}.LockIoChannelId", inner.LockIoChannelId, PinDirection.Output, "Kilit kanalı");
                CheckUnique($"{innerKey}.LockIoChannelId", inner.LockIoChannelId, $"{inner.Name} (kilit)");
            }
        }

        // Kimlikler istemci uretir: baska bir kabine ait kapi kimligi bu kabine "tasinamaz" (sessizce baska kabinin
        // kapisini ele gecirirdi).
        var outerIds = request.OuterDoors.Select(d => d.Id).ToList();
        bool foreignOuter = await _db.OuterDoors.AnyAsync(d => outerIds.Contains(d.Id) && d.CabinetId != cabinetId, cancellationToken);
        if (foreignOuter)
            Add(nameof(request.OuterDoors), "Gönderilen bir dış kapı kimliği başka bir kabine ait.");

        var innerIds = request.OuterDoors.SelectMany(d => d.InnerDoors).Select(i => i.Id).ToList();
        bool foreignInner = await _db.InnerDoors.AnyAsync(i => innerIds.Contains(i.Id) && i.OuterDoor!.CabinetId != cabinetId, cancellationToken);
        if (foreignInner)
            Add(nameof(request.OuterDoors), "Gönderilen bir iç kapı kimliği başka bir kabine ait.");

        return errors;
    }
}
