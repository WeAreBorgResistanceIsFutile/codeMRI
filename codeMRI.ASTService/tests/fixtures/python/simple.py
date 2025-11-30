# Simple Python file for testing

def calculate_sum(a, b):
    """Add two numbers and return the result."""
    return a + b

class Calculator:
    def __init__(self):
        self.value = 0
    
    def add(self, x):
        """Add a number to the current value."""
        self.value += x
        return self.value

if __name__ == "__main__":
    calc = Calculator()
    result = calc.add(5)
    print(f"Result: {result}")
