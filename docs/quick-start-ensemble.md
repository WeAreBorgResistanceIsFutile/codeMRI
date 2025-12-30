# Quick Start: Ensemble Multi-Model Generation

## ✅ Status: ACTIVE

Ensemble generation is **currently enabled** and will be used for all wiki page generation.

## How It Works

```
Your Request → 9 Models Run in Parallel → Judge Synthesizes → High-Quality Output
```

**Models in Ensemble:**

1. qwen3-coder:480b-cloud
2. devstral-2:123b-cloud
3. kimi-k2-thinking:cloud
4. deepseek-v3.1:671b-cloud
5. minimax-m2.1:cloud
6. cogito-2.1:671b-cloud
7. nemotron-3-nano:30b-cloud
8. qwen3-next:80b-cloud
9. gemma3:27b-cloud

**Judge Model:** ministral-3:14b-cloud

## Monitoring

### Check if Ensemble is Running

```bash
# Watch logs in real-time
tail -f logs/app-*.log | grep -i ensemble

# You should see:
# [INFO] Using ensemble generation for page 'MyComponent' to reduce variance
# [INFO] Ensemble generation completed for 'MyComponent': Agreement=0.85, Uncertainty=0.12, Models=9
```

### View Metrics

```bash
# Extract all ensemble metrics from today's logs
grep "Ensemble generation completed" logs/app-$(date +%Y%m%d).log

# Example output:
# [12:34:56 INF] Ensemble generation completed for 'UserService': Agreement=0.87, Uncertainty=0.11, Models=9
# [12:35:23 INF] Ensemble generation completed for 'DataLayer': Agreement=0.92, Uncertainty=0.08, Models=9
```

### Interpret Metrics

| Agreement Score | Quality Indicator |
|-----------------|-------------------|
| **> 0.8** | 🟢 Excellent - Strong consensus |
| **0.6-0.8** | 🟡 Good - Moderate agreement |
| **< 0.6** | 🔴 Review - Models disagree |

| Uncertainty | Confidence Level |
|-------------|------------------|
| **< 0.2** | 🟢 High confidence |
| **0.2-0.5** | 🟡 Medium confidence |
| **> 0.5** | 🔴 Low confidence - manual review |

## Performance Impact

⏱️ **Expected Slowdown:** 10x slower than single model  
💰 **Token Usage:** 10x higher (9 models + 1 judge)  
📈 **Quality Gain:** Significantly reduced variance  

## Toggle Ensemble On/Off

### Disable Ensemble (Faster, Lower Quality)

Edit `codeMRI.Server/appsettings.json`:

```json
"ModelRouting": {
  "EnableEnsembleGeneration": false,  // ← Change to false
  ...
}
```

### Enable Ensemble (Slower, Higher Quality)

```json
"ModelRouting": {
  "EnableEnsembleGeneration": true,  // ← Change to true
  ...
}
```

**Restart required after config changes:**

```bash
# Stop the server (Ctrl+C)
# Restart
dotnet run --project codeMRI.Server
```

## Troubleshooting

### ❌ Not Seeing Ensemble Logs?

**Check 1:** Verify configuration

```bash
grep -A 5 "EnableEnsembleGeneration" codeMRI.Server/appsettings.json
# Should show: "EnableEnsembleGeneration": true
```

**Check 2:** Verify models are available

```bash
# List available models in Ollama
curl http://localhost:11434/api/tags | jq '.models[].name'
```

**Check 3:** Check logs for errors

```bash
grep -i "error\|exception" logs/app-*.log | tail -20
```

### ⚠️ Low Agreement Scores?

This is **expected** when:

- Code is complex or ambiguous
- Prompt is unclear
- Models have very different architectures

**Action:** Review the generated documentation manually for quality.

### 🐌 Too Slow?

**Option 1:** Reduce ensemble size (faster, still better than single model)

Edit `appsettings.json`:

```json
"EnsembleModels": [
  "qwen3-coder:480b-cloud",
  "devstral-2:123b-cloud",
  "deepseek-v3.1:671b-cloud"
  // Use only 3 models instead of 9
]
```

**Option 2:** Disable ensemble for faster generation

```json
"EnableEnsembleGeneration": false
```

## Testing the Integration

### Generate a Test Page

```bash
# Run the server
dotnet run --project codeMRI.Server

# In another terminal, trigger documentation generation
# (Use your existing workflow/API)
```

### Verify Ensemble is Working

```bash
# Check logs for ensemble activity
tail -f logs/app-*.log | grep "ensemble"

# Expected output:
# [INFO] Using ensemble generation for page 'TestPage' to reduce variance
# [INFO] Ensemble generation completed for 'TestPage': Agreement=0.85, Uncertainty=0.12, Models=9
```

### Compare Quality

1. **Generate with Ensemble ON** (current state)
2. **Generate same page with Ensemble OFF**
3. **Compare outputs** - ensemble should be more consistent

## Key Benefits

✅ **Reduced Variance** - More consistent outputs across runs  
✅ **Higher Quality** - Judge selects best elements from each model  
✅ **Uncertainty Metrics** - Know when to manually review  
✅ **Robustness** - Single model failures don't break generation  

## Next Steps

1. ✅ **Monitor logs** to see ensemble in action
2. ✅ **Track agreement scores** to gauge quality
3. ✅ **Compare outputs** with previous single-model generation
4. ✅ **Adjust ensemble size** if needed for performance

## Full Documentation

See `docs/architecture/ensemble-generation.md` for complete details on:

- Architecture and design
- Configuration options
- Performance optimization
- Advanced troubleshooting

---

**Status:** ✅ Integration complete and active  
**Date:** 2025-12-30  
**Version:** 1.0
