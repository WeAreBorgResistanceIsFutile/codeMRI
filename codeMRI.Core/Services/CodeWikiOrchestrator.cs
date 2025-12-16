using System.Collections.Concurrent;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace codeMRI.Core.Services;

public class CodeWikiOrchestrator : ICodeWikiOrchestrator
{
    private readonly IHierarchicalDecompositionService _decompositionService;
    private readonly IEnhancedDependencyGraphService _graphService;
    private readonly IDocumentationJudgeService _judgeService;
    private readonly ILogger<CodeWikiOrchestrator> _logger;
    private readonly IRubricGenerationService _rubricService;
    private readonly IDocumentationSynthesisService _synthesisService;
    private readonly IWikiGenerationService _wikiGenerationService;
    private readonly IWikiRepository _wikiRepo;
    private readonly IProgressService _progressService; // Added field
    private readonly string _judgeModel; // Changed from List<string> _judgeModels
    private readonly SemaphoreSlim _semaphore;

    public CodeWikiOrchestrator(
        IHierarchicalDecompositionService decompositionService,
        IEnhancedDependencyGraphService graphService,
        IRubricGenerationService rubricService,
        IDocumentationJudgeService judgeService,
        IWikiGenerationService wikiGenerationService,
        IDocumentationSynthesisService synthesisService,
        IWikiRepository wikiRepo,
        IProgressService progressService, // Added parameter
        IOptions<CodeWikiOptions> options,
        ILogger<CodeWikiOrchestrator> logger,
        string judgeModel = "default") // Changed from List<string>? judgeModels = null
    {
        _decompositionService = decompositionService;
        _graphService = graphService;
        _rubricService = rubricService;
        _judgeService = judgeService;
        _wikiGenerationService = wikiGenerationService;
        _synthesisService = synthesisService;
        _wikiRepo = wikiRepo;
        _progressService = progressService; // Initialized new field
        _logger = logger;
        _judgeModel = judgeModel; // Initialized new field
        _semaphore = new SemaphoreSlim(options.Value.MaxDegreeOfParallelism);
    }

    public async Task<WikiStructure> GenerateAdvancedWikiAsync(
        string repositoryPath,
        RepositoryInfo repositoryInfo,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // 0. Initialize Progress Sservice
        if (progress != null) _progressService.SetHandler(p => progress.Report(p));

        _logger.LogInformation("Starting CodeWiki Advanced Workflow for {Repo}", repositoryPath);

        // 1. Hierarchical Decomposition
        _progressService.Report(new ProgressInfo { Phase = "Decomposition", Message = "Analyzing repository structure...", Percentage = 0 });
        _logger.LogInformation("Phase 1: Hierarchical Decomposition");

        ModuleTree moduleTree = null!;
        EnhancedDependencyGraph dependencyGraph = null!;
        
        // Scale Decomposition (Global 5% to 15%: Start=5, Width=10)
        await _progressService.WithScalingAsync(5, 10, async () => 
        {
             moduleTree = await _decompositionService.DecomposeHierarchicallyAsync(repositoryPath, cancellationToken);
        });
        
        // Build dependency graph for file path resolution
        _progressService.Report(new ProgressInfo { Phase = "Decomposition", Message = "Building dependency graph...", Percentage = 15 });
        var components = await _graphService.GetComponentsAsync(repositoryPath, cancellationToken);
        dependencyGraph = await _graphService.BuildGraphAsync(components, cancellationToken);
        
        // Convert to initial WikiStructure
        var structure = ConvertToWikiStructure(moduleTree, repositoryInfo);

        // 2. Rubric Generation
        _progressService.Report(new ProgressInfo { Phase = "Rubric Generation", Message = "Generating evaluation rubric...", Percentage = 15 });
        _logger.LogInformation("Phase 2: Rubric Generation");
        var rubric = await _rubricService.GenerateRubricAsync(structure, repositoryInfo, cancellationToken);
        
        // 3. Draft Generation (Content)
        _progressService.Report(new ProgressInfo { Phase = "Content Generation", Message = "Drafting wiki content...", Percentage = 20 });
        _logger.LogInformation("Phase 3: Content Drafting");
        
        // Count total modules for progress calculation
        int totalModules = CountModules(moduleTree.Root, new HashSet<string>());
        var progressState = new ProgressState { Total = totalModules, Processed = 0 };

        // Scale Content Generation (Global 20% to 90%: Start=20, Width=70)
        await _progressService.WithScalingAsync(20, 70, async () => 
        {
             await GenerateContentForModulesAsync(moduleTree.Root, structure, repositoryPath, repositoryInfo, dependencyGraph, progressState, new ConcurrentDictionary<string, byte>(), cancellationToken);
        });

        // 4. Evaluation (The Judge)
        _progressService.Report(new ProgressInfo { Phase = "Evaluation", Message = "Evaluating documentation quality...", Percentage = 90 });
        _logger.LogInformation("Phase 4: Evaluation");
        var requirements = ExtractRequirements(rubric);
        // Use configured judge models
        var judgeModelsList = new List<string> { _judgeModel };

        var assessments = await _judgeService.EvaluateRequirementsAsync(
            requirements, 
            structure, 
            judgeModelsList, 
            maxConcurrency: _semaphore.CurrentCount > 0 ? _semaphore.CurrentCount : 5, // Pass the configured max concurrency
            cancellationToken: cancellationToken);

        // Log results
        var avgScore = assessments.Any() ? assessments.Average(a => a.MeanScore) : 0;
        _logger.LogInformation("Evaluation Complete. Average Score: {Score}", avgScore);

        // TODO: Implement Refinement Loop based on low scores
        // For now, we return the judged structure (results could be appended to metadata)
        
        _progressService.Report(new ProgressInfo { Phase = "Complete", Message = "Documentation generated successfully.", Percentage = 100 });
        return structure;
    }

