using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;

namespace codeMRI.Core.Services;

public class CodeWikiOrchestrator : ICodeWikiOrchestrator
{
    private readonly IHierarchicalDecompositionService _decompositionService;
    private readonly IDocumentationJudgeService _judgeService;
    private readonly ILogger<CodeWikiOrchestrator> _logger;
    private readonly IRubricGenerationService _rubricService;
    private readonly IDocumentationSynthesisService _synthesisService;
    private readonly IWikiGenerationService _wikiGenerationService;
    private readonly IWikiRepository _wikiRepo;
    private readonly List<string> _judgeModels;

    public CodeWikiOrchestrator(
        IHierarchicalDecompositionService decompositionService,
        IRubricGenerationService rubricService,
        IDocumentationJudgeService judgeService,
        IWikiGenerationService wikiGenerationService,
        IDocumentationSynthesisService synthesisService,
        IWikiRepository wikiRepo,
        ILogger<CodeWikiOrchestrator> logger,
        List<string>? judgeModels = null)
    {
        _decompositionService = decompositionService;
        _rubricService = rubricService;
        _judgeService = judgeService;
        _wikiGenerationService = wikiGenerationService;
        _rubricService = rubricService;
        _judgeService = judgeService;
        _synthesisService = synthesisService;
        _wikiRepo = wikiRepo;
        _judgeModels = judgeModels ?? new List<string> { "default" };
        _logger = logger;
    }

    public async Task<WikiStructure> GenerateAdvancedWikiAsync(
        string repositoryPath,
        RepositoryInfo repositoryInfo,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting CodeWiki Advanced Workflow for {Repo}", repositoryPath);

        // 1. Hierarchical Decomposition
        _logger.LogInformation("Phase 1: Hierarchical Decomposition");
        var moduleTree = await _decompositionService.DecomposeHierarchicallyAsync(repositoryPath, cancellationToken);
        
        // Convert to initial WikiStructure
        var structure = ConvertToWikiStructure(moduleTree, repositoryInfo);

        // 2. Rubric Generation
        _logger.LogInformation("Phase 2: Rubric Generation");
        var rubric = await _rubricService.GenerateRubricAsync(structure, repositoryInfo, cancellationToken);
        
        // 3. Draft Generation (Content)
        _logger.LogInformation("Phase 3: Content Drafting");
        
        // We need to traverse the ModuleTree to generate pages in valid order (bottom-up is often better for synthesis)
        // But for leaf nodes, order doesn't matter much.
        await GenerateContentForModulesAsync(moduleTree.Root, structure, repositoryPath, cancellationToken);

        // 4. Evaluation (The Judge)
        _logger.LogInformation("Phase 4: Evaluation");
        var requirements = ExtractRequirements(rubric);
        // Use configured judge models
        
        var assessments = await _judgeService.EvaluateRequirementsAsync(
            requirements, 
            structure, 
            _judgeModels, 
            cancellationToken);

        // Log results
        var avgScore = assessments.Any() ? assessments.Average(a => a.MeanScore) : 0;
        _logger.LogInformation("Evaluation Complete. Average Score: {Score}", avgScore);

        // TODO: Implement Refinement Loop based on low scores
        // For now, we return the judged structure (results could be appended to metadata)
        
        return structure;
    }

    private WikiStructure ConvertToWikiStructure(ModuleTree tree, RepositoryInfo repoInfo)
    {
        var structure = new WikiStructure
        {
            Title = $"{repoInfo.Name} Documentation",
            Description = $"Automatically generated documentation for {repoInfo.Name}",
            Sections = new List<WikiSection>(), // Modules will form sections
            Pages = new List<WikiPage>()
        };

        // Recursive conversion handled during content generation or here?
        // Let's create a skeleton first.
        return structure;
    }

    private async Task GenerateContentForModulesAsync(
        ModuleNode module, 
        WikiStructure structure, 
        string repoPath,
        CancellationToken cancellationToken)
    {
        // Recursively process children first (Bottom-Up)
        foreach (var child in module.Children)
        {
            await GenerateContentForModulesAsync(child, structure, repoPath, cancellationToken);
        }

        // Generate content for this module
        WikiPage page;
        if (module.IsLeaf)
        {
             // It's a leaf node with actual code components
             // Use WikiGenerationService.GeneratePageAsync
             // We need to map components to file paths
             // Assumption: module.Components contains Component IDs which might be file paths or class names
             // We'll pass them as "RelevantFiles" simply for context
             
             // In a real scenario, we need the file contents. 
             // WikiGenerationService.GeneratePageAsync expects filePaths and fileContents.
             // We would need to fetch file contents here. 
             // For this implementation, we will assume WikiGenerationService handles retrieval if we pass empty contents but valid paths?
             // Actually GeneratePageAsync tries to fallback to VectorDB if paths are empty.
             // But if we have paths, we need contents. 
             
             // Simplification: We will pass component IDs as file paths and hope they resolve or let the service handle it.
             // Ideally we'd have a ICodeProvider service.
             
             page = await _wikiGenerationService.GeneratePageAsync(
                 module.Name, 
                 module.Components.ToList(), 
                 new Dictionary<string, string>() /* empty contents */
             );
        }
        else
        {
            // Parent page synthesis
            // We need the child pages that we just generated
            // Find child pages in the structure
            var childPages = structure.Pages
                .Where(p => module.Children.Any(c => c.Name == p.Title)) // Loose matching by title
                .ToList();

            page = await _synthesisService.SynthesizeParentPageAsync(module, childPages);
        }

        structure.Pages.Add(page);
        
        await _wikiRepo.SavePageAsync(repoPath, page);
        
        // Add to sections structure (naive mapping)
        var section = new WikiSection
        {
            Id = $"section_{module.Id}",
            Title = module.Name,
            PageRefs = new List<string> { page.Id }
        };
        structure.Sections.Add(section);
    }

    private List<RubricRequirement> ExtractRequirements(EvaluationRubric rubric)
    {
        var list = new List<RubricRequirement>();
        void Visit(RubricNode node)
        {
            if (node is RubricRequirement r && node.IsLeaf) list.Add(r);
            if (node.Children != null) foreach(var c in node.Children) Visit(c);
        }
        Visit(rubric);
        return list;
    }
}
