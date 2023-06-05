namespace ToaArchiver.Worker.Configurations
{
    public record ApiKeyOptions
    {
        public string Scope { get; set; }
        public string Header { get; set; }
        public string Key { get; set; }
    }
}
