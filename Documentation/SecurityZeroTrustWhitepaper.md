# Evermail Zero-Trust Content Protection – Technical White Paper

## Executive summary
Evermail processes sensitive email archives for multi-tenant customers. Traditional “encrypt at rest + RBAC” controls are insufficient because platform operators (or a compromised supply chain) could still access decrypted content while ingestion/search jobs run. To guarantee that **not even Evermail superadmins can read customer mail**, we combine customer-managed keys (CMK) with Azure Confidential Computing:

1. **Envelope encryption** – Every mailbox has its own AES-256-GCM Data Encryption Key (DEK). The DEK is wrapped with the tenant’s TMK stored in their Azure Key Vault/Managed HSM.
2. **Secure Key Release (SKR)** – Azure Key Vault only releases the DEK to workloads that present Microsoft Azure Attestation (MAA) proofs showing they run inside a Trusted Execution Environment (TEE).
3. **Confidential containers** – Ingestion, search, and AI services run inside Azure Confidential Container Apps/AKS pods backed by AMD SEV-SNP hardware. Plaintext exists solely inside the enclave.
4. **Immutable audit trails** – Azure Confidential Ledger records every key unwrap event so tenants (and auditors) can verify access history.

This paper documents the architecture, phased rollout, and operational controls that make Evermail’s encryption story verifiable.

## Threat model refresh
- **Assets**: Email content, attachments, search indexes, TMKs/DEKs, audit logs.
- **Attackers**: External intruders, malicious insiders, compromised CI/CD pipeline, hostile cloud operator.
- **Objectives**: Read or tamper with plaintext email content; exfiltrate keys; bypass tenant isolation.
- **Assumptions**: Azure platform primitives (Key Vault, Container Apps, MAA) are trustworthy; tenants maintain control of their CMK; TLS is enforced end-to-end.

## Architecture overview
![Envelope encryption flow](https://learn.microsoft.com/en-us/azure/confidential-computing/media/concept-skr-attestation/skr-flow.png) *(See [Microsoft SKR documentation](https://learn.microsoft.com/en-us/azure/confidential-computing/concept-skr-attestation))*.

1. **Tenant onboarding**  
   - Admin either generates or imports a key into their Azure Key Vault/Managed HSM (`RSA-HSM` or `EC-HSM`, `exportable=true`).  
   - Evermail stores only the Key Vault URI + key name and requests the minimum `release` permission for its managed identity.

2. **Mailbox processing**  
   - Upload pipeline generates a DEK (AES-256-GCM) per mailbox/upload, encrypts with the tenant TMK (`WrapKey`) and persists the wrapped blob.  
   - Queue message contains the `WrappedDekId`.

3. **Confidential worker**  
   - Worker runs inside Azure Confidential Container Apps or AKS confidential node pool (see [deployment models](https://learn.microsoft.com/en-us/azure/confidential-computing/confidential-computing-deployment-models) and [confidential containers overview](https://learn.microsoft.com/en-us/azure/confidential-computing/confidential-containers)).  
   - Before touching data, the worker attests to MAA, receives a signed token, and calls Key Vault `Release` with the token.  
   - Key Vault validates the SKR policy. Only if the attestation claims match the expected measurement hash does it release the DEK.  
   - All parsing, indexing, AI summarization, and response generation happen inside the enclave; outputs are re-encrypted with the DEK before they leave.

4. **Audit + compliance**  
   - Azure Confidential Ledger (priced at ~$3/day per ledger instance per [Microsoft announcement](https://techcommunity.microsoft.com/blog/azureconfidentialcomputingblog/price-reduction-and-upcoming-features-for-azure-confidential-ledger/4387491)) stores immutable events (`DekUnwrapped`, `LedgerEntryId`).  
   - Tenants can export the ledger to prove that no unauthorized decryptions occurred.

## Phased rollout

| Phase | Capabilities | Residual risk | Customer messaging |
| --- | --- | --- | --- |
| **Phase 1 – BYOK foundation (MVP)** | - Tenant wizard + CLI helper to create CMKs.<br>- Per-mailbox DEK generation, wrapping, rotation metadata.<br>- SKR policies authored with placeholder attestation claims.<br>- Strict RBAC/PIM on Key Vault release identities; full logging to Sentinel. | Evermail workers still run in standard Container Apps; emergency operators could attach debuggers. | “Evermail operators need break-glass approval to inspect plaintext. We monitor and log all key releases.” |
| **Phase 2 – Zero-trust enforcement** | - Workers redeployed to confidential containers (AMD SEV-SNP).<br>- SKR policies updated to require MAA claims (`x-ms-isolation-tee = sevsnpvm`, `x-ms-compliance-status = azure-compliant-cvm`).<br>- Azure Confidential Ledger records every unwrap.<br>- Contracts updated to forbid break-glass plaintext access. | None (operators cannot read data without tenant TMK + TEE attestation). | “Even Evermail admins cannot read your email. Decrypt operations only happen inside customer-approved TEEs.” |

## Operational controls
- **Key rotation** – Tenants can rotate TMKs; Evermail re-wraps DEKs asynchronously and updates `TmKeyVersion`. During rotation, the old key stays active until all DEKs are re-wrapped.
- **Attestation hygiene** – CI pipeline signs images and publishes measurement hashes. SKR policies reference those hashes; any deviation blocks key release.
- **Monitoring & alerts** – Key Vault diagnostic logs + Confidential Ledger feed dashboards (Grafana/Workbook). Alerts fire on unusual unwrap frequency or policy changes.
- **Disaster recovery** – Backups include wrapped DEKs, ledger snapshots, and attestation configs. Rebuild procedure first stands up a fresh confidential environment, re-establishes SKR, then restores mailboxes.

## Customer responsibilities
1. Maintain the TMK in an Azure subscription they control.  
2. Securely store key backups/offline copies for disaster recovery.  
3. Use Azure AD Conditional Access to restrict who can manage the Key Vault.

## Conclusion
By coupling tenant-owned keys with Secure Key Release and confidential containers, Evermail prevents any plaintext exposure outside tenant-approved TEEs. The combination of envelope encryption, attestation-bound decryption, and immutable audit logs delivers a verifiable zero-trust story that stands apart in the email-archive market. Phase 1 delivers immediate BYOK benefits; Phase 2 completes the promise that “not even Evermail’s superadmins can read your emails.”

