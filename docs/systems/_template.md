---
system: example
summary: One line, at most 140 characters. What this system is for, not how it works.
sources:
  - Assets/Scripts/Example/
verified: 0000000
---
<!-- Copy this file to <system>.md. Budget: 1,100 words for the body, enforced by
     scripts/doc-budget.ps1. Keep the headings, in this order; write "None." under one
     that has nothing to say. Present tense, what is true now. No dates, PR numbers,
     line numbers or history: git and Notion hold those. Name types and files only
     where a reader must find them; the code is the detail. -->

# Example

## Purpose
Two to four lines: why this system exists and what would break without it.

## Interface
What other systems may call, read or subscribe to, and what this system produces.
Entry points, events, data shapes, by type name. A neighbour should be able to use
this system correctly from this section alone.

## Connects to
- `other-system`: what passes between them, which way, through which type.

## Invariants
Rules that must hold. Each one is a thing a change could break.

## How it works
The mechanism, condensed: the flow a reader needs to predict behaviour without
opening the code.

## Decisions
- Why it is this way, and the alternative that was rejected. One line each.

## Pressure points
Known debt, scaling limits, and places where a different architecture would pay
off. This is where an orchestrator looks before building on top of the system.

## Change recipes
- The common changes, as steps: "add a page", "add a command". Name what must
  change together.

## Tests
Which test projects or suites guard this system, and what they do not cover.
