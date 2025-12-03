using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface IDocumentationSynthesisService
{
    Task<WikiPage> SynthesizeParentPageAsync(ModuleNode module, List<WikiPage> childPages, string language = "English");
}
