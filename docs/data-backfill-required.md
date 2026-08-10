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
