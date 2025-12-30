# Ensemble Multi-Model Documentation Generation

## Overview

This document describes the integration of **ensemble multi-model generation** into the CodeMRI wiki generation pipeline to reduce output variance and improve documentation quality.

## Problem Statement

Single-model LLM generation exhibits **high variance** across runs, producing inconsistent documentation quality. Different models have different strengths and weaknesses, leading to unpredictable outputs.

## Solution: Ensemble Synthesis

The `IMultiModelOrchestrationService` addresses this by:

1. **Parallel Generation**: Runs the same prompt through multiple models simultaneously
2. **Agreement Calculation**: Measures consensus between model outputs
3. **Judge Synthesis**: Uses a specialized model to combine the best elements from all outputs
4. **Uncertainty Quantification**: Provides metrics on model disagreement

## Architecture

### Service Flow

```
WikiGenerationService
    ↓
IMultiModelOrchestrationService (if enabled)
    ↓
├─→ Model 1 (qwen3-coder:480b-cloud)
├─→ Model 2 (devstral-2:123b-cloud)
├─→ Model 3 (kimi-k2-thinking:cloud)
├─→ Model 4 (deepseek-v3.1:671b-cloud)
├─→ Model 5 (minimax-m2.1:cloud)
├─→ Model 6 (cogito-2.1:671b-cloud)
├─→ Model 7 (nemotron-3-nano:30b-cloud)
├─→ Model 8 (qwen3-next:80b-cloud)
└─→ Model 9 (gemma3:27b-cloud)
    ↓
Calculate Agreement Score (0.0-1.0)
    ↓
Judge Model Synthesis (ministral-3:14b-cloud)
    ↓
Synthesized Documentation + Metrics
```

### Fallback Behavior

If `IMultiModelOrchestrationService` is not available or ensemble is disabled:

- Falls back to single-model generation via `ILLMServiceFacade`
- Uses `IModelRoutingService` to select the best model for the task type

## Configuration

### Current Settings (appsettings.json)

```json
"ModelRouting": {
  "EnableEnsembleGeneration": true,
  "EnsembleModels": [
    "qwen3-coder:480b-cloud",
    "devstral-2:123b-cloud",
    "kimi-k2-thinking:cloud",
    "deepseek-v3.1:671b-cloud",
    "minimax-m2.1:cloud",
    "cogito-2.1:671b-cloud",
    "nemotron-3-nano:30b-cloud",
    "qwen3-next:80b-cloud",
    "gemma3:27b-cloud"
  ],
  "SynthesisJudgeModel": "ministral-3:14b-cloud",
  "MinimumAgreementThreshold": 2
}
```

### Key Parameters

| Parameter | Value | Description |
|-----------|-------|-------------|
| `EnableEnsembleGeneration` | `true` | Master switch for ensemble mode |
| `EnsembleModels` | 9 models | Models to run in parallel |
| `SynthesisJudgeModel` | `ministral-3:14b-cloud` | Model used to synthesize outputs |
| `MinimumAgreementThreshold` | `2` | Minimum agreement threshold (0-100) |

## Performance Characteristics

### Time Complexity

**Single Model**: 1 LLM call  
**Ensemble**: N + 1 LLM calls (N models + 1 judge)

With 9 ensemble models:

- **10x slower** than single model (9 parallel + 1 synthesis)
- However, parallel execution reduces wall-clock time significantly

### Quality Improvements

- ✅ **Reduced Variance**: Multiple models smooth out individual model quirks
- ✅ **Higher Accuracy**: Judge selects best elements from each output
- ✅ **Uncertainty Metrics**: Know when models disagree (low confidence)
- ✅ **Robustness**: Single model failures don't break generation

### Trade-offs

| Aspect | Ensemble | Single Model |
|--------|----------|--------------|
| **Quality** | ⭐⭐⭐⭐⭐ High | ⭐⭐⭐ Medium |
| **Consistency** | ⭐⭐⭐⭐⭐ Very High | ⭐⭐ Low |
| **Speed** | ⭐⭐ Slow (10x calls) | ⭐⭐⭐⭐⭐ Fast |
| **Cost** | ⭐⭐ High (10x tokens) | ⭐⭐⭐⭐⭐ Low |
| **Observability** | ⭐⭐⭐⭐⭐ Rich metrics | ⭐⭐ Basic |

## Monitoring & Metrics

### Log Output

When ensemble generation is active, you'll see:

```
[INFO] Using ensemble generation for page 'MyComponent' to reduce variance
[INFO] Ensemble generation completed for 'MyComponent': Agreement=0.85, Uncertainty=0.12, Models=9
```

### Key Metrics

1. **Agreement Score** (0.0-1.0)
   - Measures how similar the model outputs are
   - Higher = stronger consensus
   - Lower = models disagree significantly

2. **Uncertainty** (0.0-1.0)
   - Derived from agreement score
   - Higher = less confident in output
   - Lower = high confidence

3. **Participating Models** (count)
   - Number of models that successfully generated output
   - Should be close to total ensemble size

### Interpreting Metrics

| Agreement | Uncertainty | Interpretation | Action |
|-----------|-------------|----------------|--------|
| > 0.8 | < 0.2 | Strong consensus | High confidence ✅ |
| 0.5-0.8 | 0.2-0.5 | Moderate agreement | Review output |
| < 0.5 | > 0.5 | High disagreement | Manual review needed ⚠️ |

## Code Changes

### Modified Files

