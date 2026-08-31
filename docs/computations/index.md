# Computations

Sanctioned procedures declared as OKF **Attested Computations**. Each names the runtime it runs on,
the parameters a caller may vary, the executor that runs it, the receipt fields a run must return as
evidence, and the deterministic attester that turns a receipt into a verdict.

The point of declaring a procedure this way is that running it stops being a claim and becomes a
receipt. "I ran the determinism tests" is prose; a receipt the attester passes is evidence.

* [determinism-baseline](determinism-baseline.md) — the simulation fingerprint gate. Proves the
  tick loop still produces the recorded output for a known board and tick count.

Attesters live in `../attesters/`; executors in `../skills/`.