    private class ProgressState
    {
        public int Total { get; set; }
        public int Processed; // Field for Interlocked
    }

    private int CountModules(ModuleNode node, HashSet<string> visitedIds) // Helper uses HashSet as it is synchronous pre-calculation
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
        RepositoryInfo repoInfo,
        EnhancedDependencyGraph graph,
        ProgressState progressState,
        ConcurrentDictionary<string, byte> visitedIds,
        CancellationToken cancellationToken)
    {
        if (!visitedIds.TryAdd(module.Id, 0)) return;

        // Recursively process children first (Bottom-Up)
        // Parallelize children processing
        var childTasks = module.Children.Select(child => 
            GenerateContentForModulesAsync(child, structure, repoPath, repoInfo, graph, progressState, visitedIds, cancellationToken));
        
        await Task.WhenAll(childTasks);

        // Limit concurrency for the actual generation logic of THIS node
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Check cache first
            // If a page with this title already exists in the repo, skip generation
            var existingPage = await _wikiRepo.GetPageByTitleAsync(repoPath, module.Name);
            if (existingPage != null)
            {
                _logger.LogInformation("Skipping generation for page '{PageTitle}' (cached)", module.Name);
                lock (structure.Pages)
                {
                   structure.Pages.Add(existingPage);
                }
                
                // Add to sections structure
                var cachedSection = new WikiSection
                {
                    Id = $"section_{module.Id}",
                    Title = module.Name,
                    PageRefs = new List<string> { existingPage.Id }
                };
                lock (structure.Sections)
                {
                    structure.Sections.Add(cachedSection);
                }
                
                UpdateProgress(progressState, module.Name);
                return;
            }

            // Generate content for this module
            WikiPage page;
            if (module.IsLeaf)
            {
                 // Map component IDs to file paths and load contents
                 var filePaths = new List<string>();
                 var fileContents = new Dictionary<string, string>();
                 
                 foreach (var componentId in module.Components)
                 {
                     var node = graph.GetNode(componentId);
                     if (node != null && !string.IsNullOrWhiteSpace(node.Metadata.FilePath))
                     {
                         var filePath = node.Metadata.FilePath;
                         if (!filePaths.Contains(filePath))
                         {
                             filePaths.Add(filePath);
                             
                             // Try to load file content
                             try
                             {
                                 if (File.Exists(filePath))
                                 {
                                     fileContents[filePath] = await File.ReadAllTextAsync(filePath, cancellationToken);
                                 }
                                 else
                                 {
                                     var fullPath = Path.Combine(repoPath, filePath);
                                     if (File.Exists(fullPath))
                                     {
                                         fileContents[filePath] = await File.ReadAllTextAsync(fullPath, cancellationToken);
                                     }
                                 }
                             }
                             catch (Exception ex)
                             {
                                 _logger.LogWarning(ex, "Failed to read file {FilePath}", filePath);
                             }
                         }
                     }
                 }
                 
                 page = await _wikiGenerationService.GeneratePageAsync(
                     module.Name, 
                     filePaths, 
                     fileContents,
                     "English",
                     repoPath,
                     repoInfo.Url,
                     repoInfo.Branch
                 );
            }
            else
            {
                // Parent page synthesis
                // We need the child pages that we just generated
                // Find child pages in the structure
                // Note: structure.Pages access must be thread-safe if modified concurrently
                List<WikiPage> childPages;
                lock (structure.Pages)
                {
                    childPages = structure.Pages
                        .Where(p => module.Children.Any(c => c.Name == p.Title)) // Loose matching by title
                        .ToList();
                }

                page = await _synthesisService.SynthesizeParentPageAsync(module, childPages);
            }

            lock (structure.Pages)
            {
                structure.Pages.Add(page);
            }
            
            await _wikiRepo.SavePageAsync(repoPath, page);
            
            // Add to sections structure (naive mapping)
            var section = new WikiSection
            {
                Id = $"section_{module.Id}",
                Title = module.Name,
                PageRefs = new List<string> { page.Id }
            };
            lock (structure.Sections)
            {
                structure.Sections.Add(section);
            }

            UpdateProgress(progressState, module.Name);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void UpdateProgress(ProgressState state, string moduleName)
    {
        Interlocked.Increment(ref state.Processed);
        
        // Local Percentage 0-100
        int percentage = (int)((double)state.Processed / state.Total * 100);
        
        _progressService.Report(new ProgressInfo 
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
