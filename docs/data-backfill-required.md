# Data backfill required

Two pre-existing data problems surfaced during the security work. Neither is caused
by these changes, but both constrain what can safely be enforced in code.

---

## 1. `Customer.IsActive` is false for every existing row

**Severity: blocks a security control. Also a live bug in the admin UI.**

`IsActive` lives on `BaseEntity` as a plain `bool`, so it defaults to `false`, and
nothing in the registration path ever set it. Meanwhile
`CustomerCommandService.SuspendCustomer` sets it to `false` and
`ReactivateCustomer` sets it to `true` — so the field is genuinely intended to mean
"not suspended".

The result is that **every customer in the database currently reads as suspended**.

Two consequences:

- Anywhere the admin UI filters or displays on `IsActive`, users appear inactive
  when they are not. Worth checking the customers list — this is visible today,
  independent of any of this work.
- Any code that treats `IsActive` as authoritative for authentication would sign
  out the entire user base.

That second point nearly happened: a check was added to `AuthService.RefreshToken`
refusing to refresh an inactive account. Correct in principle, but with this data it
would have logged out every user within one access-token lifetime — roughly 30
minutes after deploy. It was removed before shipping.

### Fixed going forward

`Customer`'s constructor now sets `IsActive = true`, so new registrations are
correct.

### Backfill needed

Existing rows must be set to `true`, **except** any account that was genuinely
suspended. Those are indistinguishable from the default today, so identify them
first — from the admin audit trail, support records, or by asking whoever performs
suspensions. If nobody has ever suspended an account, every row can be set to
`true` safely.

```
Scan the Customers table.
For each item where IsActive is false or absent:
    if the account is on the known-suspended list: leave it
    else: set IsActive = true
```

### After the backfill

Re-add the check in `AuthService.RefreshToken` as defence in depth:

```csharp
if (!customer.IsActive)
{
    await RevokeAllRefreshTokensAsync(customer.Id);
    await _unitOfWork.SaveAsync();
    return new BaseResponse<LoginCustomerResponseDto>(
        null, false, string.Empty, ResponseMessages.InvalidRefreshToken);
}
```

Suspension is enforced in the meantime by revoking the token family at the moment
an admin suspends the account, which does not depend on this field.

---

## 2. Legacy KYC documents are in the public bucket

**Severity: sensitive personal data is world-readable.**

KYC identity documents now upload to a private key prefix and are served through
short-lived presigned URLs. Documents submitted before that change were written to
the public prefix, and `Customer.IdDocumentUrl` holds a full public URL rather than
an object key.

Those objects are **still publicly readable at a predictable URL shape**. Under the
NDPA, government identity documents exposed this way are a reportable problem.

The admin review endpoint detects the legacy shape and passes the URL through with
a note, so review still works — but that is a compatibility shim, not a fix.

### Backfill needed

For each customer whose `IdDocumentUrl` starts with `http`:

1. Copy the object from its public key to `private/kyc/{customerId}/{filename}`.
2. Update `IdDocumentUrl` to the new key.
3. Delete the public object.

Then confirm nothing outside `private/` remains under the old `kyc/` prefix.

### Also required, independent of the backfill

The bucket policy must deny anonymous `s3:GetObject` on `private/*`. Nothing in the
application can enforce this — without it, the private prefix is private in name
only and the whole change achieves nothing.

---

## 3. `Property.PublishedStatus` is absent on every existing row

**Severity: blocks a performance fix. Empties the homepage if enabled early.**

The public site's hottest query — the homepage, the listing page, "new", "trending"
and "nearby" — was `GetAllAsync(x => x.IsPublished)`. A bool cannot be a DynamoDB
key, so that predicate has nothing to narrow against and every one of those calls
scanned the entire `Properties` table.

`Property.PublishedStatus` fixes it: a string attribute, derived from `IsPublished`,
written only when the listing is published, and indexed by `PublishedStatus-index`.
DynamoDB GSIs are sparse, so unpublished listings never enter the index and a query
against it reads only rows it will actually return.

The catch is what "sparse" means for rows written before the attribute existed:
**they carry no marker, so they are not in the index.** A listing published last
month is invisible to the new query until it is written again.

### Read the failure mode carefully

This is not a case where enabling the change early makes things slower. Querying an
index that does not yet contain your data returns *zero rows*, successfully. The
homepage would render an empty state, no error would be logged, and nothing would
look broken from the outside.

The read path is therefore behind `Dynamo:UsePublishedIndex`, which ships **false**.
Until you flip it, every one of those queries still does the old scan — correct, and
no worse than before.

### Backfill needed

1. Create the `PublishedStatus-index` GSI on the `Properties` table if it does not
   already exist. `DynamoDbTableInitializer` will not do this for you: it only
   creates tables that are entirely absent, so an index added to an existing table
   has to be created directly. Adding a GSI is an online operation — the table stays
   readable and writable while it backfills.

2. Re-save every property whose `IsPublished` is true. A read-then-write with no
   changes is enough; `PublishedStatus` is derived on serialization, so the marker is
   written automatically. Unpublished rows need nothing.

3. Confirm the counts line up before switching over:

   ```
   published rows in the table  ==  item count of PublishedStatus-index
   ```

   GSI item counts are updated roughly every six hours, so allow for lag rather than
   concluding the backfill failed.

4. Set `Dynamo:UsePublishedIndex` to `true` and deploy.

5. Load the homepage. If listings are there, it worked. If it is empty, set the flag
   back to false — that is a complete rollback, since nothing else depends on the
   index.

### Note on the two APIs

Both read the same table, but only the consumer API queries published listings, so
only its flag matters for step 4. The admin API filters on `IsPublished` in memory
after loading, which is unchanged.
