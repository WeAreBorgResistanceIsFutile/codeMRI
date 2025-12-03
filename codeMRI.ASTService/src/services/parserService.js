const Parser = require('tree-sitter');
const _ = require('lodash');
const { v4: uuidv4 } = require('uuid');

class ParserService {
    constructor() {
        this.parsers = new Map();
        this.initializeParsers();
    }

    loadLanguage(moduleName, propertyName = 'language') {
        try {
            const module = require(moduleName);
            
            // Priority 1: Specific property (typescript/tsx) or 'language'
            if (propertyName && module[propertyName]) {
                return module[propertyName];
            }
            if (module.language) {
                return module.language;
            }

            // Priority 2: Default export
            if (module.default) {
                return module.default;
            }
            
            // Priority 3: The module itself (if it's a native object/function)
            if (typeof module === 'object') {
                return module;
            }

            return module;
        } catch (error) {
            console.error(`Failed to load language module ${moduleName}:`, error.message);
            return null;
        }
    }

    initializeParsers() {
        console.log('Initializing parsers...');
        
        try {
            console.log('Initializing Python parser...');
            const pythonLang = this.loadLanguage('tree-sitter-python');
            if (pythonLang) {
                const pythonParser = new Parser();
                pythonParser.setLanguage(pythonLang);
                this.parsers.set('python', pythonParser);
                console.log('Python parser initialized successfully');
            }
        } catch (error) {
            console.error('Failed to initialize Python parser:', error.message);
        }

        try {
            console.log('Initializing JavaScript parser...');
            const jsLang = this.loadLanguage('tree-sitter-javascript');
            if (jsLang) {
                const javascriptParser = new Parser();
                javascriptParser.setLanguage(jsLang);
                this.parsers.set('javascript', javascriptParser);
                this.parsers.set('js', javascriptParser);
                console.log('JavaScript parser initialized successfully');
            }
        } catch (error) {
            console.error('Failed to initialize JavaScript parser:', error.message);
        }

        try {
            console.log('Initializing TypeScript parser...');
            const tsLang = this.loadLanguage('tree-sitter-typescript', 'typescript');
            if (tsLang) {
                const typescriptParser = new Parser();
                typescriptParser.setLanguage(tsLang);
                this.parsers.set('typescript', typescriptParser);
                this.parsers.set('ts', typescriptParser);
                console.log('TypeScript parser initialized successfully');
            }
        } catch (error) {
            console.error('Failed to initialize TypeScript parser:', error.message);
        }

        try {
            console.log('Initializing TSX parser...');
            const tsxLang = this.loadLanguage('tree-sitter-typescript', 'tsx');
            if (tsxLang) {
                const tsxParser = new Parser();
                tsxParser.setLanguage(tsxLang);
                this.parsers.set('tsx', tsxParser);
                console.log('TSX parser initialized successfully');
            }
        } catch (error) {
            console.error('Failed to initialize TSX parser:', error.message);
        }

        try {
            console.log('Initializing Java parser...');
            const javaLang = this.loadLanguage('tree-sitter-java');
            if (javaLang) {
                const javaParser = new Parser();
                javaParser.setLanguage(javaLang);
                this.parsers.set('java', javaParser);
                console.log('Java parser initialized successfully');
            }
        } catch (error) {
            console.error('Failed to initialize Java parser:', error.message);
        }

        try {
            console.log('Initializing C parser...');
            const cLang = this.loadLanguage('tree-sitter-c');
            if (cLang) {
                const cParser = new Parser();
                cParser.setLanguage(cLang);
                this.parsers.set('c', cParser);
                console.log('C parser initialized successfully');
            }
        } catch (error) {
            console.error('Failed to initialize C parser:', error.message);
        }

        try {
            console.log('Initializing C++ parser...');
            const cppLang = this.loadLanguage('tree-sitter-cpp');
            if (cppLang) {
                const cppParser = new Parser();
                cppParser.setLanguage(cppLang);
                this.parsers.set('cpp', cppParser);
                this.parsers.set('c++', cppParser);
                console.log('C++ parser initialized successfully');
            }
        } catch (error) {
            console.error('Failed to initialize C++ parser:', error.message);
        }

        try {
            console.log('Initializing C# parser...');
            const csharpLang = this.loadLanguage('tree-sitter-c-sharp');
            if (csharpLang) {
                const csharpParser = new Parser();
                csharpParser.setLanguage(csharpLang);
                this.parsers.set('csharp', csharpParser);
                this.parsers.set('cs', csharpParser);
                console.log('C# parser initialized successfully');
            }
        } catch (error) {
            console.error('Failed to initialize C# parser:', error.message);
        }

        console.log('Parser initialization complete. Total parsers:', this.parsers.size);
    }

    getSupportedLanguages() {
        return ['python', 'javascript', 'typescript', 'tsx', 'java', 'c', 'cpp', 'csharp'];
    }

