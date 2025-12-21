using System.Collections.Concurrent;
using codeMRI.Core.Interfaces;
using codeMRI.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace codeMRI.Core.Services;

public class CodeWikiOrchestrator : ICodeWikiOrchestrator
{
    private readonly IHierarchicalDecompositionService _decompositionService;
    private readonly IDelegationService _delegationService;
    private readonly IEnhancedDependencyGraphService _graphService;
    private readonly string _judgeModel;
    private readonly IDocumentationJudgeService _judgeService;
    private readonly ILogger<CodeWikiOrchestrator> _logger;
    private readonly INavigationStructureService _navigationService;
    private readonly CodeWikiOptions _options;
    private readonly IProgressService _progressService;
    private readonly IDocumentationRevisionService _revisionService;
    private readonly IRubricGenerationService _rubricService;
    private readonly SemaphoreSlim _semaphore;
    private readonly IDocumentationSynthesisService _synthesisService;
    private readonly IAgentTelemetryService _telemetryService;
    private readonly IDocumentIndexer _documentIndexer;
    private readonly IWikiGenerationService _wikiGenerationService;
    private readonly IWikiRepository _wikiRepo;

    public CodeWikiOrchestrator(
        IHierarchicalDecompositionService decompositionService,
        IEnhancedDependencyGraphService graphService,
        IRubricGenerationService rubricService,
        IDocumentationJudgeService judgeService,
        IWikiGenerationService wikiGenerationService,
        IDocumentationSynthesisService synthesisService,
        IDocumentationRevisionService revisionService,
        IWikiRepository wikiRepo,
        IProgressService progressService,
        IAgentTelemetryService telemetryService,
        IDelegationService delegationService,
        IDocumentIndexer documentIndexer,
        INavigationStructureService navigationService,
        IOptions<CodeWikiOptions> options,
        ILogger<CodeWikiOrchestrator> logger,
        string judgeModel = "default")
    {
        _decompositionService = decompositionService;
        _graphService = graphService;
        _rubricService = rubricService;
        _judgeService = judgeService;
        _wikiGenerationService = wikiGenerationService;
        _synthesisService = synthesisService;
        _revisionService = revisionService;
        _wikiRepo = wikiRepo;
        _progressService = progressService;
        _telemetryService = telemetryService;
        _delegationService = delegationService;
        _documentIndexer = documentIndexer;
        _navigationService = navigationService;
        _options = options.Value;
        _logger = logger;
        _judgeModel = judgeModel;
        _semaphore = new SemaphoreSlim(_options.MaxDegreeOfParallelism);
    }

