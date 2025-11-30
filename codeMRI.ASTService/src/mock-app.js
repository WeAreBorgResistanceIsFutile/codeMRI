const express = require('express');
const cors = require('cors');
const helmet = require('helmet');

const app = express();
const PORT = process.env.PORT || 3000;

// Middleware
app.use(helmet());
app.use(cors());
app.use(express.json());

// Health check endpoint
app.get('/health', (req, res) => {
  res.json({ status: 'healthy', timestamp: new Date().toISOString() });
});

// Mock AST analysis endpoint
app.post('/ast/analyze', (req, res) => {
  const { language, code } = req.body;
  
  if (!language || !code) {
    return res.status(400).json({ 
      error: 'Missing required fields: language and code' 
    });
  }

  // Mock response
  res.json({
    success: true,
    language,
    ast: {
      type: 'mock_ast',
      message: 'AST service is running in mock mode',
      nodes: [],
      edges: []
    },
    metadata: {
      lines: code.split('\n').length,
      characters: code.length,
      timestamp: new Date().toISOString()
    }
  });
});

// Root endpoint
app.get('/', (req, res) => {
  res.json({ 
    service: 'codeMRI AST Service',
    status: 'running',
    mode: 'mock'
  });
});

// Start server
app.listen(PORT, '0.0.0.0', () => {
  console.log(`AST Service running on port ${PORT}`);
});