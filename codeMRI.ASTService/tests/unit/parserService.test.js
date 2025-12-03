const { expect } = require('chai');
const ParserService = require('../../src/services/parserService'); // Adjust path as needed

describe('ParserService', () => {
    let parserService;

    before(() => {
        parserService = new ParserService();
    });

    it('should return a list of supported languages', () => {
        const languages = parserService.getSupportedLanguages();
        expect(languages).to.be.an('array');
        expect(languages.length).to.be.above(0);
        expect(languages).to.include('javascript');
        expect(languages).to.include('python');
        // Add more assertions for other expected languages if necessary
    });

    it('should parse a simple JavaScript code snippet and return raw node', async () => {
        const code = 'function hello() { console.log("world"); }';
        const language = 'javascript';

        const rootNode = await parserService.parseCode(code, language, '', true); // Request raw node

        expect(rootNode).to.be.an('object');
        // Basic checks for a tree-sitter Node object
        expect(rootNode.type).to.equal('program');
        expect(rootNode.text).to.equal(code);

        // Further assertions on the AST structure using children directly
        expect(rootNode.children).to.have.lengthOf(1); // Expect one function declaration
        const functionNode = rootNode.children[0];
        expect(functionNode.type).to.equal('function_declaration');
        expect(functionNode.child(1).text).to.equal('hello'); // Assuming child(1) is the name
    });

    // Add more tests for other languages, edge cases, error handling, etc.
});