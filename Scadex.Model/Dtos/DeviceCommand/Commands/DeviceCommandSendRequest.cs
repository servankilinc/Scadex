using FluentValidation;
using Scadex.Core.Model;

namespace Scadex.Model.Dtos.DeviceCommand.Commands;

/// <summary> Tek komut türü vardır: bir çıkış kanalını aç/kapat. </summary>
public class DeviceCommandSendRequest : IDto
{
    /// <summary> Hedef çıkış kanalı — komut her zaman bir <c>Output</c> kanalını hedefler. </summary>
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
        RuleFor(v => v.IoChannelId).NotEmpty().WithMessage("Komut için hedef kanal zorunlu");
        RuleFor(v => v.TurnOn).NotNull().WithMessage("Komut için aç/kapat bilgisi zorunlu");
    }
}
