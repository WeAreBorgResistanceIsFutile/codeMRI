const parserService = require('./mockParserService');
const _ = require('lodash');
const { v4: uuidv4 } = require('uuid');

class AnalysisOrchestrator {
    constructor() {
        this.activeTasks = new Map();
        this.taskQueue = [];
        this.maxConcurrentTasks = 5;
        this.contextWindowLimit = 8192;
        this.complexityThresholds = {
            cyclomaticComplexity: 10,
            nestingDepth: 5,
            linesOfCode: 200,
            functionCount: 20
        };
    }

    async analyzeRepository(files, options = {}) {
        const analysisId = uuidv4();
        const analysis = {
            id: analysisId,
            startTime: new Date().toISOString(),
            files: files,
            options: options,
            results: {
                globalDependencyGraph: {},
                hierarchicalStructure: [],
                entryPoints: [],
                crossModuleReferences: [],
                metrics: {
                    totalFiles: files.length,
                    totalLines: 0,
                    totalComplexity: 0,
                    supportedLanguages: new Set(),
                    processingTime: 0
                }
            },
            status: 'initializing'
        };

        this.activeTasks.set(analysisId, analysis);

        try {
            // Phase 1: Initial parsing and basic analysis
            analysis.status = 'parsing';
            const parseResults = await this.parseFiles(files, analysisId);

            // Phase 2: Build global dependency graph
            analysis.status = 'building_dependencies';
            const dependencyGraph = await this.buildGlobalDependencyGraph(parseResults);

            // Phase 3: Hierarchical decomposition
            analysis.status = 'hierarchical_decomposition';
            const hierarchicalStructure = await this.performRepositoryDecomposition(parseResults, dependencyGraph);

            // Phase 4: Entry point identification
            analysis.status = 'identifying_entry_points';
            const entryPoints = await this.identifyRepositoryEntryPoints(parseResults);

            // Phase 5: Cross-module reference analysis
            analysis.status = 'analyzing_references';
            const crossModuleReferences = await this.analyzeCrossModuleReferences(parseResults);

            // Phase 6: Dynamic delegation for complex files
            analysis.status = 'delegating_complex_tasks';
            const enhancedResults = await this.delegateComplexTasks(parseResults);

            // Compile final results
            // Convert Set to Array for supportedLanguages
            analysis.results.metrics.supportedLanguages = Array.from(analysis.results.metrics.supportedLanguages);

            analysis.results = {
                ...analysis.results,
                parseResults,
                globalDependencyGraph: dependencyGraph,
                hierarchicalStructure,
                entryPoints,
                crossModuleReferences,
                enhancedResults
            };

            analysis.status = 'completed';
            analysis.endTime = new Date().toISOString();
            analysis.results.metrics.processingTime = 
                new Date(analysis.endTime) - new Date(analysis.startTime);

            return analysis;

        } catch (error) {
            analysis.status = 'failed';
            analysis.error = error.message;
            throw error;
        }
    }

    async parseFiles(files, analysisId) {
        const results = [];
        const batchSize = 10; // Process files in batches to manage memory

        for (let i = 0; i < files.length; i += batchSize) {
            const batch = files.slice(i, i + batchSize);
            const batchPromises = batch.map(async (file) => {
                try {
                    const result = await parserService.parseCode(
                        file.content, 
                        file.language, 
                        file.filePath
                    );
                    
                    // Update global metrics
                    const analysis = this.activeTasks.get(analysisId);
                    analysis.results.metrics.totalLines += result.metrics.linesOfCode;
                    analysis.results.metrics.totalComplexity += result.metrics.cyclomaticComplexity;
                    analysis.results.metrics.supportedLanguages.add(result.language);

                    return {
                        ...file,
                        ast: result
                    };
                } catch (error) {
                    return {
                        ...file,
                        error: error.message
                    };
                }
            });

            const batchResults = await Promise.all(batchPromises);
            results.push(...batchResults);

            // Allow event loop to process other tasks
            await new Promise(resolve => setImmediate(resolve));
        }

        return results;
    }