    public async Task<WikiStructure> GenerateAdvancedWikiAsync(
        string repositoryPath,
        RepositoryInfo repositoryInfo,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Use configured default audience
        var audience = _options.DefaultAudience;

        // 0. Initialize Progress Sservice
        if (progress != null) _progressService.SetHandler(p => progress.Report(p));

        _logger.LogInformation("Starting CodeWiki Advanced Workflow for {Repo}", repositoryPath);
        repositoryInfo.RepoPath = repositoryPath;
        _telemetryService.TrackAgentActivity("Orchestrator",
            $"Starting CodeWiki Advanced Workflow for {repositoryPath}");

        // 1. Hierarchical Decomposition
        _progressService.Report(new ProgressInfo
            { Phase = "Decomposition", Message = "Analyzing repository structure...", Percentage = 0 });
        _logger.LogInformation("Phase 1: Hierarchical Decomposition");
        _telemetryService.TrackAgentActivity("Orchestrator", "Phase 1: Hierarchical Decomposition");

        ModuleTree moduleTree = null!;
        EnhancedDependencyGraph dependencyGraph = null!;

        // Scale Decomposition (Global 5% to 15%: Start=5, Width=10)
        await _progressService.WithScalingAsync(5, 10,
            async () =>
            {
                moduleTree =
                    await _decompositionService.DecomposeHierarchicallyAsync(repositoryPath, cancellationToken);
            });

        // Build dependency graph for file path resolution
        _progressService.Report(new ProgressInfo
            { Phase = "Decomposition", Message = "Building dependency graph...", Percentage = 15 });
        var components = await _graphService.GetComponentsAsync(repositoryPath, cancellationToken);
        dependencyGraph = await _graphService.BuildGraphAsync(components, cancellationToken);

        // Update repository info with counts from decomposition
        repositoryInfo.ComponentCount = components.Count;
        repositoryInfo.LinesOfCode =
            moduleTree.GetAllLeaves().Sum(l => l.ComplexityScore * 10); // Simple heuristic: 1 complexity ~ 10 LOC

        // Generate documentation-optimized structure using LLM
        _progressService.Report(new ProgressInfo
            { Phase = "Structure Planning", Message = "Planning documentation structure...", Percentage = 15 });
        
        var structure = await _navigationService.GenerateDocumentationStructureAsync(
            moduleTree,
            repositoryInfo,
            cancellationToken);

        // 2. Rubric Generation
        _progressService.Report(new ProgressInfo
            { Phase = "Rubric Generation", Message = "Generating evaluation rubric...", Percentage = 15 });
        _logger.LogInformation("Phase 2: Rubric Generation");
        _telemetryService.TrackAgentActivity("Orchestrator", "Phase 2: Rubric Generation");
        var rubric = await _rubricService.GenerateRubricAsync(structure, repositoryInfo, cancellationToken);

        // 3. Draft Generation (Content)
        _progressService.Report(new ProgressInfo
            { Phase = "Content Generation", Message = "Drafting wiki content...", Percentage = 20 });
        _logger.LogInformation("Phase 3: Content Drafting");
        _telemetryService.TrackAgentActivity("Orchestrator", "Phase 3: Content Drafting");

        // Count total modules for progress calculation
        var totalModules = CountModules(moduleTree.Root, new HashSet<string>());
        var progressState = new ProgressState { Total = totalModules, Processed = 0 };

        // Scale Content Generation (Global 20% to 90%: Start=20, Width=70)
        await _progressService.WithScalingAsync(20, 70,
            async () =>
            {
                await GenerateContentForModulesAsync(moduleTree.Root, structure, repositoryPath, repositoryInfo,
                    dependencyGraph, progressState, new ConcurrentDictionary<string, byte>(), cancellationToken,
                    audience);
            });

        // 4. Evaluation (The Judge)
        _progressService.Report(new ProgressInfo
            { Phase = "Evaluation", Message = "Evaluating documentation quality...", Percentage = 90 });
        _logger.LogInformation("Phase 4: Evaluation");
        _telemetryService.TrackAgentActivity("Orchestrator", "Phase 4: Evaluation");
        var requirements = ExtractRequirements(rubric);
        // Use configured judge models
        var judgeModelsList = new List<string> { _judgeModel };

        var assessments = await _judgeService.EvaluateRequirementsAsync(
            requirements,
            structure,
            judgeModelsList,
            _semaphore.CurrentCount > 0 ? _semaphore.CurrentCount : 5, // Pass the configured max concurrency
            cancellationToken);

        // Log results
        var avgScore = assessments.Any() ? assessments.Average(a => a.MeanScore) : 0;
        _logger.LogInformation("Evaluation Complete. Average Score: {Score}", avgScore);

        // TODO: Implement Refinement Loop based on low scores
        // For now, we return the judged structure (results could be appended to metadata)

        // 5. Indexing for RAG
        _progressService.Report(new ProgressInfo { Phase = "Indexing", Message = "Indexing documentation for RAG...", Percentage = 98 });
        _logger.LogInformation("Phase 5: Indexing Documentation");
        await _documentIndexer.IndexDocumentationAsync(repositoryPath, structure, cancellationToken);

        _progressService.Report(new ProgressInfo { Phase = "Complete", Message = "Advanced Wiki Generation Complete", Percentage = 100 });
        _telemetryService.TrackAgentActivity("Orchestrator", "CodeWiki Advanced Workflow completed");

        // Final structural refinements
        SortAndPruneStructure(structure);

        return structure;
    }

