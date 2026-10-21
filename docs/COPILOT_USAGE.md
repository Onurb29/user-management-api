# Microsoft Copilot - Lab Evidence Log

> **Status: User reports Microsoft Copilot was used; actual prompt/suggestion evidence is still pending.** Issue #17 remains open until the user supplies and verifies those details. No Copilot contribution is inferred from Codex work.

## Purpose

Record how Microsoft Copilot contributed to scaffolding, validation, debugging, middleware, performance improvements, and tests, and how its suggestions were reviewed and verified.

## Entry template

### YYYY-MM-DD - Issue #__ - Short topic

**Task / problem:**  
_Describe the engineering problem._

**Prompt provided to Microsoft Copilot:**  
```text
Paste the real prompt here.
```

**Copilot's suggestion:**  
_Summarize the proposal. Link to a PR/commit when practical._

**Engineering decision:**  
Accepted / Modified / Rejected, with a reason.

**Verification:**  
_Describe automated tests, manual request, logs, or measurements that showed the result._

**What I learned:**  
_Describe the concept, bug, or trade-off._

---

## Final lab summary

Complete this section near v1.0.0. Describe which parts benefited from Copilot and at least one example of a defect or improvement verified by your own tests.

> ChatGPT and Microsoft Copilot are different tools. Attribute contributions accurately.

## Verified ChatGPT / Codex assistance (separate attribution)

On 2026-10-21 the user authorized: “Take over implementation and tests; prepare PRs for my review.” Codex implemented and tested the CRUD, security/logging, and verification batches in dependent PRs. This is Codex assistance, not evidence of Microsoft Copilot usage.

Concrete suggestion-to-verification example: initial request logging used safe route templates, but a privacy test found an ASP.NET ambient RequestPath property containing a synthetic private marker. The implementation added a metadata allowlist and tests confirmed the marker, email and bearer token were absent. Another test found that exception handling cleared correlation headers; setting the header in OnStarting fixed the 500 response case.

The user should add their actual Copilot prompt, suggestion, decision and verification using the template above. Do not relabel the Codex examples as Copilot evidence.
