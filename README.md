# Trade (arachnode.tts)
**Open-Source Science. Institutional Delivery.**

[![License: Restricted Commercial](https://img.shields.io/badge/License-Restricted_Commercial-red.svg)](#eula-commercial-licensing--anti-circumvention)
[![Status: Production](https://img.shields.io/badge/Status-Production-success.svg)]()
[![Website: Form4TTS.com](https://img.shields.io/badge/Website-Form4TTS.com-blue.svg)](https://www.form4tts.com)

**Form4TTS** is a high-performance, deterministic pipeline for SEC EDGAR Form 4 telemetry, designed specifically for proprietary operators, quants, and LLCs claiming IRS Trader Tax Status (TTS).

The retail trading market is plagued by "black-box" mystery and unverified backtests. We do the opposite. Our mathematical architecture is open for public audit. **The pipeline is free. The logic is open. The real-time execution feed is the service.**

---

## 🏛 The Manifesto: Trust Through Verification
Real trust comes from inspection. If you cannot fork the code, audit the `SECHttpClient` compliance, or reproduce the `ClusterBuyAnalyzer` scores, you shouldn't risk your capital trading the signal. 

This repository contains the **production-grade DNA** of our system. It allows you to independently verify our $250k discretionary thresholds, 90-day drift matrices, and strict SEC Form 4 ingestion protocols before ever subscribing to our live telemetry feed.

---

## ⚙️ Core Architecture (The Science)

The foundational C# classes and mathematical models used to generate our signals are permanently open for research:

* **`SECHttpClient`**: Zero-latency SEC EDGAR ingestion protocols featuring strict 100ms rate-limit discipline and optimized GZip stream handling.
* **`ClusterBuyAnalyzer`**: Deterministic scoring logic applying role-weighted hierarchy math to isolate Tier A+ corporate insider buying clusters.
* **`SimplePnLTracker`**: VOO-rotation portfolio strategy engine featuring a strict 10% core liquidity guard.
* **`PositionId` Auditing**: Immutable SEC audit trail logs mapping execution metadata to raw official XML Accession Numbers for IRS compliance.

---

## 📡 The Hosted Service (The Signal)

While the logic is free, maintaining a high-availability, low-latency pipeline to the SEC network requires institutional operations. The [Form4TTS Professional Subscription](https://www.form4tts.com/licensing.html) provides:

1. **Zero-Latency Feed:** Real-time SignalR/Telegram broadcasts of Tier A+ detections.
2. **Execution Advisory API:** Cloud-based risk co-pilot returning mathematically optimized execution sizing calibrated to current market volatility and your specific account leverage constraints.
3. **The TTS Audit Defense:** Daily Form 4 Rationale Digests documenting your *discriminant involvement*. Archiving these timestamped reports provides the structural proof necessary to defend your IRC Section 475 Mark-to-Market (MTM) and Section 162 deductions during a tax audit.

---

## 🚀 Getting Started (Local Audit)

*(Instructions for cloning, building, and running the open-source pipeline locally for historical backtesting and math verification).*

```bash
# Clone the repository
git clone [https://github.com/arachnodedotnet/arachnode.tts.git](https://github.com/arachnodedotnet/arachnode.tts.git)

# Navigate to the project directory
cd arachnode.tts

# Build the solution
dotnet build

# Run the historical audit parser
dotnet run --project src/Form4.Analyzer --mode audit
