# MinIO photo verification (LA-346)

MinIO stores package photos and avatars. It is separate from the delivery basemap.
The application uses dedicated `packages` and `avatars` buckets (configurable via
`Minio:PackageBucket` and `Minio:AvatarBucket`). These image buckets permit public
object reads, not anonymous listing or writes; do not point them at buckets holding
private documents. Initialization reconciles that policy even when a dedicated
bucket already exists, including retry after interrupted provisioning.

## Client-reachable URLs

`Minio:Endpoint` is the server-to-storage address (`minio:9000` inside Compose).
`Minio:PublicBaseUrl`, or Compose `MINIO_PUBLIC_URL`, is the address embedded in
the returned photo URLs. It must be reachable from every intended client.

| Client | API address | Public MinIO address |
|---|---|---|
| Browser on workstation | `http://localhost:5055/api` | `http://localhost:9000` works locally |
| Android emulator only | `http://10.0.2.2:5055/api` | `http://10.0.2.2:9000` |
| Browser and emulator/phone together | workstation LAN IP, API port 5055 | same LAN IP, MinIO port 9000 |

Use the actual workstation IP. Permit access only on the intended development
network. Flutter `--dart-define=API_BASE_URL=http://<host>:5055/api` changes API
requests; it does not rewrite absolute image URLs. Re-upload a test photo after
changing PublicBaseUrl because existing database image URLs are preserved.
An HTTPS deployment needs HTTPS storage URLs to avoid browser mixed-content errors.

## Automated storage regression

Set `TEST_MINIO_ENDPOINT` (host:port), `TEST_MINIO_ACCESS_KEY` and
`TEST_MINIO_SECRET_KEY` in the test process environment, then run:

```text
dotnet test backend/SmartDroneDelivery.Tests/SmartDroneDelivery.Tests.csproj --filter FullyQualifiedName~MinioStorageIntegrationTests
```

For HTTPS also set `TEST_MINIO_SSL=true`. Each test creates a uniquely named,
private test bucket, uploads through the actual storage implementation and verifies
an unauthenticated image GET succeeds while bucket listing remains forbidden.
Only that test's object and bucket are removed. Tests skip when credentials are
absent; a skipped run is not storage acceptance. Never commit the test credentials.

## End-to-end acceptance

1. Start the API, database and MinIO with the intended client-reachable URLs.
2. Customer web: Packages -> Add package -> upload a PNG/JPEG; save, reopen and
   verify the photo. My profile -> upload avatar -> reload and sign in again.
3. Repeat on a Flutter runtime. Record whether it was Android, iOS, Windows or
   browser; browser/Windows evidence does not prove physical-phone networking.
4. Confirm each returned photo URL is HTTP 200 with the expected image bytes and
   that profile/package API reads retain the URL. Verify an unsupported upload is
   rejected without changing the persisted URL. Verify another customer cannot
   upload a replacement to the first customer's package.
5. Restart the API and repeat an upload to the existing buckets. Inspect the
   object in MinIO console (`http://localhost:9001` locally) using configured
   administrator credentials. Do not include secrets in evidence.
6. Record API/UI commit SHA, runtime, configuration shape (without secrets),
   screenshot/request results and pass/fail. Storage regression tests alone do
   not establish the complete UI or Docker/mobile acceptance.
