using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

/// <summary>
///     Strategy interface for building evaluation prompts.
///     Enables custom prompt generation without modifying the judge service.
/// </summary>
public interface IEvaluationPromptBuilder
{
    /// <summary>
    ///     Builds a prompt for evaluating a requirement against documentation.
    /// </summary>
    /// <param name="requirement">The requirement to evaluate</param>
    /// <param name="documentationStructure">The documentation structure to evaluate against</param>
    /// <returns>The evaluation prompt text</returns>
    Task<string> BuildPromptAsync(
        RubricRequirement requirement,
        WikiStructure documentationStructure);
}