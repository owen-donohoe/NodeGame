# write-sim-test

## When to use
When adding or modifying simulation behavior that needs test coverage.
Invoke explicitly: "use write-sim-test to create a test for this."

## Procedure
Step 0: Inspect the existing test framework and existing test patterns before generating tests

Step 1: Identify what is being tested
- What system or behavior changed?
- What is the expected outcome given known inputs?
- Is this a correctness test, a determinism test, or both?

Step 2: Set up initial state
- Create a minimal SimulationState with only what the test needs
- Use explicit integer values -- no magic numbers without comments
- Document what the starting state represents

Step 3: Define the command sequence
- List the GameCommands in the order they will be applied
- Use real CommandTypes from the actual codebase
- Document why each command is in the sequence

Step 4: Advance ticks
- Run SimulateTick the exact number of ticks needed
- Document what should happen each tick
- Do not over-tick -- test the minimum needed to verify behavior

Step 5: Assert expected state
- Assert specific integer field values on SimulationState
- Assert villager states, node ownership, resource counts 
  as relevant
- One assertion per logical outcome -- do not bundle unrelated 
  assertions

Step 6: Add determinism variant (always, for simulation tests)
- Run the identical scenario a second time from scratch
- Hash both resulting states with SimulationStateHasher
- Assert hash A == hash B
- Name this test with _Determinism suffix

## Output format
Produce two test methods:
1. The correctness test asserting expected state
2. The determinism test asserting hash equality
Both in the existing test assembly and namespace.
Flag any SimulationState fields that appear to be missing 
from SimulationStateHasher.
