using codeMRI.Core.Models;
using codeMRI.Shared.Models;

namespace codeMRI.Core.Interfaces;

public interface IWikiGenerationService
{
    Task<WikiStructure> GenerateStructureAsync(string fileTree, string readme, string language = "English");
    Task<WikiPage> GeneratePageAsync(string pageTitle, List<string> filePaths, Dictionary<string, string> fileContents, string language = "English");
    Task<WikiPage> GenerateParentPageAsync(ModuleNode module, List<WikiPage> childPages, string language = "English");
}
