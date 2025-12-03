const fs = require('fs');
const path = require('path');
const ParserServiceClass = require('../../src/services/parserService'); // Rename to avoid conflict
const { expect } = require('chai');

describe('RealParserService', () => {
    let parserService;

    before(() => {
        parserService = new ParserServiceClass();
    });
    
    describe('getSupportedLanguages', () => {
        it('should return list of supported languages', () => {
            const languages = parserService.getSupportedLanguages();
            expect(languages).to.be.an('array');
            // We expect these to be enabled eventually
            expect(languages).to.include('typescript');
            // expect(languages).to.include('python');
            // expect(languages).to.include('javascript');
            // expect(languages).to.include('java');
            // expect(languages).to.include('c');
            // expect(languages).to.include('cpp');
            // expect(languages).to.include('csharp');
        });
    });

    describe('parseCode', () => {
        
        // TypeScript (already enabled)
        it('should parse TypeScript code correctly', async () => {
            const code = fs.readFileSync(
                path.join(__dirname, '../fixtures/typescript/simple.ts'), 
                'utf8'
            );
            const rootNode = await parserService.parseCode(code, 'typescript', 'test.ts', true); // Request raw node
            expect(rootNode).to.be.an('object');
            expect(rootNode.type).to.equal('program');
            expect(rootNode.text).to.equal(code);
            // Example: check if it has children
            expect(rootNode.children).to.not.be.empty;
        });

        // Python
        it('should parse Python code correctly', async () => {
            const code = fs.readFileSync(
                path.join(__dirname, '../fixtures/python/simple.py'), 
                'utf8'
            );
            const rootNode = await parserService.parseCode(code, 'python', 'test.py', true); // Request raw node
            expect(rootNode).to.be.an('object');
            expect(rootNode.type).to.equal('module');
            expect(rootNode.text).to.equal(code);
            // Example: check if it has children
            expect(rootNode.children).to.not.be.empty;
        });

        // JavaScript
        it('should parse JavaScript code correctly', async () => {
            const code = fs.readFileSync(
                path.join(__dirname, '../fixtures/javascript/simple.js'), 
                'utf8'
            );
            const rootNode = await parserService.parseCode(code, 'javascript', 'test.js', true); // Request raw node
            expect(rootNode).to.be.an('object');
            expect(rootNode.type).to.equal('program');
            expect(rootNode.text).to.equal(code);
            // Example: check if it has children
            expect(rootNode.children).to.not.be.empty;
        });

        // Java
        it('should parse Java code correctly', async () => {
            const code = fs.readFileSync(
                path.join(__dirname, '../fixtures/java/simple.java'), 
                'utf8'
            );
            const rootNode = await parserService.parseCode(code, 'java', 'Simple.java', true); // Request raw node
            expect(rootNode).to.be.an('object');
            expect(rootNode.type).to.equal('program');
            expect(rootNode.text).to.equal(code);
            // Example: check if it has children
            expect(rootNode.children).to.not.be.empty;
        });

        // C
        it('should parse C code correctly', async () => {
            const code = fs.readFileSync(
                path.join(__dirname, '../fixtures/c/simple.c'), 
                'utf8'
            );
            const rootNode = await parserService.parseCode(code, 'c', 'simple.c', true); // Request raw node
            expect(rootNode).to.be.an('object');
            expect(rootNode.type).to.equal('translation_unit');
            expect(rootNode.text).to.equal(code);
            // Example: check if it has children
            expect(rootNode.children).to.not.be.empty;
        });

        // C++
        it('should parse C++ code correctly', async () => {
            const code = fs.readFileSync(
                path.join(__dirname, '../fixtures/cpp/simple.cpp'), 
                'utf8'
            );
            const rootNode = await parserService.parseCode(code, 'cpp', 'simple.cpp', true); // Request raw node
            expect(rootNode).to.be.an('object');
            expect(rootNode.type).to.equal('translation_unit');
            expect(rootNode.text).to.equal(code);
            // Example: check if it has children
            expect(rootNode.children).to.not.be.empty;
        });

        // C#
        it('should parse C# code correctly', async () => {
            const code = fs.readFileSync(
                path.join(__dirname, '../fixtures/csharp/simple.cs'), 
                'utf8'
            );
            const rootNode = await parserService.parseCode(code, 'csharp', 'simple.cs', true); // Request raw node
            expect(rootNode).to.be.an('object');
            expect(rootNode.type).to.equal('compilation_unit');
            expect(rootNode.text).to.equal(code);
            // Example: check if it has children
            expect(rootNode.children).to.not.be.empty;
        });

    });
});
