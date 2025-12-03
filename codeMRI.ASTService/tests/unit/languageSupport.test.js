const chai = require('chai');
const expect = chai.expect;
const ParserService = require('../../src/services/parserService');

describe('Language Support & Parsing Tests', () => {
    let parserService;

    before(() => {
        parserService = new ParserService();
    });

    describe('Python Parsing', () => {
        it('should parse decorators correctly', async () => {
            const code = `
@app.route("/api")
@auth_required
def get_data():
    pass
            `;
            const result = await parserService.parseCode(code, 'python', 'test.py');
            const funcNode = result.dependencyGraph.Nodes.find(n => n.Properties && n.Properties.Decorators);
            expect(funcNode).to.not.be.undefined;
            const decorators = funcNode.Properties.Decorators;
            expect(decorators.some(d => d.includes('app.route'))).to.be.true;
            expect(decorators).to.include('auth_required');
        });

        it('should parse list comprehensions', async () => {
            const code = `squares = [x**2 for x in range(10)]`;
            const result = await parserService.parseCode(code, 'python', 'test.py');
            // Expect complexity to increase or specific node types
            expect(result.metrics.cyclomaticComplexity).to.be.greaterThan(0);
        });
    });

    describe('Java Parsing', () => {
        it('should parse annotations', async () => {
            const code = `
@RestController
@RequestMapping("/api")
public class MyController {
    @GetMapping
    public void get() {}
}
            `;
            const result = await parserService.parseCode(code, 'java', 'MyController.java');
            const classNode = result.dependencyGraph.Nodes.find(n => n.Type === 'classes');
            expect(classNode).to.not.be.undefined;
            expect(classNode.Properties.Annotations).to.include('RestController');
        });
    });

    describe('C# Parsing', () => {
        it('should parse attributes', async () => {
            const code = `
[ApiController]
[Route("api/[controller]")]
public class MyController : ControllerBase {
    [HttpGet]
    public IActionResult Get() => Ok();
}
            `;
            const result = await parserService.parseCode(code, 'csharp', 'MyController.cs');
            const classNode = result.dependencyGraph.Nodes.find(n => n.Type === 'classes');
            expect(classNode).to.not.be.undefined;
            expect(classNode.Properties.Annotations).to.include('ApiController');
        });
    });

    describe('C++ Parsing', () => {
        it('should parse templates', async () => {
            const code = `
template <typename T>
class MyStack {
    T items[100];
};
            `;
            const result = await parserService.parseCode(code, 'cpp', 'stack.cpp');
            const templateNode = result.dependencyGraph.Nodes.find(n => n.Type === 'TemplateDefinition');
            expect(templateNode).to.not.be.undefined;
            expect(templateNode.Id).to.contain('MyStack');
        });

        it('should parse macros', async () => {
            const code = `#define MAX_SIZE 100`;
            const result = await parserService.parseCode(code, 'c', 'defs.h');
            const macroNode = result.dependencyGraph.Nodes.find(n => n.Type === 'MacroDefinition');
            expect(macroNode).to.not.be.undefined;
            expect(macroNode.Id).to.contain('MAX_SIZE');
        });
    });

    describe('Cross-Language References', () => {
         it('should identify entry points', async () => {
            const code = `
def main():
    print("Start")

if __name__ == "__main__":
    main()
            `;
            const result = await parserService.parseCode(code, 'python', 'main.py');
            expect(result.entryPoints).to.have.length.greaterThan(0);
            expect(result.entryPoints[0].name).to.equal('main');
         });
    });
});
