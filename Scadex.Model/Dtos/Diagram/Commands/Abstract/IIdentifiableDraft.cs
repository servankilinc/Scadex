namespace Scadex.Model.Dtos.Diagram.Commands.Abstract;

/// <summary>
/// Diyagram taslaklarinin ortak sozlesmesi: hepsi bir <c>Id</c> tasir ve bu Id'yi
/// ISTEMCI uretir (UUIDv7).
///
/// Ayri bir arayuz olmasinin sebebi <see cref="EntityDelta{T}"/>: aile fark
/// etmeksizin ayni tutarlilik kurallarini (ayni Id iki kez, hem guncelleniyor hem
/// siliniyor) tek yerde uygulayabiliyor.
/// </summary>
public interface IIdentifiableDraft
{
    Guid Id { get; set; }
}
