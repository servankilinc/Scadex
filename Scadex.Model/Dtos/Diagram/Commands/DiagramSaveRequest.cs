using System.Text.Json.Serialization;
using Scadex.Core.Model;
using FluentValidation;
using Scadex.Model.Dtos.Diagram.Commands.Abstract;
using Scadex.Model.Dtos.Diagram.Commands.Items;

namespace Scadex.Model.Dtos.Diagram.Commands;


public class DiagramSaveRequest : IDto
{
    public EntityDelta<DeviceDraft> Devices { get; set; } = new();
    public EntityDelta<ConnectionDraft> Connections { get; set; } = new();
    public EntityDelta<DiagramAnnotationDraft> DiagramAnnotations { get; set; } = new();

    /// <summary> Iki liste de bossa bu aile icin hicbir is yapilmaz. </summary>
    [JsonIgnore]
    public bool IsEmpty => Devices.IsEmpty && Connections.IsEmpty && DiagramAnnotations.IsEmpty;
}

public class DiagramSaveRequestValidator : AbstractValidator<DiagramSaveRequest>
{
    public DiagramSaveRequestValidator()
    {
        RuleForEach(v => v.Devices.Upserted).SetValidator(new DeviceDraftValidator());
        RuleForEach(v => v.Connections.Upserted).SetValidator(new ConnectionDraftValidator());
        RuleForEach(v => v.DiagramAnnotations.Upserted).SetValidator(new DiagramAnnotationDraftValidator());

        AddDeltaConsistencyRules(v => v.Devices, "Devices", "cihaz");
        AddDeltaConsistencyRules(v => v.Connections, "Connections", "kablo");
        AddDeltaConsistencyRules(v => v.DiagramAnnotations, "Annotations", "not");
    }

    /// <summary> Ayni kaydin hem yazilip hem silinmesi kontrolü. </summary>
    private void AddDeltaConsistencyRules<T>(Func<DiagramSaveRequest, EntityDelta<T>> selector, string propertyName, string label) where T : IIdentifiableDraft
    {
        RuleFor(v => selector(v))
            .Must(d => d.Upserted.Select(u => u.Id).Distinct().Count() == d.Upserted.Count)
            .OverridePropertyName($"{propertyName}.Upserted")
            .WithMessage($"Ayni {label} gonderide birden fazla kez var");

        RuleFor(v => selector(v))
            .Must(d => !d.Upserted.Select(u => u.Id).Intersect(d.Deleted).Any())
            .OverridePropertyName($"{propertyName}.Deleted")
            .WithMessage($"Ayni {label} hem kaydediliyor hem siliniyor");
    }
}
