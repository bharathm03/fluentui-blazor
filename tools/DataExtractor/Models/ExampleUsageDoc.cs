namespace DataExtractor.Models;

public class ExampleUsageDoc
{
    public string ComponentName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Key { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<CodeFileInfo> CodeFiles { get; set; } = new();
    public string Description { get; set; } = "";
    public string OriginalDescription { get; set; } = "";
}
