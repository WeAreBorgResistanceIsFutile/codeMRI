const Parser = require('tree-sitter');
const Python = require('tree-sitter-python');
const JavaScript = require('tree-sitter-javascript');
const TypeScript = require('tree-sitter-typescript');
const SQL = require('tree-sitter-sql');
const Java = require('tree-sitter-java');
const C = require('tree-sitter-c');
const Cpp = require('tree-sitter-cpp');
const CSharp = require('tree-sitter-c-sharp');
const _ = require('lodash');
const { v4: uuidv4 } = require('uuid');

class ParserService {
    constructor() {
        this.parsers = new Map();
        this.initializeParsers();
    }

    initializeParsers() {
        // Initialize Python parser
        const pythonParser = new Parser();
        pythonParser.setLanguage(Python);
        this.parsers.set('python', pythonParser);
        this.parsers.set('py', pythonParser);

        // Initialize JavaScript parser
        const javascriptParser = new Parser();
        javascriptParser.setLanguage(JavaScript);
        this.parsers.set('javascript', javascriptParser);
        this.parsers.set('js', javascriptParser);

        // Initialize TypeScript parser
        const typescriptParser = new Parser();
        typescriptParser.setLanguage(TypeScript.typescript);
        this.parsers.set('typescript', typescriptParser);
        this.parsers.set('ts', typescriptParser);

        // Initialize TSX parser
        const tsxParser = new Parser();
        tsxParser.setLanguage(TypeScript.tsx);
        this.parsers.set('tsx', tsxParser);

        // Initialize SQL parser
        const sqlParser = new Parser();
        sqlParser.setLanguage(SQL);
        this.parsers.set('sql', sqlParser);

        // Initialize Java parser
        const javaParser = new Parser();
        javaParser.setLanguage(Java);
        this.parsers.set('java', javaParser);

        // Initialize C parser
        const cParser = new Parser();
        cParser.setLanguage(C);
        this.parsers.set('c', cParser);

        // Initialize C++ parser
        const cppParser = new Parser();
        cppParser.setLanguage(Cpp);
        this.parsers.set('cpp', cppParser);
        this.parsers.set('cxx', cppParser);
        this.parsers.set('c++', cppParser);

        // Initialize C# parser
        const csharpParser = new Parser();
        csharpParser.setLanguage(CSharp);
        this.parsers.set('csharp', csharpParser);
        this.parsers.set('cs', csharpParser);
        this.parsers.set('c#', csharpParser);
    }

    getSupportedLanguages() {
        return ['python', 'javascript', 'typescript', 'tsx', 'sql', 'java', 'c', 'cpp', 'cxx', 'c++', 'csharp', 'cs', 'c#'];
    }

    async parseCode(code, language, filePath = '') {
        const parser = this.parsers.get(language.toLowerCase());
        
        if (!parser) {
            throw new Error(`Unsupported language: ${language}`);
        }

        const tree = parser.parse(code);
        const rootNode = tree.rootNode;

        const basicMetrics = this.calculateMetrics(rootNode, code);
        const advancedMetrics = this.calculateAdvancedMetrics(rootNode, code, language);
        const dependencyGraph = this.buildDependencyGraph(rootNode, language, filePath);
        const entryPoints = this.identifyEntryPoints(rootNode, language, filePath);
        const hierarchicalStructure = this.performHierarchicalDecomposition(rootNode, language, filePath);

        return {
            language,
            filePath,
            tree: this.serializeNode(rootNode),
            timestamp: new Date().toISOString(),
            metrics: {
                ...basicMetrics,
                ...advancedMetrics
            },
            dependencyGraph,
            entryPoints,
            hierarchicalStructure,
            crossModuleReferences: this.extractCrossModuleReferences(rootNode, language)
        };
    }

    serializeNode(node) {
        if (!node) return null;

        return {
            type: node.type,
            text: node.text,
            startPosition: {
                row: node.startPosition.row,
                column: node.startPosition.column
            },
            endPosition: {
                row: node.endPosition.row,
                column: node.endPosition.column
            },
            children: node.children.map(child => this.serializeNode(child))
        };
    }