    async buildGlobalDependencyGraph(parseResults) {
        const dependencyGraph = {
            nodes: new Map(),
            edges: new Set(),
            languageStats: new Map()
        };

        // Build nodes from all files
        for (const file of parseResults) {
            if (file.error) continue;

            const nodeId = file.filePath;
            dependencyGraph.nodes.set(nodeId, {
                id: nodeId,
                language: file.ast.language,
                exports: file.ast.dependencyGraph.exports,
                entryPoints: file.ast.entryPoints,
                metrics: file.ast.metrics
            });

            // Track language statistics
            const lang = file.ast.language;
            dependencyGraph.languageStats.set(lang, 
                (dependencyGraph.languageStats.get(lang) || 0) + 1);
        }

        // Build edges from dependencies
        for (const file of parseResults) {
            if (file.error) continue;

            const sourceId = file.filePath;
            const dependencies = file.ast.dependencyGraph.dependencies;

            for (const dep of dependencies) {
                // Try to find matching file for this dependency
                const targetFile = this.findFileForDependency(dep, parseResults);
                if (targetFile) {
                    const edge = {
                        source: sourceId,
                        target: targetFile.filePath,
                        type: 'import',
                        weight: this.calculateDependencyWeight(file, targetFile)
                    };
                    dependencyGraph.edges.add(JSON.stringify(edge));
                }
            }
        }

        // Convert Set to Array for serialization
        return {
            nodes: Array.from(dependencyGraph.nodes.values()),
            edges: Array.from(dependencyGraph.edges).map(edge => JSON.parse(edge)),
            languageStats: Object.fromEntries(dependencyGraph.languageStats),
            metrics: this.calculateGraphMetrics(dependencyGraph)
        };
    }

    findFileForDependency(dependency, parseResults) {
        // Simple heuristic: look for files that might match the dependency
        const depName = dependency.toLowerCase().replace(/[^a-z0-9]/g, '');
        
        for (const file of parseResults) {
            if (file.error) continue;
            
            const fileName = file.filePath.toLowerCase().split('/').pop().replace(/\.[^.]+$/, '');
            
            if (fileName.includes(depName) || depName.includes(fileName)) {
                return file;
            }
        }
        
        return null;
    }

    calculateDependencyWeight(sourceFile, targetFile) {
        // Calculate dependency weight based on various factors
        let weight = 1.0;
        
        // Increase weight for same-language dependencies
        if (sourceFile.ast.language === targetFile.ast.language) {
            weight *= 1.5;
        }
        
        // Increase weight based on number of imports
        const importCount = sourceFile.ast.dependencyGraph.dependencies.length;
        weight *= Math.min(1.0 + (importCount * 0.1), 3.0);
        
        return Math.round(weight * 100) / 100;
    }

    calculateGraphMetrics(dependencyGraph) {
        const nodeCount = dependencyGraph.nodes.size;
        const edgeCount = dependencyGraph.edges.size;
        
        return {
            nodeCount,
            edgeCount,
            density: nodeCount > 1 ? (2 * edgeCount) / (nodeCount * (nodeCount - 1)) : 0,
            averageDegree: nodeCount > 0 ? (2 * edgeCount) / nodeCount : 0,
            languageDistribution: Object.fromEntries(dependencyGraph.languageStats)
        };
    }

    async performRepositoryDecomposition(parseResults, dependencyGraph) {
        const decomposition = {
            modules: [],
            layers: [],
            components: [],
            timestamp: new Date().toISOString()
        };

        // Group files by language
        const languageGroups = _.groupBy(parseResults, f => f.ast?.language);
        
        for (const [language, files] of Object.entries(languageGroups)) {
            const module = {
                name: `${language}-module`,
                language,
                files: files.map(f => f.filePath),
                entryPoints: files.flatMap(f => f.ast?.entryPoints || []),
                metrics: this.calculateModuleMetrics(files)
            };
            
            decomposition.modules.push(module);
        }

        // Identify architectural layers based on dependency patterns
        decomposition.layers = this.identifyArchitecturalLayers(parseResults, dependencyGraph);

        // Identify reusable components
        decomposition.components = this.identifyReusableComponents(parseResults, dependencyGraph);

        return decomposition;
    }

    calculateModuleMetrics(files) {
        const validFiles = files.filter(f => !f.error && f.ast);
        
        return {
            fileCount: validFiles.length,
            totalLines: validFiles.reduce((sum, f) => sum + f.ast.metrics.linesOfCode, 0),
            totalComplexity: validFiles.reduce((sum, f) => sum + f.ast.metrics.cyclomaticComplexity, 0),
            totalFunctions: validFiles.reduce((sum, f) => sum + f.ast.metrics.functionCount, 0),
            totalClasses: validFiles.reduce((sum, f) => sum + f.ast.metrics.classCount, 0)
        };
    }

