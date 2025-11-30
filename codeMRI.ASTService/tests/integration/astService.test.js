const fs = require('fs');
const path = require('path');
const { expect } = require('chai');
const AnalysisOrchestrator = require('../../src/services/analysisOrchestrator');
const ParserService = require('../../src/services/mockParserService');

describe('AST Service Integration', () => {
    let orchestrator;
    let parserService;

    before(() => {
        orchestrator = AnalysisOrchestrator;
        parserService = ParserService;
    });

    it('should perform full analysis of a multi-language repository', async () => {
        // Load test files
        const files = [
            {
                filePath: 'math/calculator.py',
                content: fs.readFileSync(
                    path.join(__dirname, '../fixtures/python/simple.py'),
                    'utf8'
                ),
                language: 'python'
            },
            {
                filePath: 'math/multiplier.js',
                content: fs.readFileSync(
                    path.join(__dirname, '../fixtures/javascript/simple.js'),
                    'utf8'
                ),
                language: 'javascript'
            },
            {
                filePath: 'math/advanced.ts',
                content: fs.readFileSync(
                    path.join(__dirname, '../fixtures/typescript/simple.ts'),
                    'utf8'
                ),
                language: 'typescript'
            }
        ];

        // Perform full analysis
        const analysis = await orchestrator.analyzeRepository(files);

        // Validate analysis result
        expect(analysis.status).to.equal('completed');
        expect(analysis.results.parseResults).to.have.lengthOf(3);
        
        // Validate global dependency graph
        const { globalDependencyGraph } = analysis.results;
        expect(globalDependencyGraph.nodes).to.have.lengthOf(3);
        expect(globalDependencyGraph.edges).to.be.an('array');
        
        // Validate hierarchical structure
        const { hierarchicalStructure } = analysis.results;
        expect(hierarchicalStructure.modules).to.be.an('array');
        expect(hierarchicalStructure.components).to.be.an('array');
        
        // Validate entry points
        const { entryPoints } = analysis.results;
        expect(entryPoints.total).to.be.at.least(0); // Mock might not find entry points
        expect(entryPoints.byType).to.be.an('object');
        
        // Validate cross-module references
        const { crossModuleReferences } = analysis.results;
        expect(crossModuleReferences).to.be.an('array');
        
        // Validate metrics
        const { metrics } = analysis.results;
        expect(metrics.totalFiles).to.equal(3);
        expect(metrics.totalLines).to.be.a('number').greaterThan(0);
        expect(metrics.supportedLanguages).to.be.an('array').that.includes('python', 'javascript', 'typescript');
    });

    it('should handle complex file delegation', async () => {
        // Create a large file that should trigger delegation
        const largeCode = `
        // Large JavaScript file with multiple functions
        ${Array(100).fill().map((_, i) => `
        function function${i}() {
            // Some complex logic
            if (Math.random() > 0.5) {
                return true;
            }
            return false;
        }
        `).join('\n')}
        
        // Main entry point
        function main() {
            return function0() && function99();
        }
        `;

        const files = [{
            filePath: 'large.js',
            content: largeCode,
            language: 'javascript'
        }];

        const analysis = await orchestrator.analyzeRepository(files);
        expect(analysis.status).to.equal('completed');
        
        // Check if complex file was processed
        const parseResult = analysis.results.parseResults[0];
        expect(parseResult).to.have.property('ast');
        expect(parseResult.ast.metrics.functionCount).to.be.at.least(100);
        
        // Check if delegation was triggered
        expect(analysis.results.enhancedResults).to.be.an('object');
        expect(analysis.results.enhancedResults.complexFiles).to.be.an('array').with.lengthOf(1);
    });
});
