const express = require('express');
const router = express.Router();
const parserService = require('../services/parserService');

router.post('/parse', async (req, res) => {
    try {
        const { code, language, filePath } = req.body;
        
        if (!code || !language) {
            return res.status(400).json({
                error: 'Missing required parameters: code and language'
            });
        }

        const ast = await parserService.parseCode(code, language, filePath);
        res.json(ast);
    } catch (error) {
        console.error('Error parsing code:', error);
        res.status(500).json({ error: error.message });
    }
});

router.get('/supported-languages', (req, res) => {
    const languages = parserService.getSupportedLanguages();
    res.json({ languages });
});

module.exports = router;