1. **`WikiGenerationService.cs`**
   - Added ensemble generation in `GeneratePageAsync()`
   - Added ensemble generation in `GenerateEnhancedPageAsync()`
   - Logs agreement/uncertainty metrics

### Integration Points

```csharp
// Check if orchestration service is available
if (_orchestrationService != null)
{
    // Use ensemble generation
    var ensembleResult = await _orchestrationService.GenerateWithEnsembleAsync(
        systemPrompt: "",
        userPrompt: prompt + "\n\nSOURCE FILES CONTENT:\n" + sourceFilesContent,
        history: new List<ChatMessage>(),
        cancellationToken: default);
    
    content = ensembleResult.SynthesizedContent;
    
    _logger.LogInformation(
        "Ensemble: Agreement={Agreement:F2}, Uncertainty={Uncertainty:F2}, Models={ModelCount}",
        ensembleResult.AgreementScore, 
        ensembleResult.Uncertainty, 
        ensembleResult.ParticipatingModels.Count);
}
else
{
    // Fallback to single model
    var llmResponse = await _llmFacade.ExecuteAsync(...);
    content = llmResponse.Content;
}
```

## Testing & Validation

### How to Test

1. **Enable Ensemble** (already done in appsettings.json)

   ```json
   "EnableEnsembleGeneration": true
   ```

2. **Run Documentation Generation**

   ```bash
   dotnet run --project codeMRI.Server
   ```

3. **Monitor Logs** for ensemble metrics

   ```
   grep "Ensemble generation completed" logs/app-*.log
   ```

4. **Compare Quality**
   - Generate same page with ensemble ON vs OFF
   - Measure consistency across multiple runs
   - Check agreement scores

### Expected Behavior

✅ **With Ensemble ON**:

- Logs show "Using ensemble generation for..."
- 9 models run in parallel
- Judge synthesizes outputs
- Agreement/Uncertainty metrics logged

✅ **With Ensemble OFF**:

- Logs show "Using single model..."
- Single model execution
- No synthesis step

## Optimization Opportunities

### 1. Selective Ensemble

Use ensemble only for critical pages:

```csharp
// Only use ensemble for high-complexity modules
if (_orchestrationService != null && module.ComplexityScore > 80)
{
    // Ensemble generation
}
else
{
    // Single model (faster)
}
```

### 2. Dynamic Model Selection

Reduce ensemble size based on agreement:

```csharp
// Start with 3 models, add more if agreement is low
var initialModels = 3;
var result = await GenerateWithNModels(initialModels);
if (result.AgreementScore < 0.6)
{
    // Add more models for better consensus
    result = await GenerateWithNModels(9);
}
```

### 3. Caching

Cache ensemble results to avoid re-generation:

```csharp
var cacheKey = $"ensemble:{pageTitle}:{contentHash}";
if (cache.TryGet(cacheKey, out var cachedResult))
{
    return cachedResult;
}
```

## Troubleshooting

### Issue: Ensemble Not Running

**Symptoms**: Logs show "Using single model" instead of "Using ensemble"

**Causes**:

1. `EnableEnsembleGeneration: false` in config
2. `EnsembleModels` list is empty
3. `IMultiModelOrchestrationService` not registered

**Solution**:

```bash
# Check configuration
cat codeMRI.Server/appsettings.json | grep -A 20 "ModelRouting"

# Verify service registration
grep "IMultiModelOrchestrationService" codeMRI.Infrastructure/WireUp.cs
```

### Issue: Low Agreement Scores

**Symptoms**: Agreement < 0.5 consistently

**Causes**:

1. Models are too diverse (different architectures)
2. Prompt is ambiguous
3. Source code is complex/unclear

**Solution**:

- Review prompt clarity
- Check source code quality
- Consider using more similar models

### Issue: Slow Performance

**Symptoms**: Documentation generation takes 10x longer

**Expected**: This is normal with 9 models + synthesis

**Mitigation**:

- Reduce ensemble size (use 3-5 models instead of 9)
- Use selective ensemble (only for critical pages)
- Enable parallel execution optimizations

## Future Enhancements

1. **Adaptive Ensemble Size**
   - Start small, grow if needed
   - Based on agreement scores

2. **Model Weighting**
   - Give more weight to higher-performing models
   - Track model accuracy over time

3. **Uncertainty-Based Retry**
   - Automatically retry high-uncertainty outputs
   - Use different prompt strategies

4. **Performance Benchmarking**
   - Track agreement scores over time
   - Correlate with documentation quality ratings

## References

- **Interface**: `codeMRI.Core/Interfaces/IMultiModelOrchestrationService.cs`
- **Implementation**: `codeMRI.Core/Services/MultiModelOrchestrationService.cs`
- **Integration**: `codeMRI.Core/Services/WikiGenerationService.cs`
- **Configuration**: `codeMRI.Server/appsettings.json`
- **Dependency Injection**: `codeMRI.Infrastructure/WireUp.cs`

## Summary

✅ **Ensemble generation is now active** for all wiki page generation  
✅ **9 models run in parallel** to reduce variance  
✅ **Judge model synthesizes** the best elements  
✅ **Metrics logged** for monitoring quality  
✅ **Fallback available** if ensemble is disabled  

**Expected Impact**:

- 📈 **Higher quality** documentation
- 📊 **More consistent** outputs across runs
- 🎯 **Better accuracy** in technical details
- ⏱️ **Slower generation** (10x more LLM calls)
- 💰 **Higher cost** (10x token usage)

The trade-off is **quality vs. speed** - ensemble generation prioritizes quality and consistency over performance.
