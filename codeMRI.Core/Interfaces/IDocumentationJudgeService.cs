using codeMRI.Core.Models;

namespace codeMRI.Core.Interfaces;

public interface IDocumentationJudgeService
{
    Task<RequirementAssessment> EvaluateRequirementAsync(
        RubricRequirement requirement,
        WikiStructure documentationStructure,
        CancellationToken cancellationToken = default,
        string? model = null);

    Task<List<RequirementAssessment>> EvaluateRequirementsAsync(
        List<RubricRequirement> requirements,
        WikiStructure documentationStructure,
        List<string> judgeModels,
        CancellationToken cancellationToken = default);
}
