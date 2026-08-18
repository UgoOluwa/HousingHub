---
description: Work a phase of the dev/production environment split
argument-hint: "[phase number, e.g. 3]"
---

Phase: `$1`

Read `docs/environment-separation-plan.md` for the phase in full, and
`docs/handoff.md` for what has already been done. Do not re-derive the plan —
the decisions are settled:

- One AWS account, resources prefixed (`prod_` tables, `-prod` Lambdas)
- `develop` → dev, `master` → production
- Custom domain on production only
- Both frontends on Vercel

**Before touching anything, say which parts of this phase you can do and which
need the user.** Anything in the AWS console, Vercel dashboard, GitHub settings,
DNS, or a third-party account is theirs. Workflow files, application config and
code are yours. Do not describe a console click as though you performed it.

Constraints that hold in every phase:

- **`Dynamo:TablePrefix` stays empty in dev.** The existing tables are
  unprefixed; a prefix orphans every row.
- **The prefix is read through `DynamoDbNaming` by two places** — the
  `IDynamoDBContext` and `DynamoDbTableInitializer`. Never configure one
  directly; they must not be settable apart.
- **`Cors:AllowedOrigins` is credentialed.** A stale origin left in the
  production list is a real hole. Never allow a `*.vercel.app` wildcard.
- **Production secrets are generated fresh**, never copied from dev.
- **`deploy-function` does not update environment variables.** A new setting
  added in code is missing in AWS until someone sets it by hand.

If the phase changes `deploy.yml` or `scheduled-workers.yml`, validate the YAML
parses before finishing.

Finish by updating `docs/handoff.md` so the phase table reflects reality.