    calculateMetrics(rootNode, code) {
        const lines = code.split('\n').length;
        const nodes = this.countNodes(rootNode);
        const functions = this.countFunctions(rootNode);
        const classes = this.countClasses(rootNode);
        
        return {
            linesOfCode: lines,
            totalNodes: nodes,
            functionCount: functions,
            classCount: classes,
            avgFunctionLength: lines / Math.max(functions, 1)
        };
    }

    countNodes(node) {
        if (!node || !node.children) return 0;
        
        let count = 1; // Count this node
        for (const child of node.children) {
            count += this.countNodes(child);
        }
        return count;
    }

    countFunctions(node) {
        return this.countNodesByType(node, [
            'function_definition', // Python
            'function_declaration', // JavaScript
            'function_definition', // TypeScript
            'arrow_function', // TypeScript/JavaScript
            'method_definition', // TypeScript/JavaScript
            'method_declaration', // Java
            'constructor_declaration' // Java
        ]);
    }

    countClasses(node) {
        return this.countNodesByType(node, [
            'class_definition',    // Python
            'class_declaration',   // JavaScript/TypeScript
            'class_expression',    // JavaScript/TypeScript
            'class_declaration'    // Java
        ]);
    }

    countNodesByType(node, types) {
        if (!node || !node.children) return 0;
        
        let count = types.includes(node.type) ? 1 : 0;
        for (const child of node.children) {
            count += this.countNodesByType(child, types);
        }
        return count;
    }

    calculateAdvancedMetrics(rootNode, code, language) {
        return {
            cyclomaticComplexity: this.calculateCyclomaticComplexity(rootNode),
            maxNestingDepth: this.calculateMaxNestingDepth(rootNode),
            semanticDiversity: this.calculateSemanticDiversity(rootNode),
            contextWindowUtilization: this.estimateContextWindowUtilization(rootNode, code),
            functionCalls: this.countFunctionCalls(rootNode, language),
            classInheritance: this.countClassInheritance(rootNode, language),
            attributeAccess: this.countAttributeAccess(rootNode, language),
            moduleImports: this.countModuleImports(rootNode, language)
        };
    }

    calculateCyclomaticComplexity(node) {
        if (!node || !node.children) return 0;
        
        let complexity = 1; // Base complexity
        
        // Decision points that increase complexity
        const decisionTypes = [
            'if_statement', 'else_clause', 'elif_clause',
            'while_statement', 'for_statement', 'for_in_statement',
            'switch_statement', 'case', 'catch_clause',
            'conditional_expression', 'binary_expression'
        ];
        
        if (decisionTypes.includes(node.type)) {
            complexity++;
        }
        
        // Count logical operators in binary expressions
        if (node.type === 'binary_expression') {
            const text = node.text;
            complexity += (text.match(/&&|&|\|\||\|/g) || []).length;
        }
        
        for (const child of node.children) {
            complexity += this.calculateCyclomaticComplexity(child);
        }
        
        return complexity;
    }

    calculateMaxNestingDepth(node, currentDepth = 0) {
        if (!node || !node.children) return currentDepth;
        
        let maxDepth = currentDepth;
        
        // Blocks that increase nesting depth
        const nestingTypes = [
            'if_statement', 'while_statement', 'for_statement',
            'for_in_statement', 'switch_statement', 'try_statement',
            'catch_clause', 'finally_clause', 'function_definition',
            'function_declaration', 'method_definition', 'class_definition',
            'class_declaration', 'block', 'compound_statement'
        ];
        
        const nextDepth = nestingTypes.includes(node.type) ? currentDepth + 1 : currentDepth;
        
        for (const child of node.children) {
            const childDepth = this.calculateMaxNestingDepth(child, nextDepth);
            maxDepth = Math.max(maxDepth, childDepth);
        }
        
        return maxDepth;
    }

