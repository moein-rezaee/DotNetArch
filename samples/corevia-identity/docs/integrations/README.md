[فارسی](./README.fa.md) | [NopCommerce Guide](./NOPCOMMERCE_M2M_INTEGRATION.md) | [Identity README](../../README.md)

# IdentityService Integration Guides

Last Updated: 2026-03-29
Owner: IdentityService Team
Status: Active
Version: 1.0.0

## Purpose
This index groups customer-facing integration guides for external consumers of `IdentityService`.

## Scope
- External machine-to-machine integration guides
- Consumer onboarding references
- Gateway-first integration paths for partner systems

## Prerequisites
- Access to the target client credentials
- Reachability to the public Gateway endpoint

## Configuration
- Keep customer-specific secrets out of shared internal docs.
- Put per-customer integration guides in this folder instead of the service root.

## Run / Usage
- Start from the guide matching the consumer system.

## Validation / Verification
- Validate sample token acquisition and protected API calls against the documented Gateway path.

## Troubleshooting
- If a guide becomes customer-specific beyond reuse, move sensitive values to a private delivery channel and keep only the reusable integration steps here.

## Change Log
- 2026-03-29: Created service-local folder for customer-facing integration guides.

## Ownership
- IdentityService Team
