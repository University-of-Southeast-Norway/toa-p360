namespace ToaArchiver.Domain;

public class ToaOptions
{
    public IEnumerable<string>? ContractSignedStatuses { get; set; }
    public DateTimeOffset ProductionDate { get; set; }
    public string MessageParser { get; set; }
}