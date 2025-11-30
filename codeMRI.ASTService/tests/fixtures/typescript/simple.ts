// Simple TypeScript file for testing

interface ICalculator {
    value: number;
    add(x: number): number;
}

class Calculator implements ICalculator {
    value: number = 0;

    add(x: number): number {
        this.value += x;
        return this.value;
    }

    static create(): Calculator {
        return new Calculator();
    }
}

// Main entry point
function main(): void {
    const calc = Calculator.create();
    const result = calc.add(5);
    console.log(`Result: ${result}`);
}

// Export for module usage
export { Calculator, ICalculator };