    calculateSemanticDiversity(node) {
        if (!node || !node.children) return new Set();
        
        const semanticTypes = new Set();
        
        // Collect different semantic constructs
        const semanticConstructs = [
            'function_definition', 'function_declaration', 'method_definition',
            'class_definition', 'class_declaration', 'interface_declaration',
            'enum_declaration', 'struct_declaration', 'union_declaration',
            'variable_declaration', 'constant_declaration', 'parameter_declaration',
            'if_statement', 'while_statement', 'for_statement', 'switch_statement',
            'try_statement', 'catch_clause', 'throw_statement',
            'import_statement', 'export_statement', 'module_declaration'
        ];
        
        if (semanticConstructs.includes(node.type)) {
            semanticTypes.add(node.type);
        }
        
        for (const child of node.children) {
            const childTypes = this.calculateSemanticDiversity(child);
            childTypes.forEach(type => semanticTypes.add(type));
        }
        
        return semanticTypes;
    }

    estimateContextWindowUtilization(rootNode, code) {
        const tokenEstimate = this.estimateTokenCount(code);
        const typicalContextWindow = 8192; // Typical context window size
        return {
            estimatedTokens: tokenEstimate,
            utilizationPercentage: Math.round((tokenEstimate / typicalContextWindow) * 100),
            requiresChunking: tokenEstimate > typicalContextWindow * 0.8
        };
    }

    estimateTokenCount(code) {
        // Rough estimation: ~4 characters per token
        return Math.ceil(code.length / 4);
    }

    countFunctionCalls(node, language) {
        if (!node || !node.children) return 0;
        
        let count = 0;
        const callTypes = this.getFunctionCallTypes(language);
        
        if (callTypes.includes(node.type)) {
            count++;
        }
        
        for (const child of node.children) {
            count += this.countFunctionCalls(child, language);
        }
        
        return count;
    }

    getFunctionCallTypes(language) {
        const typeMap = {
            python: ['call_expression'],
            javascript: ['call_expression', 'new_expression'],
            typescript: ['call_expression', 'new_expression'],
            java: ['method_invocation', 'class_creator'],
            c: ['call_expression'],
            cpp: ['call_expression', 'new_expression'],
            csharp: ['invocation_expression', 'object_creation_expression']
        };
        
        return typeMap[language] || ['call_expression'];
    }

    countClassInheritance(node, language) {
        if (!node || !node.children) return 0;
        
        let count = 0;
        const inheritanceTypes = this.getInheritanceTypes(language);
        
        if (inheritanceTypes.includes(node.type)) {
            count++;
        }
        
        for (const child of node.children) {
            count += this.countClassInheritance(child, language);
        }
        
        return count;
    }

    getInheritanceTypes(language) {
        const typeMap = {
            python: ['argument_list'], // In class definitions
            javascript: ['class_heritage'],
            typescript: ['class_heritage', 'extends_clause', 'implements_clause'],
            java: ['extends_clause', 'implements_clause'],
            cpp: ['base_class_clause', 'access_specifier'],
            csharp: ['base_list', 'interface_list']
        };
        
        return typeMap[language] || [];
    }

    countAttributeAccess(node, language) {
        if (!node || !node.children) return 0;
        
        let count = 0;
        const accessTypes = this.getAttributeAccessTypes(language);
        
        if (accessTypes.includes(node.type)) {
            count++;
        }
        
        for (const child of node.children) {
            count += this.countAttributeAccess(child, language);
        }
        
        return count;
    }

    getAttributeAccessTypes(language) {
        const typeMap = {
            python: ['attribute', 'subscript'],
            javascript: ['member_expression', 'computed_member_expression'],
            typescript: ['member_expression', 'computed_member_expression'],
            java: ['field_access', 'method_invocation'],
            cpp: ['field_expression', 'call_expression'],
            csharp: ['member_access_expression']
        };
        
        return typeMap[language] || ['member_expression'];
    }

    countModuleImports(node, language) {
        if (!node || !node.children) return 0;
        
        let count = 0;
        const importTypes = this.getImportTypes(language);
        
        if (importTypes.includes(node.type)) {
            count++;
        }
        
        for (const child of node.children) {
            count += this.countModuleImports(child, language);
        }
        
        return count;
    }

