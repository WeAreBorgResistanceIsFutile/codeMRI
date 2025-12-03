namespace codeMRI.Core.Models;

/// <summary>
///     Represents a hierarchical tree of modules in a codebase
/// </summary>
public class ModuleTree
{
    /// <summary>
    ///     Root node of the module tree
    /// </summary>
    public ModuleNode Root { get; set; } = new() { Id = "root", Name = "Repository" };

    /// <summary>
    ///     All nodes in the tree by ID
    /// </summary>
    public Dictionary<string, ModuleNode> Nodes { get; set; } = new();

    /// <summary>
    ///     Adds a node to the tree
    /// </summary>
    public void AddNode(ModuleNode node)
    {
        Nodes[node.Id] = node;
        if (node.Parent != null) node.Parent.Children.Add(node);
    }

    /// <summary>
    ///     Gets a node by ID
    /// </summary>
    public ModuleNode? GetNode(string id)
    {
        return Nodes.TryGetValue(id, out var node) ? node : null;
    }

    /// <summary>
    ///     Gets all leaf nodes in the tree
    /// </summary>
    public List<ModuleNode> GetAllLeaves()
    {
        var leaves = new List<ModuleNode>();
        CollectLeaves(Root, leaves);
        return leaves;
    }

    private void CollectLeaves(ModuleNode node, List<ModuleNode> leaves)
    {
        if (node.Children.Count == 0)
            leaves.Add(node);
        else
            foreach (var child in node.Children)
                CollectLeaves(child, leaves);
    }
}

/// <summary>
///     Represents a node in the module tree
/// </summary>
public class ModuleNode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Level { get; set; }
    public bool IsLeaf { get; set; }
    public double EstimatedTokens { get; set; }
    public int ComplexityScore { get; set; }
    public HashSet<string> Components { get; set; } = new();
    public ModuleNode? Parent { get; set; }
    public List<ModuleNode> Children { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
    public ModuleQualityMetrics QualityMetrics { get; set; } = new();

    /// <summary>
    ///     Gets the full path of the module in the hierarchy
    /// </summary>
    public string GetPath()
    {
        return Parent != null ? $"{Parent.GetPath()}/{Name}" : Name;
    }

    /// <summary>
    ///     Adds a child node to this module
    /// </summary>
    public void AddChild(ModuleNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    /// <summary>
    ///     Checks if this module is an ancestor of the given node
    /// </summary>
    public bool IsAncestorOf(ModuleNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current.Id == Id) return true;
            current = current.Parent;
        }

        return false;
    }

    /// <summary>
    ///     Gets all components in this module and its children
    /// </summary>
    public IEnumerable<string> GetAllComponents()
    {
        foreach (var component in Components)
            yield return component;

        foreach (var child in Children)
        foreach (var childComponent in child.GetAllComponents())
            yield return childComponent;
    }
}

/// <summary>
///     Represents semantic relationships between components
/// </summary>
public enum ComponentRelationshipType
{
    Dependency,
    Inheritance,
    Implementation,
    Composition,
    Aggregation,
    Association,
    Call
}

/// <summary>
///     Represents a relationship between two components
/// </summary>
public class ComponentRelationship
{
    public string FromComponent { get; set; } = string.Empty;
    public string ToComponent { get; set; } = string.Empty;
    public ComponentRelationshipType Type { get; set; }
    public double Strength { get; set; } = 1.0;
    public string? Description { get; set; }
}

public class ModuleQualityMetrics
{
    public double Cohesion { get; set; }
    public double Coupling { get; set; }
    public double Complexity { get; set; }
    public double Instability { get; set; }
    public double Abstractness { get; set; }
    public double DistanceFromMainSequence { get; set; }
    public double MaintainabilityIndex { get; set; }
}