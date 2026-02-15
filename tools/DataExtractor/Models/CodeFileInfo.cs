namespace DataExtractor.Models;

public class CodeFileInfo
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsMain { get; set; } = false;
    public bool Hidden { get; set; } = false;
    public int Type { get; set; } = 1;
}
