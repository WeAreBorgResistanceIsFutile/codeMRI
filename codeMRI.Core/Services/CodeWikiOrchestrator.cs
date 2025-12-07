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
        _synthesisService = synthesisService;
        _wikiRepo = wikiRepo;
        _judgeModels = judgeModels ?? new List<string> { "default" };
        _logger = logger;
    }

    public async Task<WikiStructure> GenerateAdvancedWikiAsync(
        string repositoryPath,
        RepositoryInfo repositoryInfo,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting CodeWiki Advanced Workflow for {Repo}", repositoryPath);

        // 1. Hierarchical Decomposition
        progress?.Report(new ProgressInfo { Phase = "Decomposition", Message = "Analyzing repository structure...", Percentage = 5 });
        _logger.LogInformation("Phase 1: Hierarchical Decomposition");
        var moduleTree = await _decompositionService.DecomposeHierarchicallyAsync(repositoryPath, cancellationToken);
        
        // Convert to initial WikiStructure
        var structure = ConvertToWikiStructure(moduleTree, repositoryInfo);

        // 2. Rubric Generation
        progress?.Report(new ProgressInfo { Phase = "Rubric Generation", Message = "Generating evaluation rubric...", Percentage = 15 });
        _logger.LogInformation("Phase 2: Rubric Generation");
        var rubric = await _rubricService.GenerateRubricAsync(structure, repositoryInfo, cancellationToken);
        
        // 3. Draft Generation (Content)
        progress?.Report(new ProgressInfo { Phase = "Content Generation", Message = "Drafting wiki content...", Percentage = 20 });
        _logger.LogInformation("Phase 3: Content Drafting");
        
        // Count total modules for progress calculation
        int totalModules = CountModules(moduleTree.Root, new HashSet<string>());
        var progressState = new ProgressState { Total = totalModules, Processed = 0 };

        // We need to traverse the ModuleTree to generate pages in valid order (bottom-up is often better for synthesis)
        // But for leaf nodes, order doesn't matter much.
        await GenerateContentForModulesAsync(moduleTree.Root, structure, repositoryPath, progress, progressState, new HashSet<string>(), cancellationToken);

        // 4. Evaluation (The Judge)
        progress?.Report(new ProgressInfo { Phase = "Evaluation", Message = "Evaluating documentation quality...", Percentage = 90 });
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
        
        progress?.Report(new ProgressInfo { Phase = "Complete", Message = "Documentation generated successfully.", Percentage = 100 });
        return structure;
    }

    private class ProgressState
    {
        public int Total { get; set; }
        public int Processed { get; set; }
    }

    private int CountModules(ModuleNode node, HashSet<string> visitedIds)
    {
        if (!visitedIds.Add(node.Id)) return 0;

        int count = 1;
        foreach (var child in node.Children)
        {
            count += CountModules(child, visitedIds);
        }
        return count;
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
        IProgress<ProgressInfo>? progress,
        ProgressState progressState,
        HashSet<string> visitedIds,
        CancellationToken cancellationToken)
    {
        if (!visitedIds.Add(module.Id)) return;

        // Recursively process children first (Bottom-Up)
        foreach (var child in module.Children)
        {
            await GenerateContentForModulesAsync(child, structure, repoPath, progress, progressState, visitedIds, cancellationToken);
        }

        // Check cache first
        // If a page with this title already exists in the repo, skip generation
        var existingPage = await _wikiRepo.GetPageByTitleAsync(repoPath, module.Name);
        if (existingPage != null)
        {
            _logger.LogInformation("Skipping generation for page '{PageTitle}' (cached)", module.Name);
            structure.Pages.Add(existingPage);
            
            // Add to sections structure
            var cachedSection = new WikiSection
            {
                Id = $"section_{module.Id}",
                Title = module.Name,
                PageRefs = new List<string> { existingPage.Id }
            };
            structure.Sections.Add(cachedSection);
            
            UpdateProgress(progress, progressState, module.Name);
            return;
        }

        // Generate content for this module
        WikiPage page;
        if (module.IsLeaf)
        {
             
             
             page = await _wikiGenerationService.GeneratePageAsync(
                 module.Name, 
                 module.Components.ToList(), 
                 new Dictionary<string, string>(), /* empty contents */
                 "English",
                 repoPath
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

        UpdateProgress(progress, progressState, module.Name);
    }

    private void UpdateProgress(IProgress<ProgressInfo>? progress, ProgressState state, string moduleName)
    {
        state.Processed++;
        int percentage = 20 + (int)((double)state.Processed / state.Total * 70);
        progress?.Report(new ProgressInfo 
        { 
            Phase = "Content Generation", 
            Message = $"Generated content for {moduleName} ({state.Processed}/{state.Total})", 
            Percentage = percentage 
        });
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