    async parseCode(code, language, filePath = '', returnRawNode = false) {
        const parser = this.parsers.get(language.toLowerCase());
        
        if (!parser) {
            throw new Error(`Unsupported language: ${language}`);
        }

        const tree = parser.parse(code);
        const rootNode = tree.rootNode;

        if (returnRawNode) {
            return rootNode;
        }

        const collectedData = this.collectASTData(rootNode, code, language, filePath);

        return {
            language,
            filePath,
            tree: this.serializeNode(rootNode),
            timestamp: new Date().toISOString(),
            metrics: {
                linesOfCode: collectedData.linesOfCode,
                totalNodes: collectedData.nodeCount,
                functionCount: collectedData.functionCount,
                classCount: collectedData.classCount,
                avgFunctionLength: collectedData.linesOfCode / Math.max(collectedData.functionCount, 1),
                cyclomaticComplexity: collectedData.cyclomaticComplexity,
                maxNestingDepth: collectedData.maxNestingDepth,
                semanticDiversity: Array.from(collectedData.semanticDiversity),
                contextWindowUtilization: this.estimateContextWindowUtilization(collectedData.linesOfCode), // Use collected linesOfCode
                functionCalls: collectedData.functionCallCount,
                classInheritance: collectedData.classInheritanceCount,
                attributeAccess: collectedData.attributeAccessCount,
                moduleImports: collectedData.moduleImportCount
            },
            dependencyGraph: {
                Nodes: collectedData.graphNodes,
                Edges: collectedData.graphEdges,
                Timestamp: new Date().toISOString()
            },
            entryPoints: collectedData.entryPoints,
            hierarchicalStructure: {
                filePath,
                language,
                modules: [], // Not directly collected in this pass, can be derived
                classes: collectedData.hierarchicalClasses,
                functions: collectedData.hierarchicalFunctions,
                timestamp: new Date().toISOString()
            },
            crossModuleReferences: collectedData.crossModuleReferences
        };
    }

