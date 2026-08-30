using System.ComponentModel;

namespace Scadex.Core.Enums;

public static class ClientType
{
    public const string Web = "a9537ece-1957-4bd0-8692-5da2c16e715b";
    public const string Desktop = "3d760494-e65e-43ca-8381-6b0b1805fd2e";
    public const string Mobile = "7519a895-475f-4afd-808d-69a84ff26035";
    public const string Unknown = "851f4637-32e6-4585-b5f2-f6c193fc0e3c";

    public static string[] DefinedTypes = { Web, Desktop, Mobile, Unknown };
}
