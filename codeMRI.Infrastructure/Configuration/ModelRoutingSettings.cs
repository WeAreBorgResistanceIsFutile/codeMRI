namespace codeMRI.Infrastructure.Configuration;

/// <summary>
///     Configuration for multi-model routing and ensemble generation.
///     Defines which models to use for specialized tasks and ensemble synthesis.
/// </summary>
public class ModelRoutingSettings
{
    /// <summary>
    ///     Primary model for documentation generation.
    /// </summary>
    public string DocumentationModel { get; set; } = "llama3";

    /// <summary>
    ///     Primary model for chat/interaction.
    /// </summary>
    public string ChatModel { get; set; } = "llama3";

    /// <summary>
    ///     Models used as judges for quality assessment.
    /// </summary>
    public List<string> JudgeModels { get; set; } = new();

    /// <summary>
    ///     Enable specialized model routing based on task type.
    ///     When enabled, different models are used for code analysis vs natural language tasks.
    /// </summary>
    public bool EnableModelRouting { get; set; } = true;

    /// <summary>
    ///     Enable ensemble generation where multiple models generate content in parallel
    ///     and outputs are synthesized by a judge model.
    /// </summary>
    public bool EnableEnsembleGeneration { get; set; } = false;

    /// <summary>
    ///     Model optimized for code analysis and technical documentation.
    ///     Recommended: DeepSeek, Qwen, or other code-specialized models.
    /// </summary>
    public string CodeAnalysisModel { get; set; } = "deepseek-v3.1:671b-cloud";

    /// <summary>
    ///     Model optimized for natural language generation and readability.
    ///     Recommended: Llama, Mistral for clear, well-structured prose.
    /// </summary>
    public string NaturalLanguageModel { get; set; } = "mistral-large-3:675b-cloud";

    /// <summary>
    ///     Model used to synthesize outputs from multiple models in ensemble mode.
    ///     Should have strong reasoning capabilities for judging and merging content.
    /// </summary>
    public string SynthesisJudgeModel { get; set; } = "kimi-k2-thinking:cloud";

    /// <summary>
    ///     List of models to use for ensemble generation.
    ///     All models generate content in parallel, then synthesis judge combines outputs.
    /// </summary>
    public List<string> EnsembleModels { get; set; } = new();

    /// <summary>
    ///     Minimum number of models that must agree for high-confidence output.
    ///     Used to calculate uncertainty in ensemble mode.
    /// </summary>
    public int MinimumAgreementThreshold { get; set; } = 2;
}