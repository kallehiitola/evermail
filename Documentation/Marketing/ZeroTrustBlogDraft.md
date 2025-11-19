# Blog Draft – “Evermail Can’t Read Your Email (And That’s the Point)”

**Working title ideas**
- “How Evermail’s Zero-Trust Engine Keeps Your Inbox Private”
- “Bring Your Own Key + Confidential Compute: The Security Combo Your Email Archive Deserves”

**Audience**: Privacy-conscious pros (law, finance, journalists) + security reviewers  
**CTA**: Join the waitlist for zero-trust preview / talk to sales

---

## Hero
> _“If your archive vendor can read your emails, it isn’t private. Here’s how we fixed that.”_

- One-liner: “Evermail combines customer-managed keys with Azure Confidential Computing so decrypt operations only happen inside hardware-backed enclaves. Not even our superadmins can peek.”
- CTA button: “Join the zero-trust preview”

## Problem framing
1. **Traditional SaaS reality** – Encryption-at-rest still trusts the provider. Root admins, scripts, or compromised pipelines can dump plaintext while jobs run.
2. **Regulated teams need more** – Attorneys, investigative journalists, compliance teams can’t risk insider access to privileged mail.
3. **BYOK ≠ isolation** – Handing you a key is nice, but if the provider runs all workloads on their own VMs, they can still capture plaintext once the key is unwrapped.

## Our approach (keep it plain language)
1. **You hold the master key** – During onboarding you create a key in your Azure tenant (or we guide you through it). We only store a reference.
2. **Per-mailbox locks** – Every upload gets its own AES key that’s wrapped with your master key. Lose one key? Only that mailbox is affected.
3. **Hardware-enforced compute** – Ingestion, search, and AI run inside Azure Confidential Container Apps powered by AMD SEV-SNP. Microsoft’s attestation service proves the code running in the enclave matches the image we signed.
4. **Secure Key Release** – Azure Key Vault refuses to hand over your key unless it sees that attestation proof. No enclave, no decrypt.
5. **Ledger-backed audit trail** – Azure Confidential Ledger records every key unwrap so you can verify (or export) who accessed what.

> Pull quote: “We don’t ask you to trust our policies. We built a system where the cloud physically can’t hand us your data.”

## Roadmap snapshot
| Stage | What’s live today | What’s next |
| --- | --- | --- |
| Phase 1 – BYOK Foundation | Tenant wizard, per-mailbox keys, wrap/unwrap logs, strict RBAC | |
| Phase 2 – Zero-Trust GA | ✅ Confidential workloads, ✅ secure key release, ✅ confidential ledger logging | Rolling out to all tenants Q1 |

## Customer story teaser
“Our legal ops team uploads years of privileged email onto Evermail. With zero-trust compute we can finally say: even the vendor can’t read it.” – _Amelia Hart, GC @ Steadfast Capital_ (placeholder quote)

## Tech deep dive (optional accordion)
- Link to the technical white paper.
- Bullet summary of Microsoft docs backing the design.

## CTA section
“Want to migrate archives without giving up privacy? Join the zero-trust preview or talk to our security team.”

Buttons:
- `Join the preview`
- `Book a security review`

---

### Notes for final copy
- Keep tone confident but transparent (call out what Phase 1 vs Phase 2 covers).
- Include diagrams/screenshots from the white paper.
- Add FAQ snippet: “Can Evermail ever see my data?” → Only if you explicitly toggle break-glass mode AND approve it, otherwise the enclave refuses.