    collectASTData(rootNode, code, language, filePath) {
        const linesOfCode = code.split('\n').length;
        let nodeCount = 0;
        let functionCount = 0;
        let classCount = 0;
        let cyclomaticComplexity = 1; // Base complexity
        let maxNestingDepth = 0;
        const semanticDiversity = new Set();
        let functionCallCount = 0;
        let classInheritanceCount = 0;
        let attributeAccessCount = 0;
        let moduleImportCount = 0;

        const graphNodes = [];
        const graphEdges = [];
        const processedGraphNodeIds = new Set();
        const entryPoints = [];
        const hierarchicalClasses = [];
        const hierarchicalFunctions = [];
        const crossModuleReferences = [];
        const missingDocumentation = []; // Added for documentation issues

        const addGraphNode = (id, type, lang = language, properties = {}) => {
            if (!processedGraphNodeIds.has(id)) {
                graphNodes.push({ Id: id, Type: type, Language: lang, Properties: properties });
                processedGraphNodeIds.add(id);
            }
        };

        const addGraphEdge = (source, target, type, subtype = "DependsOn", targetLanguage = language) => {
            graphEdges.push({ Source: source, Target: target, Type: type, Subtype: subtype, TargetLanguage: targetLanguage });
        };


        const queue = [{ node: rootNode, currentDepth: 0, parentNodeId: filePath }];
        
        while (queue.length > 0) {
            const { node, currentDepth, parentNodeId } = queue.shift();

            if (!node) continue;
            nodeCount++;

            // --- Documentation Analysis ---
            if (this.isDocumentableNode(node, language)) {
                if (!this.hasDocumentation(node, language)) {
                    missingDocumentation.push(
                        `Missing documentation for ${node.type} '${this.extractElementName(node, language)}' at line ${node.startPosition.row + 1}`
                    );
                }
            }


            // --- Metrics Collection ---
            const decisionTypes = [
                'if_statement', 'else_clause', 'elif_clause', 'while_statement',
                'for_statement', 'for_in_statement', 'switch_statement', 'case',
                'catch_clause', 'conditional_expression', 'binary_expression'
            ];
            if (decisionTypes.includes(node.type)) {
                cyclomaticComplexity++;
            }
            if (node.type === 'binary_expression' && (node.text.includes('&&') || node.text.includes('||'))) {
                cyclomaticComplexity += (node.text.match(/&&|&|\|\|/g) || []).length;
            }

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
                semanticDiversity.add(node.type);
            }

            const callTypes = this.getFunctionCallTypes(language);
            if (callTypes.includes(node.type)) {
                functionCallCount++;
            }

            const inheritanceTypes = this.getInheritanceTypes(language);
            if (inheritanceTypes.includes(node.type)) {
                classInheritanceCount++;
            }

            const accessTypes = this.getAttributeAccessTypes(language);
            if (accessTypes.includes(node.type)) {
                attributeAccessCount++;
            }

            const importTypes = this.getImportTypes(language);
            if (importTypes.includes(node.type)) {
                moduleImportCount++;
            }

            // --- Dependency Graph & Hierarchical Structure & Entry Points Collection ---
            let currentElementId = null;
            const structureTypes = this.getStructureTypes(language);
            const matchingStructType = structureTypes.find(st => st.type === node.type);

            if (matchingStructType) {
                const elementName = this.extractElementName(node, language);
                const elementType = matchingStructType.category;
                
                currentElementId = `${filePath}::${elementName}`;
                
                const nodeProperties = {};
                if (['csharp', 'java'].includes(language)) {
                    const annotations = this.extractAnnotationsAttributes(node, language);
                    if (annotations.length > 0) {
                        nodeProperties.Annotations = annotations;
                    }
                } else if (['python', 'javascript', 'typescript'].includes(language)) {
                    const decorators = this.extractDecorators(node, language);
                    if (decorators.length > 0) {
                        nodeProperties.Decorators = decorators;
                    }
                }

                addGraphNode(currentElementId, elementType, language, nodeProperties); // Add nodeProperties
                if (parentNodeId !== filePath) { // If not top-level, add contains edge
                     addGraphEdge(parentNodeId, currentElementId, "DependsOn", "CONTAINS");
                }

                const elementMetrics = {
                    lines: node.endPosition.row - node.startPosition.row + 1,
                    complexity: 0, // Will be aggregated later
                    nestingDepth: currentDepth
                };

                if (elementType === 'classes') {
                    hierarchicalClasses.push({
                        name: elementName,
                        type: node.type,
                        line: node.startPosition.row + 1,
                        parent: parentNodeId, // Simplified parent tracking
                        metrics: elementMetrics
                    });
                     // Only count actual classes, not just related nodes
                    if (['class_definition', 'class_declaration', 'class_expression', 'class_specifier'].includes(node.type)) {
                        classCount++;
                    }
                } else if (elementType === 'functions') {
                    hierarchicalFunctions.push({
                        name: elementName,
                        type: node.type,
                        line: node.startPosition.row + 1,
                        parent: parentNodeId, // Simplified parent tracking
                        metrics: elementMetrics
                    });
                     // Only count actual functions
                    if (['function_definition', 'function_declaration', 'arrow_function', 'method_definition', 'method_declaration', 'constructor_declaration'].includes(node.type)) {
                        functionCount++;
                    }
                }

                const entryPointPatterns = this.getEntryPointPatterns(language);
                for (const pattern of entryPointPatterns) {
                    if (this.matchesEntryPointPattern(node, pattern, language)) {
                        entryPoints.push({
                            type: pattern.type,
                            name: elementName,
                            filePath,
                            line: node.startPosition.row + 1,
                            signature: node.text.trim().substring(0, 100)
                        });
                    }
                }
            }
            // --- C/C++ Specific Constructs ---
            else if (['c', 'cpp'].includes(language)) {
                if (['preproc_define', 'preproc_def'].includes(node.type)) {
                    const macroNameNode = this.getChildByFieldName(node, 'name') || node.child(1);
                    if (macroNameNode) {
                        const macroId = `${filePath}::MACRO::${macroNameNode.text}`;
                        addGraphNode(macroId, 'MacroDefinition', language);
                        addGraphEdge(filePath, macroId, "DependsOn", "DEFINES_MACRO");
                    }
                } else if (node.type === 'struct_specifier' || node.type === 'union_specifier') {
                    const structNameNode = this.getChildByFieldName(node, 'name');
                    if (structNameNode) {
                        const structId = `${filePath}::${node.type.toUpperCase()}::${structNameNode.text}`;
                        addGraphNode(structId, node.type === 'struct_specifier' ? 'StructDefinition' : 'UnionDefinition', language);
                        addGraphEdge(filePath, structId, "DependsOn", "DEFINES_TYPE");
                    }
                } else if (language === 'cpp' && node.type === 'template_declaration') {
                    // Find the declaration being templated (class, function, etc.)
                    let declarationNode = node.child(1); 
                    // Robustness: look for specific declaration types if child(1) is not it (e.g. comment in between)
                    if (!declarationNode || !['class_specifier', 'function_definition'].includes(declarationNode.type)) {
                        declarationNode = node.children.find(c => ['class_specifier', 'function_definition', 'struct_specifier'].includes(c.type));
                    }

                    if (declarationNode) {
                         const templateNameNode = this.getChildByFieldName(declarationNode, 'name');
                         if (templateNameNode) {
                            const templateId = `${filePath}::TEMPLATE::${templateNameNode.text}`;
                            addGraphNode(templateId, 'TemplateDefinition', language);
                            addGraphEdge(filePath, templateId, "DependsOn", "DEFINES_TEMPLATE");
                         }
                    }
                }
            }
            // End of C/C++ Specific Constructs
            else if (importTypes.includes(node.type)) {
                 const importedModule = this.extractDependencyName(node, language);
                 if (importedModule && parentNodeId) {
                     addGraphNode(importedModule, 'Module', language);
                     addGraphEdge(parentNodeId, importedModule, "DependsOn", "IMPORTS");
                 }
            } else if (['python', 'javascript', 'typescript'].includes(language) && this.isDynamicImport(node, language)) {
                // Handle dynamic imports
                const dynamicImportTarget = this.extractDynamicImportTarget(node, language);
                if (dynamicImportTarget && parentNodeId) {
                    addGraphNode(dynamicImportTarget, 'DynamicModule', language);
                    addGraphEdge(parentNodeId, dynamicImportTarget, "DependsOn", "DYNAMIC_IMPORTS");
                }
            }


            // Identify function/method calls
            if (callTypes.includes(node.type)) {
                // This is a basic approach; a more sophisticated one would resolve the call target
                const calleeNode = node.child(0); // Often the first child is the callee
                if (calleeNode && (currentElementId || parentNodeId)) { // Use currentElementId from the nearest scope
                    const calleeName = calleeNode.text;
                    // Heuristic: if it's not a local definition, it might be an external call
                    // For now, let's just add it as a potential dependency
                    addGraphNode(calleeName, 'CallTarget', language);
                    addGraphEdge(currentElementId || parentNodeId, calleeName, "DependsOn", "CALLS");
                }
            }


            // --- Cross-Module References ---
            // This part needs more context (e.g., global symbol table) to be truly accurate.
            // For now, re-use the logic from extractCrossModuleReferences, assuming currentElementId or filePath as source
            const currentCrossModuleRefs = [];
            this.findCrossModuleReferences(node, language, currentCrossModuleRefs, [currentElementId || filePath]);
            currentCrossModuleRefs.forEach(ref => crossModuleReferences.push(ref));


            // Recursively add children to the queue
            maxNestingDepth = Math.max(maxNestingDepth, currentDepth);
            const nextDepth = this.getNestingTypes(language).includes(node.type) ? currentDepth + 1 : currentDepth;

            for (const child of node.children) {
                queue.push({ node: child, currentDepth: nextDepth, parentNodeId: currentElementId || parentNodeId });
            }
        }

