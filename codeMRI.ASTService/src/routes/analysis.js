const express = require('express');
const analysisOrchestrator = require('../services/analysisOrchestrator');
const router = express.Router();

// Analyze entire repository
router.post('/repository', async (req, res) => {
    try {
        const { files, options = {} } = req.body;
        
        if (!files || !Array.isArray(files)) {
            return res.status(400).json({
                error: 'Invalid request: files array is required'
            });
        }

        // Validate file objects
        for (const file of files) {
            if (!file.content || !file.language || !file.filePath) {
                return res.status(400).json({
                    error: 'Each file must have content, language, and filePath properties'
                });
            }
        }

        const analysis = await analysisOrchestrator.analyzeRepository(files, options);
        
        res.json({
            success: true,
            analysisId: analysis.id,
            status: analysis.status,
            results: analysis.results
        });

    } catch (error) {
        console.error('Repository analysis error:', error);
        res.status(500).json({
            error: 'Analysis failed',
            details: error.message
        });
    }
});

// Get analysis status
router.get('/status/:analysisId', async (req, res) => {
    try {
        const { analysisId } = req.params;
        const analysis = analysisOrchestrator.getAnalysisStatus(analysisId);
        
        if (!analysis) {
            return res.status(404).json({
                error: 'Analysis not found'
            });
        }

        res.json({
            success: true,
            analysisId: analysis.id,
            status: analysis.status,
            startTime: analysis.startTime,
            endTime: analysis.endTime,
            error: analysis.error
        });

    } catch (error) {
        console.error('Status check error:', error);
        res.status(500).json({
            error: 'Status check failed',
            details: error.message
        });
    }
});

// Cancel analysis
router.delete('/cancel/:analysisId', async (req, res) => {
    try {
        const { analysisId } = req.params;
        const cancelled = analysisOrchestrator.cancelAnalysis(analysisId);
        
        if (!cancelled) {
            return res.status(404).json({
                error: 'Analysis not found'
            });
        }

        res.json({
            success: true,
            message: 'Analysis cancelled successfully'
        });

    } catch (error) {
        console.error('Cancel analysis error:', error);
        res.status(500).json({
            error: 'Cancel failed',
            details: error.message
        });
    }
});

// Get supported languages
router.get('/languages', (req, res) => {
    try {
        const parserService = require('../services/parserService');
        const languages = parserService.getSupportedLanguages();
        
        res.json({
            success: true,
            languages: languages.sort()
        });

    } catch (error) {
        console.error('Get languages error:', error);
        res.status(500).json({
            error: 'Failed to get supported languages',
            details: error.message
        });
    }
});

// Analyze single file with enhanced metrics
router.post('/file', async (req, res) => {
    try {
        const { content, language, filePath, options = {} } = req.body;
        
        if (!content || !language || !filePath) {
            return res.status(400).json({
                error: 'content, language, and filePath are required'
            });
        }

        const parserService = require('../services/parserService');
        const result = await parserService.parseCode(content, language, filePath);
        
        res.json({
            success: true,
            result
        });

    } catch (error) {
        console.error('File analysis error:', error);
        res.status(500).json({
            error: 'File analysis failed',
            details: error.message
        });
    }
});

// Get complexity thresholds
router.get('/thresholds', (req, res) => {
    try {
        const thresholds = analysisOrchestrator.complexityThresholds;
        
        res.json({
            success: true,
            thresholds
        });

    } catch (error) {
        console.error('Get thresholds error:', error);
        res.status(500).json({
            error: 'Failed to get thresholds',
            details: error.message
        });
    }
});

// Update complexity thresholds
router.put('/thresholds', (req, res) => {
    try {
        const { thresholds } = req.body;
        
        if (!thresholds || typeof thresholds !== 'object') {
            return res.status(400).json({
                error: 'Valid thresholds object is required'
            });
        }

        // Update thresholds with validation
        const validThresholds = {};
        const defaultThresholds = analysisOrchestrator.complexityThresholds;
        
        for (const [key, defaultValue] of Object.entries(defaultThresholds)) {
            if (thresholds[key] !== undefined && typeof thresholds[key] === 'number' && thresholds[key] > 0) {
                validThresholds[key] = thresholds[key];
            } else {
                validThresholds[key] = defaultValue;
            }
        }
        
        analysisOrchestrator.complexityThresholds = validThresholds;
        
        res.json({
            success: true,
            thresholds: validThresholds
        });

    } catch (error) {
        console.error('Update thresholds error:', error);
        res.status(500).json({
            error: 'Failed to update thresholds',
            details: error.message
        });
    }
});

module.exports = router;