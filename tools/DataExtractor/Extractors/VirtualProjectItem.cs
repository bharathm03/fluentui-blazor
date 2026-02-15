using System.Text;
using Microsoft.AspNetCore.Razor.Language;

namespace DataExtractor.Extractors;

/// <summary>
/// In-memory implementation of RazorProjectItem for compiling Razor content without disk I/O.
/// </summary>
public class VirtualProjectItem : RazorProjectItem
{
    private readonly byte[] _content;

    public VirtualProjectItem(string basePath, string filePath, string content)
    {
        BasePath = basePath;
        FilePath = filePath;
        RelativePhysicalPath = filePath;
        _content = Encoding.UTF8.GetBytes(content);
    }

    public override string BasePath { get; }
    public override string FilePath { get; }
    public override string PhysicalPath => FilePath;
    public override string RelativePhysicalPath { get; }
    public override RazorFileKind FileKind => RazorFileKind.Component;
    public override bool Exists => true;

    public override Stream Read() => new MemoryStream(_content);
}
