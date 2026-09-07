using FluentValidation;
using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.DeviceCommand.Commands;

public class DeviceCommandSendRequest : IDto
{
    /// <summary> Şu an için sadece <see cref="DeviceCommandType.SetOutput"/> komutu gönderiliyor. Diğer komutlar henüz kullanılmıyor. </summary>
    public DeviceCommandType CommandType { get; set; }

    /// <summary>  Kanal bilgisi girilmeli çünkü tek komutu turu <see cref="DeviceCommandType.SetOutput"/> kullanılır ve o da her zaman bir cikis kanalini hedefler. </summary>
    public Guid? IoChannelId { get; set; }
    /// <summary>
    /// Gidecek degeri sunucu <c>Pin.Function</c>'daki NO/NC'den cozer ve buna uygun olarak komut gönderilir. Örn: <c>Pin.Function = NO</c> ise <c>Value = true</c> → röle kapanır, <c>false</c> → röle açılır. <br/>
    /// </summary>
    public bool? TurnOn { get; set; }
}

public class DeviceCommandSendRequestValidator : AbstractValidator<DeviceCommandSendRequest>
{
    private const int MaxValueLength = 64;

    public DeviceCommandSendRequestValidator()
    {
        RuleFor(v => v.CommandType).IsInEnum().WithMessage("Geçersiz komut türü");
        RuleFor(v => v.IoChannelId).NotEmpty().WithMessage("Komut için hedef kanal zorunlu");
        RuleFor(v => v.TurnOn).NotNull().WithMessage("Komut için aç/kapat bilgisi zorunlu");
    }
}
