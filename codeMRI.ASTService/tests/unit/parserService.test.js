const fs = require('fs');
const path = require('path');
const ParserService = require('../../src/services/mockParserService');
const { expect } = require('chai');

describe('ParserService', () => {
    let parserService;

    before(() => {
        parserService = ParserService;
    });

    describe('getSupportedLanguages', () => {
        it('should return list of supported languages', () => {
            const languages = parserService.getSupportedLanguages();
            expect(languages).to.be.an('array');
            expect(languages).to.include.members(['python', 'javascript', 'typescript']);
        });
    });

    describe('parseCode', () => {
        it('should parse Python code correctly', async () => {
            const code = fs.readFileSync(
                path.join(__dirname, '../fixtures/python/simple.py'), 
                'utf8'
            );
            const result = await parserService.parseCode(code, 'python', 'test.py');
            
            // Basic validation
            expect(result).to.have.property('language', 'python');
            expect(result).to.have.property('filePath', 'test.py');
            expect(result.tree).to.be.an('object');
            
            // Validate metrics
            expect(result.metrics).to.be.an('object');
            expect(result.metrics.linesOfCode).to.be.a('number').greaterThan(0);
            expect(result.metrics.functionCount).to.equal(2); // calculate_sum and Calculator.add
            expect(result.metrics.classCount).to.equal(1); // Calculator class
            
            // Validate dependency graph
            expect(result.dependencyGraph).to.be.an('object');
            expect(result.dependencyGraph.dependencies).to.be.an('array');
            expect(result.dependencyGraph.exports).to.be.an('array');
            
            // Validate entry points
            expect(result.entryPoints).to.be.an('array');
            expect(result.entryPoints).to.have.lengthOf(1); // __main__ block
            expect(result.entryPoints[0].type).to.equal('main_function');
        });

        it('should parse JavaScript code correctly', async () => {
            const code = fs.readFileSync(
                path.join(__dirname, '../fixtures/javascript/simple.js'), 
                'utf8'
            );
            const result = await parserService.parseCode(code, 'javascript', 'test.js');
            
            expect(result).to.have.property('language', 'javascript');
            expect(result.tree).to.be.an('object');
            expect(result.metrics.functionCount).to.equal(2); // calculateProduct and Multiplier.multiply
            expect(result.metrics.classCount).to.equal(1); // Multiplier class
            
            // Validate entry points
            expect(result.entryPoints).to.be.an('array');
            expect(result.entryPoints).to.have.lengthOf(1); // main function
            expect(result.entryPoints[0].type).to.equal('main_function');
        });

        it('should parse TypeScript code correctly', async () => {
            const code = fs.readFileSync(
                path.join(__dirname, '../fixtures/typescript/simple.ts'), 
                'utf8'
            );
            const result = await parserService.parseCode(code, 'typescript', 'test.ts');
            
            expect(result).to.have.property('language', 'typescript');
            expect(result.tree).to.be.an('object');
            expect(result.metrics.functionCount).to.equal(2); // add and create
            expect(result.metrics.classCount).to.equal(1); // Calculator class
        });

        it('should throw error for unsupported language', async () => {
            try {
                await parserService.parseCode('', 'unsupported', 'test.txt');
                throw new Error('Should have thrown an error');
            } catch (error) {
                expect(error.message).to.include('Unsupported language');
            }
        });
    });

    // Additional tests for specific parser functionality
    describe('AST Parsing Details', () => {
        it('should calculate cyclomatic complexity correctly', async () => {
            const code = `
            function test(x) {
                if (x > 0) {
                    return 1;
                } else if (x < 0) {
                    return -1;
                }
                return 0;
            }`;
            
            const result = await parserService.parseCode(code, 'javascript', 'complexity.js');
            expect(result.metrics.cyclomaticComplexity).to.equal(3); // Base + 2 conditions
        });

        it('should identify function calls', async () => {
            const code = `
            function a() {}
            function b() {
                a();
                console.log('test');
            }`;
            
            const result = await parserService.parseCode(code, 'javascript', 'calls.js');
            expect(result.metrics.functionCalls).to.equal(2); // a() and console.log()
        });
    });
});
