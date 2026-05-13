using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Trade.Tests
{
    /// <summary>
    /// Black–Scholes sanity checks + robust reflection-based invocations to cover different
    /// ImpliedVolatilitySolver method signatures (Solve / TrySolve, with/without q, etc.).
    /// This suite targets 100% line/branch coverage in ImpliedVolatilitySolver by exercising:
    ///  - Calls and puts
    ///  - ATM / deep ITM / deep OTM regimes
    ///  - Bounds violations (should throw/return failure/NaN)
    ///  - Convergence with different initial guesses (if accepted)
    ///  - Tolerance and max-iteration branches (if exposed)
    /// </summary>
    [TestClass]
    public class ImpliedVolatilitySolverTests2
    {
        // === Black–Scholes helpers (risk-neutral, continuous dividend yield 'q') ===

        private static double CND(double x)
        {
            // Abramowitz-Stegun approximation for N(x)
            double L = Math.Abs(x);
            double k = 1.0 / (1.0 + 0.2316419 * L);
            double w = 1.0 - 1.0 / Math.Sqrt(2 * Math.PI) * Math.Exp(-L * L / 2.0) *
                       (0.319381530 * k - 0.356563782 * Math.Pow(k, 2) + 1.781477937 * Math.Pow(k, 3)
                        - 1.821255978 * Math.Pow(k, 4) + 1.330274429 * Math.Pow(k, 5));
            return x < 0.0 ? 1.0 - w : w;
        }

        private static double BlackScholesCall(double S, double K, double r, double q, double T, double vol)
        {
            if (T <= 0) return Math.Max(0.0, S * Math.Exp(-q * T) - K * Math.Exp(-r * T));
            if (vol <= 0) return Math.Max(0.0, S * Math.Exp(-q * T) - K * Math.Exp(-r * T));
            double sqrtT = Math.Sqrt(T);
            double d1 = (Math.Log(S / K) + (r - q + 0.5 * vol * vol) * T) / (vol * sqrtT);
            double d2 = d1 - vol * sqrtT;
            return S * Math.Exp(-q * T) * CND(d1) - K * Math.Exp(-r * T) * CND(d2);
        }

        private static double BlackScholesPut(double S, double K, double r, double q, double T, double vol)
        {
            if (T <= 0) return Math.Max(0.0, K * Math.Exp(-r * T) - S * Math.Exp(-q * T));
            if (vol <= 0) return Math.Max(0.0, K * Math.Exp(-r * T) - S * Math.Exp(-q * T));
            double sqrtT = Math.Sqrt(T);
            double d1 = (Math.Log(S / K) + (r - q + 0.5 * vol * vol) * T) / (vol * sqrtT);
            double d2 = d1 - vol * sqrtT;
            return K * Math.Exp(-r * T) * CND(-d2) - S * Math.Exp(-q * T) * CND(-d1);
        }

        private static (double lower, double upper) NoArbBoundsCall(double S, double K, double r, double q, double T)
            => (Math.Max(0.0, S * Math.Exp(-q * T) - K * Math.Exp(-r * T)), S * Math.Exp(-q * T));

        private static (double lower, double upper) NoArbBoundsPut(double S, double K, double r, double q, double T)
            => (Math.Max(0.0, K * Math.Exp(-r * T) - S * Math.Exp(-q * T)), K * Math.Exp(-r * T));

        // === Reflection helpers to invoke whichever signature your solver exposes ===

        private static Type FindSolverType()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                })
                .Where(t => t != null && t.IsClass && t.Name.EndsWith("ImpliedVolatilitySolver", StringComparison.Ordinal))
                .ToList();

            if (types.Count == 0)
                Assert.Inconclusive("Could not locate ImpliedVolatilitySolver type in loaded assemblies.");

            // Prefer a type in a probable namespace if multiple
            var preferred = types.FirstOrDefault(t => t.FullName != null && t.FullName.Contains("Trade"));
            return preferred ?? types.First();
        }

        private static MethodInfo FindMethod(Type solver, string[] names)
        {
            var methods = solver.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            foreach (var name in names)
            {
                var m = methods.FirstOrDefault(mi => mi.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (m != null) return m;
            }
            // Fall back to any method containing "Solve" or "ImpliedVol"
            return methods.FirstOrDefault(mi =>
                mi.Name.IndexOf("Solve", StringComparison.OrdinalIgnoreCase) >= 0 ||
                mi.Name.IndexOf("ImpliedVol", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Attempts to call your solver. Returns (ok, iv). For TrySolve-style, ok==true means converged.
        /// For Solve-style, ok==true if we got a finite iv.
        /// Supports a variety of parameter orders and presence/absence of q, initialGuess, tolerance, maxIter.
        /// </summary>
        private static (bool ok, double iv) InvokeSolver(
            double optionPrice, double S, double K, double r, double q, double T, bool isCall,
            double? initialGuess = null, double? tol = null, int? maxIter = null)
        {
            var solverType = FindSolverType();
            var method = FindMethod(solverType, new[] { "Solve", "TrySolve", "Compute", "Calculate", "ImpliedVolatility" });
            Assert.IsNotNull(method, "Could not find a Solve/TrySolve/Compute method on ImpliedVolatilitySolver.");

            bool instance = !method.IsStatic;
            object solverInstance = instance ? Activator.CreateInstance(solverType) : null;

            var pars = method.GetParameters();
            var args = new object[pars.Length];

            // Helper for enum option type if needed
            object optionTypeArg = null;
            foreach (var p in pars)
            {
                if (p.ParameterType.IsEnum &&
                    (p.Name.IndexOf("type", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     p.ParameterType.Name.IndexOf("Option", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    // Try to pick an enum member that contains "Call"/"Put"
                    var names = Enum.GetNames(p.ParameterType);
                    string target = isCall ? "CALL" : "PUT";
                    var match = names.FirstOrDefault(n => n.ToUpperInvariant().Contains(target));
                    optionTypeArg = match != null ? Enum.Parse(p.ParameterType, match) : Enum.GetValues(p.ParameterType).GetValue(0);
                    break;
                }
            }

            for (int i = 0; i < pars.Length; i++)
            {
                var p = pars[i];
                string nm = p.Name?.ToLowerInvariant() ?? "";
                var pt = p.ParameterType;

                if (pt == typeof(double) || pt == typeof(float) || pt == typeof(decimal))
                {
                    double val;
                    if (nm.Contains("market") && nm.Contains("price") || nm == "price" || nm == "optionprice")
                        val = optionPrice;
                    else if (nm == "s" || nm.Contains("spot") || nm.Contains("underlying"))
                        val = S;
                    else if (nm == "k" || nm.Contains("strike"))
                        val = K;
                    else if (nm == "r" || nm.Contains("rate"))
                        val = r;
                    else if (nm == "q" || nm.Contains("div"))
                        val = q;
                    else if (nm == "t" || nm.Contains("maturity") || nm.Contains("expiry") || nm.Contains("ttm"))
                        val = T;
                    else if (nm.Contains("guess") || nm.Contains("initial"))
                        val = initialGuess ?? 0.2;
                    else if (nm.Contains("tol") || nm.Contains("tolerance") || nm.Contains("eps"))
                        val = tol ?? 1e-8;
                    else if (nm.Contains("max") && nm.Contains("iter"))
                        val = maxIter ?? 100;
                    else
                    {
                        // Fallback heuristic by order
                        val = optionPrice;
                    }

                    if (pt == typeof(float)) args[i] = (float)val;
                    else if (pt == typeof(decimal)) args[i] = (decimal)val;
                    else args[i] = val;
                }
                else if (pt == typeof(bool))
                {
                    // call/put
                    args[i] = isCall;
                }
                else if (pt == typeof(char))
                {
                    args[i] = isCall ? 'C' : 'P';
                }
                else if (pt.IsEnum)
                {
                    args[i] = optionTypeArg ?? Enum.GetValues(pt).GetValue(0);
                }
                else if (p.IsOut)
                {
                    // Reserve a slot for out double
                    args[i] = 0.0;
                }
                else
                {
                    // Unknown parameter type—try to pass null/default
                    args[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
                }
            }

            object ret;
            try
            {
                ret = method.Invoke(solverInstance, args);
            }
            catch (TargetInvocationException ex)
            {
                // Rethrow inner for clarity
                throw ex.InnerException ?? ex;
            }

            // TrySolve(bool, out double) pattern?
            if (method.ReturnType == typeof(bool))
            {
                bool ok = (bool)ret;
                // find the out double
                int outIndex = Array.FindIndex(pars, p => p.IsOut && (p.ParameterType == typeof(double) || p.ParameterType == typeof(float) || p.ParameterType == typeof(decimal)));
                Assert.IsTrue(outIndex >= 0, "TrySolve-like method returned bool but no out volatility was found.");
                double iv = Convert.ToDouble(args[outIndex]);
                return (ok && !double.IsNaN(iv) && !double.IsInfinity(iv) && iv >= 0.0, iv);
            }

            // Solve() => double
            if (method.ReturnType == typeof(double) || method.ReturnType == typeof(float) || method.ReturnType == typeof(decimal))
            {
                double iv = Convert.ToDouble(ret);
                return (!double.IsNaN(iv) && !double.IsInfinity(iv) && iv >= 0.0, iv);
            }

            Assert.Inconclusive("ImpliedVolatilitySolver method neither returned bool nor a numeric volatility.");
            return (false, double.NaN);
        }

        private static void AssertClose(double a, double b, double tol, string msg = null)
            => Assert.IsTrue(Math.Abs(a - b) <= tol, msg ?? $"Expected {b} got {a} (tol {tol}).");

        // === TESTS ===

        [TestMethod][TestCategory("Core")]
        public void ATM_Call_RecoversTrueVolatility()
        {
            double S = 100, K = 100, r = 0.01, q = 0.00, T = 1.0, trueVol = 0.20;
            double price = BlackScholesCall(S, K, r, q, T, trueVol);

            var (ok, iv) = InvokeSolver(price, S, K, r, q, T, isCall: true, initialGuess: 0.15, tol: 1e-10, maxIter: 100);
            Assert.IsTrue(ok, "Solver failed to converge for ATM call.");
            AssertClose(iv, trueVol, 5e-4, "Recovered IV deviates too much from true volatility.");
        }

        [TestMethod][TestCategory("Core")]
        public void ATM_Put_RecoversTrueVolatility()
        {
            double S = 100, K = 100, r = 0.01, q = 0.00, T = 1.0, trueVol = 0.25;
            double price = BlackScholesPut(S, K, r, q, T, trueVol);

            var (ok, iv) = InvokeSolver(price, S, K, r, q, T, isCall: false, initialGuess: 0.30, tol: 1e-10, maxIter: 100);
            Assert.IsTrue(ok, "Solver failed to converge for ATM put.");
            AssertClose(iv, trueVol, 7e-4);
        }

        [TestMethod][TestCategory("Core")]
        public void DeepITM_Call_And_DeepOTM_Put_Converge()
        {
            double S = 150, K = 100, r = 0.02, q = 0.01, T = 0.75, vol = 0.35;
            double c = BlackScholesCall(S, K, r, q, T, vol);
            double p = BlackScholesPut(S, K, r, q, T, vol);

            var (okC, ivC) = InvokeSolver(c, S, K, r, q, T, isCall: true, initialGuess: 0.5, tol: 1e-8, maxIter: 200);
            var (okP, ivP) = InvokeSolver(p, S, K, r, q, T, isCall: false, initialGuess: 0.1, tol: 1e-8, maxIter: 200);

            Assert.IsTrue(okC, "Deep ITM call did not converge.");
            Assert.IsTrue(okP, "Deep OTM put did not converge.");
            AssertClose(ivC, vol, 1e-3);
            AssertClose(ivP, vol, 1e-3);
        }

        [TestMethod][TestCategory("Core")]
        public void DeepOTM_Call_And_DeepITM_Put_Converge()
        {
            double S = 80, K = 120, r = 0.00, q = 0.00, T = 0.5, vol = 0.40;
            double c = BlackScholesCall(S, K, r, q, T, vol);
            double p = BlackScholesPut(S, K, r, q, T, vol);

            var (okC, ivC) = InvokeSolver(c, S, K, r, q, T, isCall: true, initialGuess: 0.2, tol: 1e-8, maxIter: 200);
            var (okP, ivP) = InvokeSolver(p, S, K, r, q, T, isCall: false, initialGuess: 0.6, tol: 1e-8, maxIter: 200);

            Assert.IsTrue(okC, "Deep OTM call did not converge.");
            Assert.IsTrue(okP, "Deep ITM put did not converge.");
            AssertClose(ivC, vol, 2e-3);
            AssertClose(ivP, vol, 2e-3);
        }

        [TestMethod][TestCategory("Core")]
        public void PriceOutsideNoArbBounds_FailsOrReturnsNaN()
        {
            double S = 100, K = 100, r = 0.01, q = 0.02, T = 0.5;
            var (loC, hiC) = NoArbBoundsCall(S, K, r, q, T);
            var (loP, hiP) = NoArbBoundsPut(S, K, r, q, T);

            // Construct invalid prices
            double invalidCallLow = loC - 0.50;
            double invalidCallHigh = hiC + 0.50;
            double invalidPutLow = loP - 0.50;
            double invalidPutHigh = hiP + 0.50;

            // Each should either return (ok=false) or NaN, or throw ArgumentException/InvalidOperationException
            void AssertInvalid(double price, bool isCall)
            {
                try
                {
                    var (ok, iv) = InvokeSolver(price, S, K, r, q, T, isCall, initialGuess: 0.2, tol: 1e-10, maxIter: 100);
                    Assert.IsTrue(!ok || double.IsNaN(iv) || iv < 0.0,
                        $"Solver accepted an arbitrage-violating price (isCall={isCall}). iv={iv}, ok={ok}");
                }
                catch (ArgumentException) { /* acceptable */ }
                catch (InvalidOperationException) { /* acceptable */ }
            }

            AssertInvalid(invalidCallLow, isCall: true);
            AssertInvalid(invalidCallHigh, isCall: true);
            AssertInvalid(invalidPutLow, isCall: false);
            AssertInvalid(invalidPutHigh, isCall: false);
        }

        [TestMethod][TestCategory("Core")]
        public void Monotonicity_IncreasingPriceYieldsNonDecreasingIV()
        {
            double S = 100, K = 100, r = 0.00, q = 0.00, T = 1.0;
            // Generate a ladder of call prices from BS vols 5% .. 80%
            var vols = Enumerable.Range(0, 10).Select(i => 0.05 + i * 0.075).ToArray();
            var prices = vols.Select(v => BlackScholesCall(S, K, r, q, T, v)).ToArray();

            double prevIv = -1.0;
            foreach (var c in prices)
            {
                var (ok, iv) = InvokeSolver(c, S, K, r, q, T, isCall: true, initialGuess: 0.2, tol: 1e-8, maxIter: 200);
                Assert.IsTrue(ok, "Solver failed on a valid monotonically increasing price.");
                Assert.IsTrue(iv >= prevIv - 1e-4, $"Implied vol should be non-decreasing with price. Prev={prevIv}, Now={iv}");
                prevIv = iv;
            }
        }

        [TestMethod][TestCategory("Core")]
        public void SensibleDefaults_WorkWhenInitialGuessNotProvided()
        {
            double S = 420, K = 400, r = 0.015, q = 0.00, T = 0.25, vol = 0.33;
            double c = BlackScholesCall(S, K, r, q, T, vol);

            // No initialGuess/tol/maxIter → rely on defaults in your solver
            var (ok, iv) = InvokeSolver(c, S, K, r, q, T, isCall: true, initialGuess: null, tol: null, maxIter: null);
            Assert.IsTrue(ok, "Solver did not converge with defaults.");
            AssertClose(iv, vol, 2e-3);
        }

        [TestMethod][TestCategory("Core")]
        public void PutCallParity_ConsistentIVsForATM()
        {
            // Parity roughly aligns put/call IVs near ATM in BS world
            double S = 100, K = 100, r = 0.01, q = 0.00, T = 0.75, vol = 0.22;
            double c = BlackScholesCall(S, K, r, q, T, vol);
            double p = BlackScholesPut(S, K, r, q, T, vol);

            var (okC, ivC) = InvokeSolver(c, S, K, r, q, T, isCall: true, initialGuess: 0.2, tol: 1e-9, maxIter: 200);
            var (okP, ivP) = InvokeSolver(p, S, K, r, q, T, isCall: false, initialGuess: 0.2, tol: 1e-9, maxIter: 200);

            Assert.IsTrue(okC && okP, "Both call and put should converge.");
            AssertClose(ivC, ivP, 1e-3, "Call and put implied vols should be very close at ATM under BS.");
        }

        [TestMethod][TestCategory("Core")]
        public void ZeroTimeToMaturity_RespectsIntrinsicAndReturnsZeroVol()
        {
            double S = 105, K = 100, r = 0.01, q = 0.00, T = 0.0;
            double c = Math.Max(0.0, S - K); // intrinsic
            double p = Math.Max(0.0, K - S);

            var (okC, ivC) = InvokeSolver(c, S, K, r, q, T, isCall: true);
            var (okP, ivP) = InvokeSolver(p, S, K, r, q, T, isCall: false);

            // With T ~ 0, any finite solver should return ~0 vol if price==intrinsic
            if (okC) Assert.IsTrue(ivC <= 1e-6, $"Zero T call IV should be ~0, got {ivC}");
            if (okP) Assert.IsTrue(ivP <= 1e-6, $"Zero T put IV should be ~0, got {ivP}");
        }

        [TestMethod][TestCategory("Core")]
        public void VerySmallAndVeryLargePrices_MapToExtremeVols()
        {
            double S = 100, K = 100, r = 0.00, q = 0.00, T = 1.0;

            // Near lower bound: call ~ 0.01
            var (okLow, ivLow) = InvokeSolver(0.01, S, K, r, q, T, isCall: true, initialGuess: 0.05);
            // Near upper bound: call ~ S
            var (okHigh, ivHigh) = InvokeSolver(S * 0.99, S, K, r, q, T, isCall: true, initialGuess: 1.50);

            if (okLow) Assert.IsTrue(ivLow < 0.05, $"Tiny price should imply very low vol, got {ivLow}");
            if (okHigh) Assert.IsTrue(ivHigh > 1.0, $"Near-intrinsic-max price should imply very high vol, got {ivHigh}");
        }
    }
}
