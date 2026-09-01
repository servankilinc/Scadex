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
    public string? Value { get; set; }
}

public class DeviceCommandSendRequestValidator : AbstractValidator<DeviceCommandSendRequest>
{
    private const int MaxValueLength = 64;

    public DeviceCommandSendRequestValidator()
    {
        RuleFor(v => v.CommandType).IsInEnum().WithMessage("Geçersiz komut türü");
        RuleFor(v => v.IoChannelId).NotEmpty().WithMessage("Komut için hedef kanal zorunlu");
        RuleFor(v => v.Value).NotEmpty().WithMessage("Komut için değer zorunlu");
        RuleFor(v => v.Value).MaximumLength(MaxValueLength).WithMessage($"Değer en fazla {MaxValueLength} karakter olabilir");
    }
}