    private void SortAndPruneStructure(WikiStructure structure)
    {
        if (structure?.Sections == null) return;

        // Perform recursive pruning and sorting
        PruneAndSortSections(structure.Sections);
    }

    private void PruneAndSortSections(List<WikiSection> sections)
    {
        for (var i = sections.Count - 1; i >= 0; i--)
        {
            var section = sections[i];

            // 1. Recursive call for subsections
            if (section.SubSections != null && section.SubSections.Count > 0) PruneAndSortSections(section.SubSections);

            // 2. Pruning: If section name is effectively the same as child page/section, simplify
            // Check if it's a "wrapper" section with one page or one subsection of the same name
            if (section.PageRefs.Count == 1 && (section.SubSections == null || section.SubSections.Count == 0))
            {
                // Note: We don't have easy access to page titles here without the structure.
                // However, we can use the module name logic or simply look at the structure.
                // For now, let's keep it simple: if it's a leaf section, it's just a grouping.
            }
        }

        // 3. Sorting logic
        sections.Sort((a, b) =>
        {
            int GetPriority(string title)
            {
                var t = title.ToLowerInvariant();
                if (t.Contains("overview")) return 1;
                if (t.Contains("application")) return 2;
                if (t.Contains("domain")) return 3;
                if (t.Contains("repository")) return 10;
                if (t.Contains("others")) return 11;
                return 5; // Default middle
            }

            var pA = GetPriority(a.Title);
            var pB = GetPriority(b.Title);

            if (pA != pB) return pA.CompareTo(pB);
            return string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
        });
    }

    private int
        CountModules(ModuleNode node,
            HashSet<string> visitedIds) // Helper uses HashSet as it is synchronous pre-calculation
    {
        if (!visitedIds.Add(node.Id)) return 0;

        var count = 1;
        foreach (var child in node.Children) count += CountModules(child, visitedIds);
        return count;
    }

    private WikiSection? FindSectionById(List<WikiSection> sections, string id)
    {
        if (sections == null) return null;

        foreach (var section in sections)
        {
            if (section.Id == id) return section;
            var found = FindSectionById(section.SubSections, id);
            if (found != null) return found;
        }

        return null;
    }

    private async Task GenerateContentForModulesAsync(
        ModuleNode module,
        WikiStructure structure,
        string repoPath,
        RepositoryInfo repoInfo,
        EnhancedDependencyGraph graph,
        ProgressState progressState,
        ConcurrentDictionary<string, byte> visitedIds,
        CancellationToken cancellationToken,
        AudienceType audience)
    {
        if (!visitedIds.TryAdd(module.Id, 0)) return;

        // Recursively process children first (Bottom-Up)
        // Parallelize children processing
        var childTasks = module.Children.Select(child =>
            GenerateContentForModulesAsync(child, structure, repoPath, repoInfo, graph, progressState, visitedIds,
                cancellationToken, audience));

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

                // Update the section reference for this cached page
                if (structure.ModuleToSectionMap.TryGetValue(module.Id, out var cachedPageSectionId))
                {
                    var cachedPageSection = FindSectionById(structure.Sections, cachedPageSectionId);
                    if (cachedPageSection != null)
                    {
                        lock (cachedPageSection)
                        {
                            if (!cachedPageSection.PageRefs.Contains(existingPage.Id))
                                cachedPageSection.PageRefs.Add(existingPage.Id);
                        }
                    }
                }

                UpdateProgress(progressState, module.Name);
                return;
            }

