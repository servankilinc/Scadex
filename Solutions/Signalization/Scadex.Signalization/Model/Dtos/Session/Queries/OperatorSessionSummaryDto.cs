using Scadex.Core.Model;

namespace Scadex.Signalization.Model.Dtos.Session.Queries;

public class OperatorSessionSummaryDto : IDto
{
    public int SessionCount { get; set; }
    public int TotalDurationSec { get; set; }
    public int WarningCount { get; set; }

    /// <summary> Operator basina: oturum suresi oturumdaki HER operatore sayilir (ayni anda iki operator calisabilir). </summary>
    public List<OperatorSessionSummaryRowDto> ByOperator { get; set; } = [];
    public List<OperatorSessionSummaryRowDto> ByAuthority { get; set; } = [];
    public List<OperatorSessionSummaryRowDto> ByCabinet { get; set; } = [];
}

public class OperatorSessionSummaryRowDto : IDto
{
    public string Key { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int SessionCount { get; set; }
    public int TotalDurationSec { get; set; }
    public int AverageDurationSec { get; set; }
    public int WarningCount { get; set; }
}
