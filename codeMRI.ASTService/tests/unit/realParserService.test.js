const fs = require('fs');
const path = require('path');
const ParserService = require('../../src/services/parserService');
const { expect } = require('chai');

describe('RealParserService', () => {
    
    describe('getSupportedLanguages', () => {
        it('should return list of supported languages', () => {
            const languages = ParserService.getSupportedLanguages();
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
            const result = await ParserService.parseCode(code, 'typescript', 'test.ts');
            expect(result).to.have.property('language', 'typescript');
            expect(result.tree).to.be.an('object');
        });

        // Python
        it('should parse Python code correctly', async () => {
            const code = fs.readFileSync(
                path.join(__dirname, '../fixtures/python/simple.py'), 
                'utf8'
            );
            const result = await ParserService.parseCode(code, 'python', 'test.py');
            expect(result).to.have.property('language', 'python');
            expect(result.metrics.functionCount).to.be.greaterThan(0);
        });

        // JavaScript
        it('should parse JavaScript code correctly', async () => {
            const code = fs.readFileSync(
                path.join(__dirname, '../fixtures/javascript/simple.js'), 
                'utf8'
            );
            const result = await ParserService.parseCode(code, 'javascript', 'test.js');
            expect(result).to.have.property('language', 'javascript');
            expect(result.metrics.functionCount).to.be.greaterThan(0);
        });

        // Java
        it('should parse Java code correctly', async () => {
            const code = fs.readFileSync(
                path.join(__dirname, '../fixtures/java/simple.java'), 
                'utf8'
            );
            const result = await ParserService.parseCode(code, 'java', 'Simple.java');
            expect(result).to.have.property('language', 'java');
            expect(result.metrics.classCount).to.be.greaterThan(0);
        });

        // C
        it('should parse C code correctly', async () => {
            const code = fs.readFileSync(
                path.join(__dirname, '../fixtures/c/simple.c'), 
                'utf8'
            );
            const result = await ParserService.parseCode(code, 'c', 'simple.c');
            expect(result).to.have.property('language', 'c');
            expect(result.metrics.functionCount).to.be.greaterThan(0);
        });

        // C++
        it('should parse C++ code correctly', async () => {
            const code = fs.readFileSync(
                path.join(__dirname, '../fixtures/cpp/simple.cpp'), 
                'utf8'
            );
            const result = await ParserService.parseCode(code, 'cpp', 'simple.cpp');
            expect(result).to.have.property('language', 'cpp');
            expect(result.metrics.classCount).to.be.greaterThan(0);
        });

        // C#
        it('should parse C# code correctly', async () => {
            const code = fs.readFileSync(
                path.join(__dirname, '../fixtures/csharp/simple.cs'), 
                'utf8'
            );
            const result = await ParserService.parseCode(code, 'csharp', 'simple.cs');
            expect(result).to.have.property('language', 'csharp');
            expect(result.metrics.classCount).to.be.greaterThan(0);
        });

    });
});
