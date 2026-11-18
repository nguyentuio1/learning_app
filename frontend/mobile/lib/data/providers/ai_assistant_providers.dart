import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/network_providers.dart';
import '../services/ai_assistant_api_service.dart';

/// Presentation capability only. Backend authorization and data policy still apply.
final aiAssistantChatEnabledProvider = Provider<bool>(
  (ref) => const bool.fromEnvironment('AI_CHAT_ENABLED', defaultValue: false),
  name: 'aiAssistantChatEnabled',
);

final aiAssistantApiServiceProvider = Provider<AiAssistantApiService>(
  (ref) => AiAssistantApiService(ref.watch(dioProvider)),
  name: 'aiAssistantApiService',
);