    identifyArchitecturalLayers(parseResults, dependencyGraph) {
        const layers = [];
        
        // Simple heuristic-based layer identification
        const entryPointFiles = parseResults.filter(f => 
            f.ast?.entryPoints && f.ast.entryPoints.length > 0
        );
        
        const serviceFiles = parseResults.filter(f => 
            f.filePath.toLowerCase().includes('service') || 
            f.filePath.toLowerCase().includes('business')
        );
        
        const dataFiles = parseResults.filter(f => 
            f.filePath.toLowerCase().includes('model') || 
            f.filePath.toLowerCase().includes('entity') ||
            f.filePath.toLowerCase().includes('repository')
        );

        if (entryPointFiles.length > 0) {
            layers.push({
                name: 'presentation',
                type: 'entry-points',
                files: entryPointFiles.map(f => f.filePath),
                description: 'API endpoints, UI controllers, main entry points'
            });
        }

        if (serviceFiles.length > 0) {
            layers.push({
                name: 'business',
                type: 'services',
                files: serviceFiles.map(f => f.filePath),
                description: 'Business logic and service layer'
            });
        }

        if (dataFiles.length > 0) {
            layers.push({
                name: 'data',
                type: 'persistence',
                files: dataFiles.map(f => f.filePath),
                description: 'Data models and persistence layer'
            });
        }

        return layers;
    }

    identifyReusableComponents(parseResults, dependencyGraph) {
        const components = [];
        
        // Find files that are imported by many other files (high indegree)
        const indegreeMap = new Map();
        
        for (const edge of dependencyGraph.edges) {
            indegreeMap.set(edge.target, (indegreeMap.get(edge.target) || 0) + 1);
        }
        
        // Sort by indegree and identify top candidates
        const sortedByIndegree = Array.from(indegreeMap.entries())
            .sort((a, b) => b[1] - a[1])
            .slice(0, 5); // Top 5 candidates
        
        for (const [filePath, indegree] of sortedByIndegree) {
            if (indegree >= 2) { // At least 2 dependents
                const file = parseResults.find(f => f.filePath === filePath);
                if (file && !file.error) {
                    components.push({
                        name: filePath.split('/').pop().replace(/\.[^.]+$/, ''),
                        filePath,
                        type: 'reusable-component',
                        dependents: indegree,
                        exports: file.ast.dependencyGraph.exports,
                        language: file.ast.language
                    });
                }
            }
        }
        
        return components;
    }

    async identifyRepositoryEntryPoints(parseResults) {
        const allEntryPoints = [];
        
        for (const file of parseResults) {
            if (file.error || !file.ast?.entryPoints) continue;
            
            for (const entryPoint of file.ast.entryPoints) {
                allEntryPoints.push({
                    ...entryPoint,
                    filePath: file.filePath,
                    language: file.ast.language
                });
            }
        }
        
        // Categorize entry points
        return {
            total: allEntryPoints.length,
            byType: _.groupBy(allEntryPoints, 'type'),
            byLanguage: _.groupBy(allEntryPoints, 'language'),
            all: allEntryPoints.sort((a, b) => a.filePath.localeCompare(b.filePath))
        };
    }

    async analyzeCrossModuleReferences(parseResults) {
        const crossModuleRefs = [];
        const referenceMap = new Map();
        
        for (const file of parseResults) {
            if (file.error || !file.ast?.crossModuleReferences) continue;
            
            for (const ref of file.ast.crossModuleReferences) {
                const key = `${file.filePath}->${ref.target}`;
                if (!referenceMap.has(key)) {
                    referenceMap.set(key, {
                        source: file.filePath,
                        target: ref.target,
                        references: [],
                        language: file.ast.language
                    });
                }
                
                referenceMap.get(key).references.push({
                    line: ref.line,
                    fullReference: ref.fullReference,
                    type: ref.type
                });
            }
        }
        
        return Array.from(referenceMap.values());
    }

    async delegateComplexTasks(parseResults) {
        const complexFiles = [];
        const delegatedTasks = [];
        
        // Identify files that need special processing
        for (const file of parseResults) {
            if (file.error || !file.ast?.metrics) continue;
            
            const complexity = this.assessFileComplexity(file.ast.metrics);
            
            if (complexity.requiresDelegation) {
                complexFiles.push({
                    ...file,
                    complexityScore: complexity.score,
                    delegationReasons: complexity.reasons
                });
            }
        }
        
        // Process complex files with enhanced analysis
        for (const complexFile of complexFiles) {
            const enhancedAnalysis = await this.performEnhancedAnalysis(complexFile);
            delegatedTasks.push(enhancedAnalysis);
        }
        
        return {
            complexFiles: complexFiles.map(f => ({
                filePath: f.filePath,
                complexityScore: f.complexityScore,
                delegationReasons: f.delegationReasons
            })),
            enhancedAnalyses: delegatedTasks
        };
    }

