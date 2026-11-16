import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/network_providers.dart';
import '../services/ai_assistant_api_service.dart';

final aiAssistantApiServiceProvider = Provider<AiAssistantApiService>(
  (ref) => AiAssistantApiService(ref.watch(dioProvider)),
  name: 'aiAssistantApiService',
);
