const fs = require('fs');
const path = require('path');
const { expect } = require('chai');
const AnalysisOrchestrator = require('../../src/services/analysisOrchestrator');
const ParserService = require('../../src/services/mockParserService');

describe('AnalysisOrchestrator', () => {
    let orchestrator;
    let parserService;

    before(() => {
        orchestrator = AnalysisOrchestrator;
        parserService = ParserService;
    });

    describe('analyzeRepository', () => {
        it('should analyze a repository with multiple files', async () => {
            const files = [
                {
                    filePath: 'test1.py',
                    content: fs.readFileSync(
                        path.join(__dirname, '../fixtures/python/simple.py'),
                        'utf8'
                    ),
                    language: 'python'
                },
                {
                    filePath: 'test2.js',
                    content: fs.readFileSync(
                        path.join(__dirname, '../fixtures/javascript/simple.js'),
                        'utf8'
                    ),
                    language: 'javascript'
                }
            ];

            const analysis = await orchestrator.analyzeRepository(files);
            
            // Validate analysis result
            expect(analysis).to.have.property('id');
            expect(analysis.status).to.equal('completed');
            expect(analysis.results).to.be.an('object');
            
            // Validate parse results
            expect(analysis.results.parseResults).to.be.an('array');
            expect(analysis.results.parseResults).to.have.lengthOf(2);
            
            // Validate global dependency graph
            expect(analysis.results.globalDependencyGraph).to.be.an('object');
            expect(analysis.results.globalDependencyGraph.nodes).to.be.an('array');
            expect(analysis.results.globalDependencyGraph.edges).to.be.an('array');
            
            // Validate hierarchical structure
            expect(analysis.results.hierarchicalStructure).to.be.an('object');
            expect(analysis.results.hierarchicalStructure.modules).to.be.an('array');
            
            // Validate entry points
            expect(analysis.results.entryPoints).to.be.an('object');
            expect(analysis.results.entryPoints.total).to.be.a('number').greaterThan(0);
        });

        it('should handle empty repository', async () => {
            const analysis = await orchestrator.analyzeRepository([]);
            
            expect(analysis.status).to.equal('completed');
            expect(analysis.results.parseResults).to.be.an('array').that.is.empty;
            expect(analysis.results.globalDependencyGraph.nodes).to.be.an('array').that.is.empty;
        });

        it('should handle files with errors', async () => {
            const files = [
                {
                    filePath: 'invalid.py',
                    content: 'invalid python syntax if else',
                    language: 'python'
                }
            ];

            const analysis = await orchestrator.analyzeRepository(files);
            expect(analysis.status).to.equal('completed');
            expect(analysis.results.parseResults[0].error).to.be.a('string');
        });
    });

    describe('buildGlobalDependencyGraph', () => {
        it('should build a dependency graph from parse results', async () => {
            const parseResults = [
                {
                    filePath: 'file1.py',
                    ast: {
                        language: 'python',
                        dependencyGraph: {
                            dependencies: ['file2'],
                            exports: ['function1']
                        }
                    }
                },
                {
                    filePath: 'file2.py',
                    ast: {
                        language: 'python',
                        dependencyGraph: {
                            dependencies: [],
                            exports: ['function2']
                        }
                    }
                }
            ];

            const graph = await orchestrator.buildGlobalDependencyGraph(parseResults);
            
            expect(graph.nodes).to.have.lengthOf(2);
            expect(graph.edges).to.have.lengthOf(1);
            expect(graph.edges[0]).to.have.property('source', 'file1.py');
            expect(graph.edges[0]).to.have.property('target', 'file2.py');
        });
    });

    describe('identifyReusableComponents', () => {
        it('should identify components with multiple dependents', async () => {
            const parseResults = [
                {
                    filePath: 'utils.js',
                    ast: {
                        dependencyGraph: {
                            exports: ['helper1', 'helper2']
                        }
                    }
                },
                {
                    filePath: 'app.js',
                    ast: {
                        dependencyGraph: {
                            dependencies: ['utils.js']
                        }
                    }
                },
                {
                    filePath: 'worker.js',
                    ast: {
                        dependencyGraph: {
                            dependencies: ['utils.js']
                        }
                    }
                }
            ];

            const dependencyGraph = {
                nodes: [
                    { id: 'utils.js' },
                    { id: 'app.js' },
                    { id: 'worker.js' }
                ],
                edges: [
                    { source: 'app.js', target: 'utils.js' },
                    { source: 'worker.js', target: 'utils.js' }
                ]
            };

            const components = orchestrator.identifyReusableComponents(parseResults, dependencyGraph);
            
            expect(components).to.be.an('array');
            expect(components).to.have.lengthOf(1);
            expect(components[0].filePath).to.equal('utils.js');
            expect(components[0].dependents).to.equal(2);
        });
    });
});
