using YamlDotNet.Serialization;

namespace BadDicom.Configuration;

[YamlSerializable]
internal sealed class Config
{
    public TargetDatabase? Database { get;set; }
    public ExplicitUIDs? UIDs { get; set; }
}