    assessFileComplexity(metrics) {
        const reasons = [];
        let score = 0;
        
        if (metrics.cyclomaticComplexity > this.complexityThresholds.cyclomaticComplexity) {
            reasons.push(`High cyclomatic complexity: ${metrics.cyclomaticComplexity}`);
            score += metrics.cyclomaticComplexity;
        }
        
        if (metrics.maxNestingDepth > this.complexityThresholds.nestingDepth) {
            reasons.push(`Deep nesting: ${metrics.maxNestingDepth} levels`);
            score += metrics.maxNestingDepth * 2;
        }
        
        if (metrics.linesOfCode > this.complexityThresholds.linesOfCode) {
            reasons.push(`Large file: ${metrics.linesOfCode} lines`);
            score += Math.floor(metrics.linesOfCode / 50);
        }
        
        if (metrics.functionCount > this.complexityThresholds.functionCount) {
            reasons.push(`Many functions: ${metrics.functionCount}`);
            score += metrics.functionCount;
        }
        
        return {
            score,
            reasons,
            requiresDelegation: score > 15 || reasons.length >= 2
        };
    }

    async performEnhancedAnalysis(complexFile) {
        // Simulate enhanced analysis with more detailed processing
        return {
            filePath: complexFile.filePath,
            analysisType: 'enhanced',
            timestamp: new Date().toISOString(),
            detailedMetrics: {
                semanticComplexity: this.calculateSemanticComplexity(complexFile),
                maintainabilityIndex: this.calculateMaintainabilityIndex(complexFile),
                technicalDebt: this.assessTechnicalDebt(complexFile)
            },
            recommendations: this.generateRecommendations(complexFile)
        };
    }

    calculateSemanticComplexity(complexFile) {
        // Simplified semantic complexity calculation
        const metrics = complexFile.ast.metrics;
        const baseComplexity = metrics.cyclomaticComplexity;
        const semanticFactor = metrics.semanticDiversity.size / 10;
        
        return Math.round(baseComplexity * (1 + semanticFactor) * 100) / 100;
    }

    calculateMaintainabilityIndex(complexFile) {
        // Simplified maintainability index calculation
        const metrics = complexFile.ast.metrics;
        const volume = metrics.linesOfCode;
        const complexity = metrics.cyclomaticComplexity;
        
        // Microsoft maintainability index formula (simplified)
        const maintainability = Math.max(0, 
            171 - 5.2 * Math.log(volume) - 0.23 * complexity - 16.2 * Math.log(metrics.linesOfCode)
        );
        
        return Math.round(maintainability * 100) / 100;
    }

    assessTechnicalDebt(complexFile) {
        const debt = [];
        const metrics = complexFile.ast.metrics;
        
        if (metrics.cyclomaticComplexity > 20) {
            debt.push({
                type: 'complexity',
                severity: 'high',
                description: 'Very high cyclomatic complexity indicates need for refactoring',
                estimatedHours: metrics.cyclomaticComplexity * 2
            });
        }
        
        if (metrics.maxNestingDepth > 6) {
            debt.push({
                type: 'nesting',
                severity: 'medium',
                description: 'Deep nesting makes code hard to understand',
                estimatedHours: metrics.maxNestingDepth * 3
            });
        }
        
        if (metrics.linesOfCode > 500) {
            debt.push({
                type: 'size',
                severity: 'medium',
                description: 'Large file should be split into smaller modules',
                estimatedHours: Math.floor(metrics.linesOfCode / 100) * 4
            });
        }
        
        return debt;
    }

    generateRecommendations(complexFile) {
        const recommendations = [];
        const metrics = complexFile.ast.metrics;
        
        if (metrics.cyclomaticComplexity > 10) {
            recommendations.push({
                type: 'refactoring',
                priority: 'high',
                description: 'Extract complex methods into smaller, focused functions',
                impact: 'improves readability and testability'
            });
        }
        
        if (metrics.maxNestingDepth > 4) {
            recommendations.push({
                type: 'structure',
                priority: 'medium',
                description: 'Reduce nesting depth using early returns or guard clauses',
                impact: 'reduces cognitive load'
            });
        }
        
        if (metrics.functionCount > 15) {
            recommendations.push({
                type: 'organization',
                priority: 'low',
                description: 'Consider splitting this file into multiple focused modules',
                impact: 'improves maintainability'
            });
        }
        
        return recommendations;
    }

    getAnalysisStatus(analysisId) {
        return this.activeTasks.get(analysisId);
    }

    cancelAnalysis(analysisId) {
        const analysis = this.activeTasks.get(analysisId);
        if (analysis) {
            analysis.status = 'cancelled';
            analysis.endTime = new Date().toISOString();
            return true;
        }
        return false;
    }
}

module.exports = new AnalysisOrchestrator();
