# Trade Interfaces

This folder contains the interface definitions for the Trade application's core classes.

## Purpose

These interfaces provide clean abstractions for the main components of the trading system, enabling:
- **Dependency Injection**: Easier testing and mocking
- **Loose Coupling**: Reduced dependencies between components  
- **Testability**: Simplified unit testing with mock implementations
- **Extensibility**: Easy to create alternative implementations

## Interfaces

### IImpliedVolatilitySolver

Interface for implied volatility calculations and option pricing using the Black-Scholes model.

**Key Features:**
- Black-Scholes option pricing
- Implied volatility solving using bisection method
- Price data loading from CSV files
- Caching for performance optimization
- Historical volatility calculations

**Usage Example:**
```csharp
IImpliedVolatilitySolver solver = new ImpliedVolatilitySolver();
solver.LoadClosePrices("market_data.csv");

// Calculate option price
double optionPrice = solver.BlackScholesPrice(
    S: 100.0,    // Current stock price
    K: 105.0,    // Strike price  
    T: 0.25,     // 3 months to expiration
    r: 0.05,     // 5% risk-free rate
    q: 0.02,     // 2% dividend yield
    sigma: 0.20, // 20% volatility
    isCall: true // Call option
);

// Solve for implied volatility
double impliedVol = solver.SolveIV(100.0, 105.0, 0.25, 0.05, 0.02, 8.50, true);
```

### IOptionContract

Interface representing a single option contract for IV calculation.

**Key Properties:**
- `ValuationDate`: The date for price valuation
- `Strike`: Option strike price
- `TimeToExpiration`: Time to expiration in years
- `RiskFreeRate`: Risk-free interest rate
- `DividendYield`: Continuous dividend yield
- `MarketPrice`: Current market price of the option
- `IsCall`: True for call, false for put

**Usage Example:**
```csharp
IOptionContract option = new OptionContract
{
    ValuationDate = DateTime.Today,
    Strike = 105.0,
    TimeToExpiration = 0.25,
    RiskFreeRate = 0.05,
    DividendYield = 0.02,
    MarketPrice = 8.50,
    IsCall = true
};
```

## Implementation Notes

- **Thread Safety**: The current implementations are not thread-safe. Consider thread-safe implementations for multi-threaded scenarios.
- **Performance**: Caching mechanisms are implemented for frequently accessed calculations.
- **Error Handling**: Methods include comprehensive input validation and return NaN for invalid scenarios.
- **Precision**: Default tolerance for IV solving is 1e-6 with maximum 100 iterations.

## Framework Compatibility

- **Target Framework**: .NET Framework 4.7.2
- **C# Version**: 7.3
- **Dependencies**: System.Collections.Generic, System.Globalization, System.IO

## Testing

Both interfaces have comprehensive test suites:
- `ImpliedVolatilitySolverTests.cs` - Black-Scholes pricing and IV solving tests
- Unit tests cover edge cases, boundary conditions, and mathematical accuracy

## Future Enhancements

Potential interface extensions:
- Async method variants for large data processing
- Generic type parameters for different numerical precision
- Additional option pricing models (e.g., Binomial, Monte Carlo)
- Real-time market data integration interfaces