// Simple JavaScript file for testing

function calculateProduct(a, b) {
    return a * b;
}

class Multiplier {
    constructor() {
        this.value = 1;
    }

    multiply(x) {
        this.value *= x;
        return this.value;
    }
}

// Main entry point
function main() {
    const multiplier = new Multiplier();
    const result = multiplier.multiply(5);
    console.log(`Result: ${result}`);
}

// Export for module usage
module.exports = {
    calculateProduct,
    Multiplier
};
