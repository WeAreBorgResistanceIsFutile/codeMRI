const express = require('express');
const cors = require('cors');
const helmet = require('helmet');

const astRoutes = require('./routes/ast');
const healthRoutes = require('./routes/health');
const analysisRoutes = require('./routes/analysis');

const app = express();
const PORT = process.env.PORT || 3002;

// Middleware
app.use(helmet());
app.use(cors());
app.use(express.json({ limit: '50mb' }));
app.use(express.text({ type: 'text/plain', limit: '50mb' }));

// Routes
app.use('/api/ast', astRoutes);
app.use('/api/analysis', analysisRoutes);
app.use('/health', healthRoutes);

app.listen(PORT, () => {
    console.log(`AST Service running on port ${PORT}`);
});