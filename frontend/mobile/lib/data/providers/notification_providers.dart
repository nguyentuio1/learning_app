import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/network_providers.dart';
import '../models/notification_model.dart';
import '../models/paged_result.dart';
import '../services/notification_api_service.dart';

final notificationApiServiceProvider = Provider<NotificationApiService>(
  (ref) => NotificationApiService(ref.watch(dioProvider)),
  name: 'notificationApiService',
);

final unreadNotificationsCountProvider = FutureProvider.autoDispose<int>(
  (ref) => ref.watch(notificationApiServiceProvider).getUnreadCount(),
  name: 'unreadNotificationsCount',
);