    getImportTypes(language) {
        const typeMap = {
            python: ['import_statement', 'import_from_statement', 'future_import_statement'],
            javascript: ['import_statement', 'export_statement'],
            typescript: ['import_statement', 'export_statement'],
            java: ['import_declaration'],
            c: ['preproc_include'],
            cpp: ['preproc_include', 'using_directive', 'namespace_declaration'],
            csharp: ['using_directive']
        };
        
        return typeMap[language] || ['import_statement'];
    }

    buildDependencyGraph(rootNode, language, filePath) {
        const dependencies = new Set();
        const exports = new Set();
        
        this.extractDependencies(rootNode, language, dependencies, exports);
        
        return {
            filePath,
            dependencies: Array.from(dependencies),
            exports: Array.from(exports),
            timestamp: new Date().toISOString()
        };
    }

    extractDependencies(node, language, dependencies, exports) {
        if (!node || !node.children) return;
        
        const importTypes = this.getImportTypes(language);
        const exportTypes = this.getExportTypes(language);
        
        if (importTypes.includes(node.type)) {
            const dep = this.extractDependencyName(node, language);
            if (dep) dependencies.add(dep);
        }
        
        if (exportTypes.includes(node.type)) {
            const exp = this.extractExportName(node, language);
            if (exp) exports.add(exp);
        }
        
        for (const child of node.children) {
            this.extractDependencies(child, language, dependencies, exports);
        }
    }

    getExportTypes(language) {
        const typeMap = {
            python: ['function_definition', 'class_definition'],
            javascript: ['export_statement', 'function_declaration', 'class_declaration'],
            typescript: ['export_statement', 'function_declaration', 'class_declaration', 'interface_declaration'],
            java: ['class_declaration', 'interface_declaration', 'method_declaration'],
            c: ['function_definition'],
            cpp: ['function_definition', 'class_declaration'],
            csharp: ['class_declaration', 'interface_declaration', 'method_declaration']
        };
        
        return typeMap[language] || [];
    }

    extractDependencyName(node, language) {
        // Extract module/file name from import statements
        const text = node.text.trim();
        
        if (language === 'python') {
            const match = text.match(/(?:import|from)\s+([^\s;]+)/);
            return match ? match[1].split('.')[0] : null;
        } else if (['javascript', 'typescript'].includes(language)) {
            const match = text.match(/(?:import.*from|import)\s+['"]([^'"]+)['"]/);
            return match ? match[1].split('/')[0] : null;
        } else if (language === 'java') {
            const match = text.match(/import\s+([^;]+);/);
            return match ? match[1].split('.')[0] : null;
        }
        
        return null;
    }

    extractExportName(node, language) {
        // Extract exported function/class names
        if (node.type === 'function_definition' || node.type === 'function_declaration') {
            const nameNode = node.childForFieldName('name');
            return nameNode ? nameNode.text : null;
        } else if (node.type === 'class_definition' || node.type === 'class_declaration') {
            const nameNode = node.childForFieldName('name');
            return nameNode ? nameNode.text : null;
        }
        
        return null;
    }

    identifyEntryPoints(rootNode, language, filePath) {
        const entryPoints = [];
        
        this.findEntryPoints(rootNode, language, filePath, entryPoints);
        
        return entryPoints;
    }

    findEntryPoints(node, language, filePath, entryPoints) {
        if (!node || !node.children) return;
        
        const entryPointPatterns = this.getEntryPointPatterns(language);
        
        for (const pattern of entryPointPatterns) {
            if (this.matchesEntryPointPattern(node, pattern, language)) {
                entryPoints.push({
                    type: pattern.type,
                    name: this.extractEntryPointName(node, language),
                    filePath,
                    line: node.startPosition.row + 1,
                    signature: node.text.trim().substring(0, 100)
                });
            }
        }
        
        for (const child of node.children) {
            this.findEntryPoints(child, language, filePath, entryPoints);
        }
    }