            // Dynamic delegation: check if this leaf module needs to be subdivided
            if (module.IsLeaf)
            {
                var delegationDecision = _delegationService.EvaluateDelegation(module, graph, GetModuleDepth(module));
                if (delegationDecision.ShouldDelegate)
                {
                    _logger.LogInformation(
                        "Delegating module {ModuleName}: {Reason}",
                        module.Name, delegationDecision.Description);

                    _telemetryService.TrackDelegation(
                        "Orchestrator",
                        "Orchestrator-SubModule",
                        delegationDecision.Description,
                        module.Id);

                    // Perform delegation (subdivide the module)
                    var subModules = await _delegationService.DelegateModuleAsync(module, graph, cancellationToken);

                    // Update progress total since we added new modules
                    Interlocked.Add(ref progressState.Total, subModules.Count);

                    // Release semaphore during recursive processing
                    _semaphore.Release();
                    try
                    {
                        // Process new sub-modules recursively (they are now children of this module)
                        var subTasks = subModules.Select(subModule =>
                            GenerateContentForModulesAsync(subModule, structure, repoPath, repoInfo, graph,
                                progressState, visitedIds, cancellationToken, audience));
                        await Task.WhenAll(subTasks);
                    }
                    finally
                    {
                        await _semaphore.WaitAsync(cancellationToken);
                    }

                    // Module is no longer a leaf - mark it for parent page synthesis with merge
                    module.Metadata["HasClusterChildren"] = "true";
                }
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
                                        fileContents[filePath] =
                                            await File.ReadAllTextAsync(fullPath, cancellationToken);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to read file {FilePath}", filePath);
                            }
                        }
                    }
                }

                page = await _wikiGenerationService.GenerateEnhancedPageAsync(
                    module,
                    null, // relatedPages 
                    new ModulePageContext(), // Empty context as placeholder or we should build it 
                    fileContents,
                    "English",
                    repoPath,
                    audience,
                    repoInfo.Url,
                    repoInfo.Branch,
                    filePaths
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

                // Check if this module was subdivided into clusters
                var isMergingClusters = module.Metadata.ContainsKey("HasClusterChildren") &&
                                        module.Metadata["HasClusterChildren"] == "true";

                if (isMergingClusters)
                    _logger.LogInformation(
                        "Merging {Count} cluster pages into parent page for {ModuleName}",
                        childPages.Count, module.Name);

                page = await _synthesisService.SynthesizeParentPageAsync(
                    module,
                    childPages,
                    "English",
                    audience,
                    isMergingClusters);

                // Revision step: Refine parent documentation based on child insights (Algorithm 1 - CodeWiki paper)
                if (_options.EnableRevisionLoop && childPages.Count >= _options.MinChildrenForRevision)
                {
                    _logger.LogInformation(
                        "Revising parent page {ModuleName} based on {Count} child pages",
                        module.Name, childPages.Count);

                    page = await _revisionService.ReviseParentDocumentationAsync(
                        page,
                        module,
                        childPages,
                        "English",
                        cancellationToken);
                }
            }

            lock (structure.Pages)
            {
                structure.Pages.Add(page);
            }

            await _wikiRepo.SavePageAsync(repoPath, page);

            // Update the section for this module using the mapping from navigation service
            string sectionId;
            if (structure.ModuleToSectionMap.TryGetValue(module.Id, out var mappedSectionId))
            {
                sectionId = mappedSectionId;
            }
            else
            {
                // Fallback: try the old pattern
                sectionId = $"section_{module.Id}";
                _logger.LogWarning("Module {ModuleId} not found in ModuleToSectionMap, using fallback section ID", module.Id);
            }
            
            var section = FindSectionById(structure.Sections, sectionId);

            if (section != null)
            {
                lock (section)
                {
                    if (page != null && !section.PageRefs.Contains(page.Id)) section.PageRefs.Add(page.Id);
                }
            }
            else
            {
                _logger.LogWarning("Section {SectionId} not found in structure during content generation", sectionId);
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
        var percentage = (int)((double)state.Processed / state.Total * 100);

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
            if (node.Children != null)
                foreach (var c in node.Children)
                    Visit(c);
        }

        Visit(rubric);
        return list;
    }

    /// <summary>
    ///     Calculates the depth of a module in the hierarchy (root = 0)
    /// </summary>
    private static int GetModuleDepth(ModuleNode module)
    {
        var depth = 0;
        var current = module.Parent;
        while (current != null)
        {
            depth++;
            current = current.Parent;
        }

        return depth;
    }

    private class ProgressState
    {
        public int Processed; // Field for Interlocked
        public int Total; // Field for Interlocked
    }
}