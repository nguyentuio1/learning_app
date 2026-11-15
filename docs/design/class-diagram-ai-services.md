# Class Diagram - AI-assisted services

**Parent:** [LA-201](https://hungndemail.atlassian.net/browse/LA-201). **Task:** [LA-244](https://hungndemail.atlassian.net/browse/LA-244).

Existing persistence entities are marked `existing`. All controllers, service/gateway classes and transfer objects are proposed responsibilities for LA-158/162/165, not implemented classes or fixed HTTP DTOs. Only relevant existing entity fields are shown; omitted sensitive fields must not be added to AI context by loading complete entities.

```mermaid
classDiagram
    class User {
        <<existing>>
        Guid Id
        string Status
    }
    class DeliveryOrder {
        <<existing>>
        Guid Id
        Guid CustomerId
        string Status
        DateTime UpdatedAt
        DateTime EstimatedDeliveryTime
    }
    class AiEtaPrediction {
        <<existing>>
        Guid Id
        Guid OrderId
        decimal CalculatedDistanceKm
        int PredictedDurationMinutes
        string WeatherCondition
        decimal ConfidenceScore
        DateTime CreatedAt
    }
    class AiChatSession {
        <<existing>>
        Guid Id
        Guid UserId
        string SessionTitle
        DateTime CreatedAt
    }
    class AiChatMessage {
        <<existing>>
        long Id
        Guid SessionId
        string SenderType
        string MessageText
        DateTime CreatedAt
    }
    class EtaController {
        <<proposed>>
        ReadEstimate(caller, orderId)
    }
    class SummaryController {
        <<proposed>>
        Summarize(caller, orderId)
    }
    class CustomerAssistantController {
        <<proposed>>
        SendMessage(caller, request)
        ReadHistory(caller, sessionId, page)
    }
    class AuthorizedAiFactsReader {
        <<proposed>>
        ReadOrderScope(caller, orderId)
        ReadOwnedSession(caller, sessionId)
        ResolveOwnedReference(caller, reference)
        ReadMinimalSnapshot(scope)
        Revalidate(scope, inputStamp)
    }
    class EtaPredictionService {
        <<proposed>>
        Recalculate(trustedTrigger, orderId)
        ReadEstimate(scope)
    }
    class DeliverySummaryService {
        <<proposed>>
        Generate(scope)
    }
    class CustomerAssistantService {
        <<proposed>>
        Send(caller, request)
        ReadHistory(caller, sessionId, page)
    }
    class WeatherObservationSource {
        <<proposed>>
        ReadVerifiedObservation(features)
    }
    class EtaModelGateway {
        <<proposed>>
        Predict(EtaFeatures)
    }
    class TextGenerationGateway {
        <<proposed>>
        Summarize(SummaryContext)
        Answer(AssistantContext)
    }
    User "1" --> "0..*" DeliveryOrder : owns
    DeliveryOrder "1" --> "0..1" AiEtaPrediction : current prediction
    User "1" --> "0..*" AiChatSession : owns
    AiChatSession "1" --> "0..*" AiChatMessage : contains
    EtaController ..> AuthorizedAiFactsReader
    EtaController ..> EtaPredictionService
    SummaryController ..> DeliverySummaryService
    CustomerAssistantController ..> CustomerAssistantService
    EtaPredictionService ..> AuthorizedAiFactsReader
    EtaPredictionService ..> WeatherObservationSource
    EtaPredictionService ..> EtaModelGateway
    EtaPredictionService ..> AiEtaPrediction : atomic current upsert
    DeliverySummaryService ..> AuthorizedAiFactsReader
    DeliverySummaryService ..> TextGenerationGateway
    CustomerAssistantService ..> AuthorizedAiFactsReader
    CustomerAssistantService ..> TextGenerationGateway
    CustomerAssistantService ..> AiChatSession : owned persistence
    CustomerAssistantService ..> AiChatMessage : atomic message pair
```

Nullable fields and storage constraints are specified in [class-specification-ai-services.md](class-specification-ai-services.md); Mermaid's simplified property types above do not remove nullability. No summary entity, prediction-history relationship, model tool with operational write authority, or session-to-order FK is implied.

Sequences: [ETA / LA-241](sequence-ai-eta-estimation.md), [summary / LA-242](sequence-ai-delivery-summarization.md), [customer assistant / LA-243](sequence-ai-customer-assistant.md).