    getEntryPointPatterns(language) {
        const patterns = {
            python: [
                { type: 'main_function', name: 'main' },
                { type: 'async_main', name: 'async_main' },
                { type: 'flask_route', nodeType: 'decorator' },
                { type: 'django_view', nodeType: 'function_definition' }
            ],
            javascript: [
                { type: 'main_function', name: 'main' },
                { type: 'express_route', nodeType: 'call_expression' },
                { type: 'event_listener', nodeType: 'call_expression' }
            ],
            typescript: [
                { type: 'main_function', name: 'main' },
                { type: 'nest_controller', nodeType: 'class_declaration' },
                { type: 'express_route', nodeType: 'call_expression' }
            ],
            java: [
                { type: 'main_method', name: 'main' },
                { type: 'servlet', nodeType: 'class_declaration' },
                { type: 'spring_controller', nodeType: 'class_declaration' }
            ],
            c: [
                { type: 'main_function', name: 'main' }
            ],
            cpp: [
                { type: 'main_function', name: 'main' },
                { type: 'constructor', nodeType: 'function_definition' }
            ],
            csharp: [
                { type: 'main_method', name: 'Main' },
                { type: 'webapi_controller', nodeType: 'class_declaration' }
            ]
        };
        
        return patterns[language] || [];
    }

    matchesEntryPointPattern(node, pattern, language) {
        if (pattern.name) {
            const nameNode = node.childForFieldName('name');
            return nameNode && nameNode.text === pattern.name;
        }
        
        if (pattern.nodeType) {
            return node.type === pattern.nodeType;
        }
        
        return false;
    }

    extractEntryPointName(node, language) {
        const nameNode = node.childForFieldName('name');
        return nameNode ? nameNode.text : 'unnamed';
    }

    performHierarchicalDecomposition(rootNode, language, filePath) {
        const decomposition = {
            filePath,
            language,
            modules: [],
            classes: [],
            functions: [],
            timestamp: new Date().toISOString()
        };
        
        this.extractHierarchicalStructure(rootNode, language, decomposition);
        
        return decomposition;
    }

    extractHierarchicalStructure(node, language, decomposition, parent = null) {
        if (!node || !node.children) return;
        
        const structureTypes = this.getStructureTypes(language);
        
        for (const structType of structureTypes) {
            if (node.type === structType.type) {
                const element = {
                    name: this.extractElementName(node, language),
                    type: structType.category,
                    line: node.startPosition.row + 1,
                    parent: parent,
                    children: [],
                    metrics: this.calculateElementMetrics(node)
                };
                
                decomposition[structType.category].push(element);
                
                // Process children
                for (const child of node.children) {
                    this.extractHierarchicalStructure(child, language, decomposition, element);
                }
                
                return;
            }
        }
        
        // Continue processing children
        for (const child of node.children) {
            this.extractHierarchicalStructure(child, language, decomposition, parent);
        }
    }

    getStructureTypes(language) {
        return [
            { type: 'class_definition', category: 'classes' },
            { type: 'class_declaration', category: 'classes' },
            { type: 'function_definition', category: 'functions' },
            { type: 'function_declaration', category: 'functions' },
            { type: 'method_definition', category: 'functions' },
            { type: 'method_declaration', category: 'functions' },
            { type: 'interface_declaration', category: 'classes' },
            { type: 'struct_declaration', category: 'classes' }
        ];
    }

    extractElementName(node, language) {
        const nameNode = node.childForFieldName('name');
        return nameNode ? nameNode.text : 'unnamed';
    }

    calculateElementMetrics(node) {
        return {
            lines: node.endPosition.row - node.startPosition.row + 1,
            complexity: this.calculateCyclomaticComplexity(node),
            nestingDepth: this.calculateMaxNestingDepth(node)
        };
    }

    extractCrossModuleReferences(node, language) {
        const references = [];
        
        this.findCrossModuleReferences(node, language, references);
        
        return references;
    }

    findCrossModuleReferences(node, language, references) {
        if (!node || !node.children) return;
        
        // Look for qualified names that might reference other modules
        if (node.type === 'qualified_name' || node.type === 'member_expression') {
            const parts = node.text.split('.');
            if (parts.length > 1) {
                references.push({
                    type: 'module_reference',
                    target: parts[0],
                    fullReference: node.text,
                    line: node.startPosition.row + 1
                });
            }
        }
        
        for (const child of node.children) {
            this.findCrossModuleReferences(child, language, references);
        }
    }
}

module.exports = new ParserService();
