# S3: making the private prefix actually private

Bucket: `housinghub-files-dev` (region `af-south-1`)

KYC identity documents now upload to `private/kyc/{customerId}/…` and are served
through short-lived presigned URLs. **None of that helps until the bucket policy
stops serving that prefix to anonymous callers.** This is how.

---

## Key layout

| Prefix | Contents | Anonymous read |
|---|---|---|
| `properties/{propertyId}/…` | Listing photos and videos | **Yes** — rendered in `<img>` on public pages |
| `profile-photos/{customerId}/…` | Profile pictures | **Yes** |
| `private/kyc/{customerId}/…` | Government ID documents | **No** |
| `kyc/{customerId}/…` | *Legacy* KYC documents, pre-migration | **No** — see §4 |

---

## 1. Do not use an explicit Deny

The obvious move is adding a `Deny` on `private/*`. Don't.

A statement with `"Effect": "Deny"` and `"Principal": "*"` matches **every**
principal, including the IAM role your API runs as. An explicit Deny always beats
an Allow, and a presigned URL is just a signed request made as that role — so the
Deny would apply to it too, and admin KYC review would break with a 403 that looks
like a bug rather than a policy.

Instead, **narrow the public Allow**. S3 is deny-by-default: anything not
explicitly allowed is already refused for anonymous callers, and your API keeps
access through its own IAM role permissions, which the bucket policy does not
restrict.

---

## 2. Check what's there now

```bash
aws s3api get-bucket-policy \
  --bucket housinghub-files-dev \
  --region af-south-1 \
  --query Policy --output text | python3 -m json.tool
```

You almost certainly have something like this — note the `/*`, which is the problem:

```json
{
  "Effect": "Allow",
  "Principal": "*",
  "Action": "s3:GetObject",
  "Resource": "arn:aws:s3:::housinghub-files-dev/*"
}
```

**Save the output before you change anything**, so you can roll back.

---

## 3. Apply the narrowed policy

```bash
cat > /tmp/bucket-policy.json <<'JSON'
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "PublicReadListingMedia",
      "Effect": "Allow",
      "Principal": "*",
      "Action": "s3:GetObject",
      "Resource": [
        "arn:aws:s3:::housinghub-files-dev/properties/*",
        "arn:aws:s3:::housinghub-files-dev/profile-photos/*"
      ]
    },
    {
      "Sid": "DenyInsecureTransport",
      "Effect": "Deny",
      "Principal": "*",
      "Action": "s3:*",
      "Resource": [
        "arn:aws:s3:::housinghub-files-dev",
        "arn:aws:s3:::housinghub-files-dev/*"
      ],
      "Condition": { "Bool": { "aws:SecureTransport": "false" } }
    }
  ]
}
JSON

aws s3api put-bucket-policy \
  --bucket housinghub-files-dev \
  --region af-south-1 \
  --policy file:///tmp/bucket-policy.json
```

Everything outside those two prefixes — `private/*` and legacy `kyc/*` — is now
anonymous-inaccessible. The API is unaffected: it reads and writes via its IAM
role, and presigning continues to work.

The second statement is unrelated but free: it refuses any plaintext HTTP request
to the bucket.

---

## 4. This immediately protects legacy KYC documents — and breaks viewing them

Documents uploaded before the private-prefix change sit under `kyc/…` with a public
URL stored in `Customer.IdDocumentUrl`. Narrowing the policy makes those
**inaccessible to anonymous callers straight away**, which is the correct outcome —
they are government IDs that have been world-readable.

The side effect is that the admin "legacy document" passthrough will now 403,
because it hands back the stored public URL rather than presigning.

**Apply the policy anyway, then migrate.** Leaving IDs publicly readable so that
review keeps working is the wrong trade. Migration steps are in
`data-backfill-required.md` §2; in short, copy each object to
`private/kyc/{customerId}/…`, update `IdDocumentUrl` to the key, delete the
original. Once migrated, the normal presigned path takes over and review works
again.

---

## 5. Block Public Access settings

If "Block all public access" is on, the `properties/*` Allow will not take effect
and listing photos break. Check:

```bash
aws s3api get-public-access-block \
  --bucket housinghub-files-dev --region af-south-1
```

You need `BlockPublicPolicy` and `RestrictPublicBuckets` set to `false` for the
public prefixes to serve. Keep `BlockPublicAcls` and `IgnorePublicAcls` **true** —
those govern per-object ACLs, which nothing in the code sets, and leaving them on
prevents an object being made public by accident.

```bash
aws s3api put-public-access-block \
  --bucket housinghub-files-dev --region af-south-1 \
  --public-access-block-configuration \
    BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=false,RestrictPublicBuckets=false
```

---

## 6. Verify

```bash
BUCKET=housinghub-files-dev
REGION=af-south-1

# Put a probe object in the private prefix
echo "probe" > /tmp/probe.txt
aws s3 cp /tmp/probe.txt s3://$BUCKET/private/kyc/probe.txt --region $REGION

# Must be 403
curl -s -o /dev/null -w "private prefix anonymous: %{http_code}\n" \
  "https://$BUCKET.s3.$REGION.amazonaws.com/private/kyc/probe.txt"

# Must be 200 — proves presigning still works for the app
URL=$(aws s3 presign s3://$BUCKET/private/kyc/probe.txt --expires-in 60 --region $REGION)
curl -s -o /dev/null -w "private prefix presigned:  %{http_code}\n" "$URL"

aws s3 rm s3://$BUCKET/private/kyc/probe.txt --region $REGION
```

Expected:

```
private prefix anonymous: 403
private prefix presigned:  200
```

Then confirm you have not broken the public path — open any listing with a photo
and check the image loads, or:

```bash
curl -s -o /dev/null -w "listing photo: %{http_code}\n" \
  "https://$BUCKET.s3.$REGION.amazonaws.com/properties/<some-real-key>.jpg"
```

That must be `200`. If it is `403`, re-check §5.

---

## 7. Production bucket

`housinghub-files-dev` is the name in every appsettings file, including the
non-Development ones — so development and production may be sharing a bucket. If
there is a separate production bucket, apply all of the above to it too. If there
isn't, splitting them is worth doing: a dev deploy currently writes into the same
place production reads from.

---

## Rollback

```bash
aws s3api put-bucket-policy \
  --bucket housinghub-files-dev --region af-south-1 \
  --policy file:///path/to/the-policy-you-saved-in-step-2.json
```
