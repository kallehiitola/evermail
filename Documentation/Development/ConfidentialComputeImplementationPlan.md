## Confidential Content Protection Implementation Plan

> Primary references:
> - [Microsoft Learn – Deployment models in confidential computing](https://learn.microsoft.com/en-us/azure/confidential-computing/confidential-computing-deployment-models)
> - [Microsoft Learn – Secrets and key management for confidential computing](https://learn.microsoft.com/en-us/azure/confidential-computing/secret-key-management)
> - [Microsoft Learn – Secure Key Release with AKV + Azure Confidential Computing](https://learn.microsoft.com/en-us/azure/confidential-computing/concept-skr-attestation)
> - [Microsoft Learn – Confidential containers on Azure](https://learn.microsoft.com/en-us/azure/confidential-computing/confidential-containers)
> - [Azure Confidential Ledger price update (Mar 1 2025)](https://techcommunity.microsoft.com/blog/azureconfidentialcomputingblog/price-reduction-and-upcoming-features-for-azure-confidential-ledger/4387491)

### Current status – 20 Nov 2025

**What’s done**
- `TenantEncryptionSettings` + `MailboxEncryptionState` entities, EF configuration, migrations, and REST endpoints under `/api/v1/tenants/encryption` are live (Phase 1 schema/API).
- Documentation, security white paper, and marketing draft updated to reflect the zero-trust roadmap.

**Done on 20 Nov**
- Admin encryption UI (`/admin/encryption`) and Key Vault onboarding script (`scripts/tenant-keyvault-onboarding.ps1`).
- `/api/v1/tenants/encryption/test` now hits Azure Key Vault via `DefaultAzureCredential` and records verification metadata.
- Queue + worker plumbing now creates `MailboxEncryptionState` rows, carries the ID through the message, and records key release events.
- Security.md updated with PIM, logging, and break-glass guardrails for Phase 1.

**Next iteration focus**
1. **Admin UX polish** – Add inline validation + telemetry to measure onboarding friction.
2. **MAA handshake prototype** – Build a local attestation stub so we can plug in SKR policies before confidential containers are ready.
3. **Encrypted search path** – Start plumbing deterministic token encryption so Phase 2 isn’t blocked on schema changes.
4. **Ledger POC** – Stand up Azure Confidential Ledger + health dashboards to capture key release proofs.

### Phase 1 (MVP) – BYOK foundation

| Workstream | Tasks | Notes |
| --- | --- | --- |
| Tenant onboarding | - Extend settings UI/API to collect Azure Key Vault URI + key name. <br> - Provide guided script (Azure CLI/Bicep) that creates an `RSA-HSM` or `EC-HSM` key with `exportable` flag and grants Evermail’s managed identity **Release** permission only. <br> - Script lives in `scripts/tenant-keyvault-onboarding.ps1` so tenants can review + audit. <br> - Persist `TenantKeyVaultUri`, `TenantKeyName`, `KeyVaultTenantId`, and `KeyVersion`. | Staged SKR JSON uses placeholder attestation claims so we can flip to real MAA statements later without re-onboarding tenants. |
| DEK lifecycle | - Generate a new AES-256-GCM DEK per mailbox upload. <br> - Call `WrapKey` with tenant TMK, store `WrappedDekBlob`, `DekVersion`, `CreatedAt`, `CreatedBy`, `TmKeyVersion`. <br> - Add rotation job that re-wraps DEKs when the tenant rotates their key version. | All metadata lives in `MailboxEncryptionState` table with TenantId FK. |
| Queue schema | - Add `WrappedDekId` and `DekVersion` to ingestion/deletion messages so background workers know which wrapped key to unwrap. <br> - Store `AttestationPolicyId` for future validation. | |
| Operational guardrails | - Enforce Azure AD PIM on the “Evermail Key Release” identity. <br> - Enable Key Vault logging → Log Analytics, alert on unexpected `ReleaseKey` calls. <br> - Document that superadmins retain break-glass access until Phase 2. | |

### Phase 2 – Zero-trust enforcement

| Workstream | Tasks | Notes |
| --- | --- | --- |
| Confidential infrastructure | - Provision Azure Confidential Container Apps environment (or AKS confidential node pool) in `evermail-secure` resource group. <br> - Deploy Microsoft Azure Attestation (MAA) provider per region. <br> - Update CI/CD to sign worker images and publish measurement hashes. | See deployment models doc for trade-offs between confidential VMs vs containers. |
| Secure Key Release | - Replace placeholder SKR JSON with real policy that requires `x-ms-isolation-tee.x-ms-attestation-type = sevsnpvm` and `x-ms-compliance-status = azure-compliant-cvm`. <br> - Reference the container image signer + measurement in the policy’s `anyOf/allOf` clauses. | Policy grammar: <https://learn.microsoft.com/en-us/azure/key-vault/keys/policy-grammar> |
| Worker changes | - Add attestation handshake: request token from MAA, attach to Key Vault `Release` call, verify success. <br> - Keep unwrapped DEK in enclave memory only. <br> - Re-encrypt search snippets/results before they leave the enclave. <br> - Emit structured audit events (`DekUnwrapped`, `MailboxBatchProcessed`). | Use Azure.Identity `DefaultAzureCredential` within the confidential container; managed identity must be scoped to Key Vault release action. |
| Observability | - Stand up Azure Confidential Ledger (~$3/day/ledger) to store append-only “who unwrapped what” evidence. <br> - Mirror ledger transaction IDs into `AuditLogs` for tenant self-service. | Blog reference above. |
| Rollout | - Tenant-by-tenant flag indicates whether SKR policies are pointed at confidential workloads. <br> - Migration script reprocesses existing mailboxes to ensure DEKs were wrapped by tenant TMK. <br> - Contracts/marketing flipped to “Evermail cannot read your mail” once 100% of ingestion/search requests originate from TEEs. | |

### Risk & mitigation matrix

| Risk | Mitigation |
| --- | --- |
| Tenant misconfigures Key Vault permissions | Provide automated validation (`/api/v1/tenants/{id}/encryption/test`) that attempts a dummy wrap/unwrap and surfaces actionable errors. |
| Attestation drift (image updated without policy change) | Integrate measurement hash generation with CI pipeline; failing attestation blocks deploy and posts to #security. |
| Performance overhead | Confidential Container Apps add ~5-10% latency; batch processing amortizes the cost. Monitor ingestion throughput and scale replicas accordingly. |
| Disaster recovery | Back up wrapped DEKs + ledger proofs. During DR, redeploy confidential environment, restore ledger, and re-run attestation bootstrap before rehydrating mailboxes. |

### Deliverables checklist

- [x] Updated docs (Architecture, Security, whitepaper, marketing blog)
- [x] Tenant onboarding wizard + CLI helper (API + Blazor UI + PowerShell helper shipped; UX polish/telemetry still on backlog)
- [x] `MailboxEncryptionState` table + queue schema updates
- [ ] Confidential container infrastructure (Bicep/Terraform)
- [ ] Worker attestation + SKR integration tests
- [ ] Confidential Ledger provisioning + dashboards
- [ ] Runbook for tenant key rotation + break-glass
- [ ] GA announcement once 100% traffic flows through TEEs

