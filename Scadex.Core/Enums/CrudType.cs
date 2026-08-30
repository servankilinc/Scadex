using System.ComponentModel;

namespace Scadex.Core.Enums;

public enum CrudType : byte
{
    [Description("Read")]
    Read = 1,
    [Description("Create")]
    Create = 2,
    [Description("Update")]
    Update = 3,
    [Description("Delete")]
    Delete = 4,
    [Description("Undefined")]
    Undefined = 5
}