        return {
            linesOfCode,
            nodeCount,
            functionCount,
            classCount,
            cyclomaticComplexity,
            maxNestingDepth,
            semanticDiversity,
            functionCallCount,
            classInheritanceCount,
            attributeAccessCount,
            moduleImportCount,
            graphNodes,
            graphEdges,
            entryPoints,
            hierarchicalClasses,
            hierarchicalFunctions,
            crossModuleReferences,
            missingDocumentation // Return missing documentation
        };
    }

    // Helper to determine if a node type typically requires documentation
    isDocumentableNode(node, language) {
        const documentableTypes = {
            python: ['class_definition', 'function_definition'],
            javascript: ['class_declaration', 'function_declaration', 'method_definition'],
            typescript: ['class_declaration', 'function_declaration', 'method_definition', 'interface_declaration'],
            java: ['class_declaration', 'method_declaration', 'constructor_declaration'],
            csharp: ['class_declaration', 'method_declaration', 'constructor_declaration', 'interface_declaration'],
            c: ['function_definition', 'declaration'], // For structs/typedefs etc.
            cpp: ['class_declaration', 'function_definition', 'method_definition']
        };
        return (documentableTypes[language] || []).includes(node.type);
    }

    // Heuristic to check for the presence of documentation comments
    hasDocumentation(node, language) {
        // This is a simplified check. A more robust solution would parse the comment content.
        const prevSibling = node.previousSibling;
        if (prevSibling && prevSibling.type === 'comment') {
            const commentText = prevSibling.text.toLowerCase();
            if (language === 'python') {
                return commentText.includes('"""') || commentText.includes("'''"); // Python docstrings
            } else if (['javascript', 'typescript'].includes(language)) {
                return commentText.startsWith('/**') || commentText.startsWith('//'); // JSDoc, single line comments
            } else if (language === 'java') {
                return commentText.startsWith('/**') || commentText.startsWith('//'); // Javadoc, single line comments
            } else if (language === 'csharp') {
                return commentText.startsWith('///') || commentText.startsWith('/*'); // XML comments, multi-line
            } else if (['c', 'cpp'].includes(language)) {
                return commentText.startsWith('/*') || commentText.startsWith('//'); // Multi-line or single-line
            }
        }
        // Check for specific comment fields for certain languages
        // Example: Python docstring is often the first statement in function/class body
        if (language === 'python' && ['class_definition', 'function_definition'].includes(node.type)) {
            const bodyNode = this.getChildByFieldName(node, 'body');
            if (bodyNode && bodyNode.children.length > 0 && bodyNode.children[0].type === 'expression_statement') {
                const expression = bodyNode.children[0].child(0);
                if (expression && expression.type === 'string' && (expression.text.startsWith('"""') || expression.text.startsWith("'''"))) {
                    return true;
                }
            }
        }
        return false;
    }

    // Helper method to get a child node by field name
    getChildByFieldName(node, fieldName) {
        if (!node) {
            return null;
        }
        if (typeof node.childForFieldName === 'function') {
             const child = node.childForFieldName(fieldName);
             if (child) {
                 return child;
             }
        }
        
        // Fallback for languages where field names might not be consistently used
        // or for more generic node types. This part might need to be language-specific.
        // For example, in many languages, the name of a function/class is the first identifier child.
        if (fieldName === 'name') {
            for (let i = 0; i < node.childCount; i++) {
                const c = node.child(i);
                if (c.type === 'identifier' || c.type === 'shorthand_property_identifier' || c.type === 'type_identifier') {
                    return c;
                }
            }
        }
        return null;
    }

    // Helper method to extract element name (e.g., function name, class name)
    extractElementName(node, language) {
        let nameNode;
        switch (language) {
            case 'python':
                nameNode = this.getChildByFieldName(node, 'name');
                if (!nameNode && node.type === 'decorated_definition') {
                    const decorated = this.getChildByFieldName(node, 'definition');
                    if (decorated) {
                        nameNode = this.getChildByFieldName(decorated, 'name');
                    }
                }
                break;
            case 'javascript':
            case 'typescript':
            case 'tsx':
                nameNode = this.getChildByFieldName(node, 'name') || this.getChildByFieldName(node, 'property_name');
                if (!nameNode && node.type === 'lexical_declaration') {
                    const declaration = node.child(1);
                    if (declaration) {
                        nameNode = this.getChildByFieldName(declaration, 'name');
                    }
                }
                break;
            case 'java':
            case 'csharp':
            case 'c':
            case 'cpp':
                nameNode = this.getChildByFieldName(node, 'name');
                break;
            default:
                nameNode = node.child(0);
                break;
        }
        return nameNode ? nameNode.text : 'Unknown';
    }

    // Helper to get language-specific function call types
    getFunctionCallTypes(language) {
        switch (language) {
            case 'python':
                return ['call', 'call_expression', 'function_call'];
            case 'javascript':
            case 'typescript':
            case 'tsx':
                return ['call_expression', 'new_expression'];
            case 'java':
            case 'csharp':
                return ['method_invocation', 'object_creation_expression'];
            case 'c':
            case 'cpp':
                return ['call_expression'];
            default:
                return [];
        }
    }

    // Helper to get language-specific inheritance types
    getInheritanceTypes(language) {
        switch (language) {
            case 'python':
                return ['class_definition'];
            case 'javascript':
            case 'typescript':
            case 'tsx':
                return ['class_declaration', 'class_expression'];
            case 'java':
                return ['class_declaration', 'interface_declaration'];
            case 'csharp':
                return ['class_declaration', 'interface_declaration', 'struct_declaration'];
            case 'cpp':
                return ['class_specifier'];
            default:
                return [];
        }
    }

    // Helper to get language-specific attribute access types (e.g., obj.prop)
    getAttributeAccessTypes(language) {
        switch (language) {
            case 'python':
                return ['attribute'];
            case 'javascript':
            case 'typescript':
            case 'tsx':
                return ['member_expression', 'property_identifier'];
            case 'java':
            case 'csharp':
                return ['field_access', 'member_access'];
            case 'c':
            case 'cpp':
                return ['field_expression', 'pointer_field_expression'];
            default:
                return [];
        }
    }

    // Helper to get language-specific import types
    getImportTypes(language) {
        switch (language) {
            case 'python':
                return ['import_statement', 'import_from_statement'];
            case 'javascript':
            case 'typescript':
            case 'tsx':
                return ['import_statement', 'import_clause', 'export_statement'];
            case 'java':
                return ['import_declaration'];
            case 'csharp':
                return ['using_directive'];
            case 'c':
            case 'cpp':
                return ['preproc_include'];
            default:
                return [];
        }
    }

    // Helper to get language-specific structure types for hierarchical view and graph nodes
    getStructureTypes(language) {
        switch (language) {
            case 'python':
                return [
                    { type: 'class_definition', category: 'classes' },
                    { type: 'function_definition', category: 'functions' },
                    { type: 'decorated_definition', category: 'functions' }
                ];
            case 'javascript':
            case 'typescript':
            case 'tsx':
                return [
                    { type: 'class_declaration', category: 'classes' },
                    { type: 'class_expression', category: 'classes' },
                    { type: 'function_declaration', category: 'functions' },
                    { type: 'method_definition', category: 'functions' },
                    { type: 'arrow_function', category: 'functions' },
                    { type: 'function_expression', category: 'functions' }
                ];
            case 'java':
                return [
                    { type: 'class_declaration', category: 'classes' },
                    { type: 'interface_declaration', category: 'classes' },
                    { type: 'enum_declaration', category: 'classes' },
                    { type: 'method_declaration', category: 'functions' },
                    { type: 'constructor_declaration', category: 'functions' }
                ];
            case 'csharp':
                return [
                    { type: 'class_declaration', category: 'classes' },
                    { type: 'interface_declaration', category: 'classes' },
                    { type: 'struct_declaration', category: 'classes' },
                    { type: 'enum_declaration', category: 'classes' },
                    { type: 'method_declaration', category: 'functions' },
                    { type: 'constructor_declaration', category: 'functions' },
                    { type: 'accessor_declaration', category: 'functions' }
                ];
            case 'c':
                return [
                    { type: 'function_definition', category: 'functions' },
                    { type: 'struct_specifier', category: 'classes' },
                    { type: 'union_specifier', category: 'classes' }
                ];
            case 'cpp':
                return [
                    { type: 'class_declaration', category: 'classes' },
                    { type: 'struct_declaration', category: 'classes' },
                    { type: 'function_definition', category: 'functions' },
                    { type: 'method_definition', category: 'functions' }
                ];
            default:
                return [];
        }
    }

    // Helper to get language-specific entry point patterns
    getEntryPointPatterns(language) {
        switch (language) {
            case 'python':
                return [
                    { type: 'main_function', pattern: (node) => this.extractElementName(node, language) === 'main' },
                    { type: 'flask_route', pattern: (node) => node.text.includes('@app.route') },
                    { type: 'django_view', pattern: (node) => node.text.includes('def') && node.parent && node.parent.type === 'call' && node.parent.text.includes('path(') }
                ];
            case 'javascript':
            case 'typescript':
            case 'tsx':
                return [
                    { type: 'export_default', pattern: (node) => node.parent && node.parent.type === 'export_default_declaration' },
                    { type: 'react_component', pattern: (node) => (node.type === 'function_declaration' || node.type === 'arrow_function') && /^[A-Z]/.test(this.extractElementName(node, language)) && node.text.includes('return') && (node.text.includes('React.') || node.text.includes('JSX')) },
                    { type: 'node_entry', pattern: (node) => node.text.includes('module.exports') || node.text.includes('exports.') }
                ];
            case 'java':
                return [
                    { type: 'main_method', pattern: (node) => this.extractElementName(node, language) === 'main' && node.text.includes('public static void main') }
                ];
            case 'csharp':
                return [
                    { type: 'main_method', pattern: (node) => this.extractElementName(node, language) === 'Main' && node.text.includes('public static void Main') }
                ];
            case 'c':
            case 'cpp':
                return [
                    { type: 'main_function', pattern: (node) => this.extractElementName(node, language) === 'main' }
                ];
            default:
                return [];
        }
    }

    // Helper to match entry point patterns
    matchesEntryPointPattern(node, pattern, language) {
        return pattern.pattern(node, language);
    }

    // Helper to extract dependency name from import/include nodes
    extractDependencyName(node, language) {
        switch (language) {
            case 'python':
                if (node.type === 'import_statement') {
                    const importedModule = this.getChildByFieldName(node, 'name') || node.child(1);
                    return importedModule ? importedModule.text : null;
                } else if (node.type === 'import_from_statement') {
                    const importedModule = this.getChildByFieldName(node, 'module_name');
                    return importedModule ? importedModule.text : null;
                }
                break;
            case 'javascript':
            case 'typescript':
            case 'tsx':
                const sourceNode = node.childForFieldName('source');
                if (sourceNode) {
                    return sourceNode.text.replace(/['"]/g, '');
                }
                break;
            case 'java':
                const importPath = node.text.replace('import ', '').replace(';', '').trim();
                return importPath.substring(importPath.lastIndexOf('.') + 1);
            case 'csharp':
                return node.text.replace('using ', '').replace(';', '').trim();
            case 'c':
            case 'cpp':
                const pathNode = node.child(node.childCount - 1);
                if (pathNode) {
                    return pathNode.text.replace(/[<>"']/g, '');
                }
                break;
        }
        return null;
    }

    // Placeholder for cross-module references (requires more sophisticated analysis)
    findCrossModuleReferences(node, language, crossModuleReferences, currentScope) {
        // This is a placeholder. Real cross-module reference detection requires
        // symbol resolution across multiple files.
        // For now, we can identify potential external references.
        const externalReferenceTypes = ['identifier', 'qualified_identifier']; // Common types for references
        if (externalReferenceTypes.includes(node.type)) {
            const identifier = node.text;
            // Very basic heuristic: if it's not a known keyword and not defined in current scope
            // (which is hard to check without a symbol table), it might be external.
            if (!['const', 'let', 'var', 'function', 'class', 'import', 'export', 'public', 'private'].includes(identifier) &&
                !this.isLocalDefinition(identifier, node, language)) {
                // Add to crossModuleReferences if it looks like an external call or reference
                // This part would need to be much smarter.
                // For now, avoid adding too much noise.
            }
        }
        // Recursively check children
        for (const child of node.children) {
            this.findCrossModuleReferences(child, language, crossModuleReferences, currentScope);
        }
    }

    // Placeholder for estimating context window utilization (already exists, but including for completeness)
    estimateContextWindowUtilization(linesOfCode) {
        // Rough estimation: ~4 characters per token
        const tokenEstimate = Math.ceil(linesOfCode * 4); // Assuming avg 40 chars per line
        const typicalContextWindow = 8192; // Typical context window size
        return {
            estimatedTokens: tokenEstimate,
            utilizationPercentage: Math.round((tokenEstimate / typicalContextWindow) * 100),
            requiresChunking: tokenEstimate > typicalContextWindow * 0.8
        };
    }

    // Heuristic for local definition (highly simplified)
    isLocalDefinition(name, node, language) {
        // This would require symbol table lookups.
        // For now, a very basic check: does the name directly appear as a child of a local scope defining node?
        // This is extremely rudimentary and needs a full symbol resolver.
        let isLocal = false;
        let currentNode = node;
        while(currentNode) {
            if (['class_definition', 'class_declaration', 'function_definition', 'function_declaration', 'method_definition'].includes(currentNode.type)) {
                const nameNode = this.getChildByFieldName(currentNode, 'name');
                if (nameNode && nameNode.text === name) {
                    isLocal = true;
                    break;
                }
                // Also check parameters
                const parametersNode = this.getChildByFieldName(currentNode, 'parameters');
                if (parametersNode) {
                    for (const param of parametersNode.children) {
                        if (param.type === 'identifier' && param.text === name) {
                            isLocal = true;
                            break;
                        }
                    }
                }
            }
            // Add checks for local variable declarations
            // This needs to be much more robust with full scope analysis.
            currentNode = currentNode.parent;
        }
        return isLocal;
    }

    // Placeholder for inferring target language. This would need to be much smarter.
    inferTargetLanguage(target) {
        // Very basic heuristic for demo/testing purposes
        if (target.includes("PythonService") || target.includes(".py")) return "python";
        if (target.includes("JavaProcessor") || target.includes(".java")) return "java";
        if (target.includes("CSharpComponent") || target.includes(".cs")) return "csharp";
        if (target.includes("JavaScriptService") || target.includes(".js")) return "javascript";
        if (target.includes("TypeScriptService") || target.includes(".ts")) return "typescript";
        if (target.includes("CModule") || target.includes(".c")) return "c";
        if (target.includes("CppModule") || target.includes(".cpp")) return "cpp";
        return "unknown";
    }

    extractAnnotationsAttributes(node, language) {
        const attributes = [];
        if (language === 'csharp') {
            // C# attributes are typically children of 'attribute_list' or 'attribute_target' nodes
            // attached to class/method declarations.
            // Simplified: look for identifier preceded by '[' and followed by ']'
            const cursor = node.walk();
            if (cursor.gotoFirstChild()) {
                do {
                    if (cursor.currentNode.type === 'attribute_list') {
                        const attributeCursor = cursor.currentNode.walk();
                        if (attributeCursor.gotoFirstChild()) { // Go to '['
                            if (attributeCursor.gotoNextSibling()) { // Go to attribute_target or attribute
                                do {
                                    if (attributeCursor.currentNode.type === 'attribute') {
                                        const nameNode = this.getChildByFieldName(attributeCursor.currentNode, 'name');
                                        if (nameNode) {
                                            attributes.push(nameNode.text);
                                        }
                                    }
                                } while (attributeCursor.gotoNextSibling());
                            }
                        }
                        // attributeCursor.reset(); - removed
                    }
                } while (cursor.gotoNextSibling());
            }
            // cursor.reset(); - removed
        } else if (language === 'java') {
            // Java annotations are typically children of 'modifiers' node or directly on declaration
            // Simplified: look for '@' followed by identifier
            const cursor = node.walk();
            if (cursor.gotoFirstChild()) {
                do {
                    if (cursor.currentNode.type === 'modifiers') {
                        const modifierCursor = cursor.currentNode.walk();
                        if (modifierCursor.gotoFirstChild()) {
                            do {
                                if (modifierCursor.currentNode.type.includes('annotation')) {
                                    const nameNode = modifierCursor.currentNode.child(1); // Usually identifier after '@'
                                    if (nameNode) {
                                        attributes.push(nameNode.text);
                                    }
                                }
                            } while (modifierCursor.gotoNextSibling());
                        }
                        // modifierCursor.reset(); - removed
                    }
                } while (cursor.gotoNextSibling());
            }
            // cursor.reset(); - removed
        }
        return attributes;
    }

    extractDecorators(node, language) {
        const decorators = [];
        if (language === 'python') {
            const decoratorNodes = node.children.filter(child => child.type === 'decorator');
            for (const decoratorNode of decoratorNodes) {
                const nameNode = decoratorNode.child(1); // Typically the name after '@'
                if (nameNode) {
                    decorators.push(nameNode.text);
                }
            }
        } else if (['javascript', 'typescript'].includes(language)) {
            // Decorators in TS/JS often appear as 'decorator' nodes preceding class/method definitions
            const decoratorNodes = node.children.filter(child => child.type === 'decorator');
            for (const decoratorNode of decoratorNodes) {
                const expression = decoratorNode.child(1); // Expression inside the decorator
                if (expression && expression.type === 'call_expression') {
                    const identifier = expression.child(0);
                    if (identifier) {
                        decorators.push(identifier.text);
                    }
                } else if (expression) {
                    decorators.push(expression.text);
                }
            }
        }
        return decorators;
    }

    isDynamicImport(node, language) {
        if (language === 'python') {
            // Look for call expressions to 'importlib.import_module' or similar patterns
            return node.type === 'call_expression' && node.text.includes('importlib.import_module');
        } else if (['javascript', 'typescript'].includes(language)) {
            // Look for dynamic import() syntax
            return node.type === 'call_expression' && node.text.startsWith('import(');
        }
        return false;
    }

    extractDynamicImportTarget(node, language) {
        if (language === 'python') {
            // Extract the module name from importlib.import_module('module_name')
            const argList = node.childForFieldName('arguments');
            if (argList && argList.child(0) && argList.child(0).type === 'string') {
                return argList.child(0).text.slice(1, -1); // Remove quotes
            }
        } else if (['javascript', 'typescript'].includes(language)) {
            // Extract module from import('module_name')
            const argList = node.childForFieldName('arguments');
            if (argList && argList.child(0) && argList.child(0).type === 'string') {
                return argList.child(0).text.slice(1, -1); // Remove quotes
            }
        }
        return null;
    }

    getNestingTypes(language) {
         // Blocks that increase nesting depth
        return [
            'if_statement', 'while_statement', 'for_statement',
            'for_in_statement', 'switch_statement', 'try_statement',
            'catch_clause', 'finally_clause', 'function_definition',
            'function_declaration', 'method_definition', 'class_definition',
            'class_declaration', 'block', 'compound_statement'
        ];
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

    // Placeholder for cross-module references (requires more sophisticated analysis)
    findCrossModuleReferences(node, language, crossModuleReferences, currentScope) {
        // This is a placeholder. Real cross-module reference detection requires
        // symbol resolution across multiple files.
        // For now, we can identify potential external references.
        const externalReferenceTypes = ['identifier', 'qualified_identifier']; // Common types for references
        if (externalReferenceTypes.includes(node.type)) {
            const identifier = node.text;
            // Very basic heuristic: if it's not a known keyword and not defined in current scope
            // (which is hard to check without a symbol table), it might be external.
            if (!['const', 'let', 'var', 'function', 'class', 'import', 'export', 'public', 'private'].includes(identifier) &&
                !this.isLocalDefinition(identifier, node, language)) {
                // Add to crossModuleReferences if it looks like an external call or reference
                // This part would need to be much smarter.
                // For now, avoid adding too much noise.
            }
        }
        // Recursively check children
        for (const child of node.children) {
            this.findCrossModuleReferences(child, language, crossModuleReferences, currentScope);
        }
    }

    // Placeholder for estimating context window utilization (already exists, but including for completeness)
    estimateContextWindowUtilization(linesOfCode) {
        // Rough estimation: ~4 characters per token
        const tokenEstimate = Math.ceil(linesOfCode * 4); // Assuming avg 40 chars per line
        const typicalContextWindow = 8192; // Typical context window size
        return {
            estimatedTokens: tokenEstimate,
            utilizationPercentage: Math.round((tokenEstimate / typicalContextWindow) * 100),
            requiresChunking: tokenEstimate > typicalContextWindow * 0.8
        };
    }

    // Heuristic for local definition (highly simplified)
    isLocalDefinition(name, node, language) {
        // This would require symbol table lookups.
        // For now, a very basic check: does the name directly appear as a child of a local scope defining node?
        // This is extremely rudimentary and needs a full symbol resolver.
        let isLocal = false;
        let currentNode = node;
        while(currentNode) {
            if (['class_definition', 'class_declaration', 'function_definition', 'function_declaration', 'method_definition'].includes(currentNode.type)) {
                const nameNode = this.getChildByFieldName(currentNode, 'name');
                if (nameNode && nameNode.text === name) {
                    isLocal = true;
                    break;
                }
                // Also check parameters
                const parametersNode = this.getChildByFieldName(currentNode, 'parameters');
                if (parametersNode) {
                    for (const param of parametersNode.children) {
                        if (param.type === 'identifier' && param.text === name) {
                            isLocal = true;
                            break;
                        }
                    }
                }
            }
            // Add checks for local variable declarations
            // This needs to be much more robust with full scope analysis.
            currentNode = currentNode.parent;
        }
        return isLocal;
    }

}

module.exports = ParserService;