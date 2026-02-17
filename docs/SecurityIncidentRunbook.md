# Security Incident Runbook

## Scope
- Auth API
- Gateway
- JWT signing key and refresh token security
- Audit trail (`UserActionLogs`)

## Severity Levels
- `SEV-1`: confirmed key leak, mass account abuse, cross-tenant leak
- `SEV-2`: repeated brute-force or suspicious token activity
- `SEV-3`: isolated user compromise

## Detection Signals
- High rate of `LoginFailed` from same IP or username
- Unexpected `TokenRefresh` failures or token reuse
- `SecurityDenied` spikes for tenant-restricted endpoints

## Initial Response (0-15 min)
1. Assign incident owner and communication channel.
2. Capture timeline start (`UTC`) and affected systems.
3. Freeze risky admin actions (user creation/role changes) if needed.
4. Export current logs from `UserActionLogs` for incident window.

## Containment (15-30 min)
1. Rotate `JWT_SIGNING_KEY` immediately if compromise is suspected.
2. Revoke active refresh token families for affected users.
3. Restrict ingress at Gateway/IP allowlist if attack is ongoing.
4. Increase login throttling temporarily.

## Eradication (30-90 min)
1. Reset compromised credentials.
2. Invalidate suspicious sessions (`TokenRevoke` flow).
3. Patch root cause (policy gap, tenant check gap, key handling gap).
4. Confirm no sensitive secret exists in config files or source control.

## Recovery
1. Deploy fixed build.
2. Re-enable normal traffic progressively.
3. Monitor for recurrence for at least one full token lifetime.

## Evidence Checklist
- Incident start/end times
- Source IPs and user agents
- Affected user IDs and company codes
- Actions taken (key rotation, token revoke, password reset)
- Post-incident verification results

## Post-Incident Actions (within 24h)
1. Root cause analysis document.
2. Permanent controls added (tests, alerts, policy changes).
3. Update this runbook with lessons learned.
