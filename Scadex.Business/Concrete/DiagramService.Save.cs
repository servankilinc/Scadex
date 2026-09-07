using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Diagram.Commands;

namespace Scadex.Business.Concrete;

public partial class DiagramService
{
    /// <summary>
    /// Diyagram editorunun toplu kaydetme yolu — kod tabanindaki ILK gercek
    /// cok-varlikli transaction.
    ///
    /// Generic CRUD sablonunun <c>*AndSaveAsync</c> konvansiyonu burada BILEREK
    /// kirilir: o metotlarin her biri kendi <c>SaveChanges</c>'ini cagirir ve tek bir
    /// kaydetme icin sekiz ayri commit uretirdi.
    ///
    /// Bu metodun kullandigi ic adimlar <c>DiagramService.SaveInternals.cs</c>'te.
    ///
    /// <b>Basarida VERI DONMEZ.</b> Diyagramdaki her satirin — cihaz, kablo, not,
    /// pin ve kanal dahil — Guid'ini istemci uretiyor, dolayisiyla ne kimlik
    /// haritasi ne de sayac gerekiyor. Kaydetme atomik oldugu icin bos 200 tek
    /// basina "gonderdigim her sey kalici" demektir.
    /// </summary>
    public async Task<Result> SaveAsync(Guid cabinetId, DiagramSaveRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for DiagramSaveRequest");

        var cabinetExists = await _unitOfWork.Cabinets.IsExistAsync(
            where: c => c.Id == cabinetId && c.IsActive,
            cancellationToken: cancellationToken);

        if (!cabinetExists)
            return Result.NotFound(description: "Kabin bulunamadi veya pasif durumda");

        // Bos gonderi: transaction bile acilmaz. Kaydet dugmesi bos bir gunlukle
        // tetiklendiginde bunu 400 ile cezalandirmak yalnizca gurultu uretir.
        if (request.IsEmpty)
            return Result.Success();

        var context = await LoadSaveContextAsync(cabinetId, request, cancellationToken);

        // Referans dogrulamalari YAZMADAN ONCE yapilir: transaction'i acip sonra
        // geri almak yerine hic acmamak, kilit suresini de log gurultusunu de azaltir.
        var errors = ValidateReferences(request, context);
        if (errors.Count > 0)
            return Result.Validation(errors, description: "Diyagram kaydetme referanslari gecersiz");

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // ---- FAZ 1: SILMELER ----
            // Silmeler ve yazmalar AYRI SaveChanges'lerde akitilir. Sebep filtreli
            // unique index: IX_Connection_SourcePinId_TargetPinId "WHERE IsDeleted = 0"
            // ile calisir. Kullanici bir kabloyu silip AYNI iki pin arasina yenisini
            // cizdiginde (cizdi-vazgecti-yeniden cizdi, editorde siradan bir dizi),
            // silme bir UPDATE, olusturma bir INSERT'tur; tek batch'te EF'in sirasi
            // garanti degildir ve INSERT once giderse index ihlali 500 doner.
            ApplyDeletions(request, context);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // ---- FAZ 2: OLUSTURMALAR + GUNCELLEMELER ----
            ApplyUpserts(cabinetId, request, context);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result.Success();
        }
        catch
        {
            // Yutulmaz, yeniden firlatilir: global ExceptionHandleMiddleware yigini
            // loglayip ProblemDetails uretiyor. Burada Result.Failure'a cevirmek,
            // beklenmedik bir DB hatasinin izini silerdi.
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
