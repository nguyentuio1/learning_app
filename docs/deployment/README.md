# Deployment and operations

| Task | Maintained guide |
|---|---|
| Configure and start Docker/backend/web, check health, troubleshoot | [Deployment guide](deployment-guide.md) |
| Back up and restore PostgreSQL, verify recovery | [Database backup and restore](database-backup-guide.md) |
| Select/configure Ollama or Gemini and understand activation gates | [AI provider configuration](gemini-ai.md) |
| Install a client or use the product | [Installation](../guides/installation-guide.md), [user manual](../guides/user-manual.md) |

This is the single home for deployment/backup instructions. The tracking-number allocator's technical design is under [architecture](../architecture/tracking-number-allocation.md).

Policies are separate from instructions: [cloud processing](../decisions/ai-cloud-processing.md) and [assistant retention](../decisions/assistant-retention-policy.md) remain proposals with their stated approval gates. A working key does not approve customer-cloud processing. AI generation defaults remain off.

Measured outcomes belong to [testing](../testing/README.md): [synthetic Gemini results](../testing/acceptance/gemini-synthetic-acceptance.md), [MinIO verification procedure](../testing/acceptance/minio-photo-verification.md) and [Flutter photo evidence](../testing/acceptance/flutter-photo-upload.md). Moving a report here or following a guide does not establish a deployed environment.
