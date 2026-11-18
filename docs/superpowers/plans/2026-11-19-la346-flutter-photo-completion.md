# Flutter package photo completion

Goal: complete LA-346 on the existing authorized upload API without changing storage permissions.

Architecture: retain existing CRUD methods and add a typed create-record response for first-upload package identity. Flutter picks a photo, validates size, saves a new package once, uploads multipart bytes, and retains that identity for retry/finish. Failed replacement preserves the existing URL. Cards and edit screens display persisted network photos with an explicit loading-error state.

Tech: Flutter/Riverpod, existing image_picker, Dio, ASP.NET/MinIO.

- [ ] Red tests: missing upload action and typed multipart request; invalid size and failure mapping; failed replacement/retry must not duplicate a created package.
- [ ] Implement service methods, picker provider and form upload with progress/disabled controls; keep existing CRUD compatibility.
- [ ] Add photo display to list/edit and verify existing validation/ownership behavior.
- [ ] Run focused then full tests/analyzer; real Flutter browser package/avatar uploads, reload and stored object checks against isolated API/MinIO.
- [ ] Commit evidence, independent review, current CI, merge to develop and reconcile actual LA-346 scope. Native device installation remains separately identified if unavailable.
