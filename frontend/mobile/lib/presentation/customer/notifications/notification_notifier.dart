import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/models/notification_model.dart';
import '../../../data/models/paged_result.dart';
import '../../../data/providers/notification_providers.dart';

final notificationsFilterProvider = StateProvider.autoDispose<bool>((ref) => false);

final customerNotificationsProvider =
    FutureProvider.autoDispose<PagedResult<NotificationModel>>((ref) async {
  final onlyUnread = ref.watch(notificationsFilterProvider);
  final api = ref.watch(notificationApiServiceProvider);
  return api.getNotifications(
    isRead: onlyUnread ? false : null,
    page: 1,
    pageSize: 50,
  );
});

class NotificationActionsNotifier extends AsyncNotifier<void> {
  @override
  FutureOr<void> build() {}

  Future<void> markAsRead(String id) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      await ref.read(notificationApiServiceProvider).markAsRead(id);
      ref.invalidate(customerNotificationsProvider);
      ref.invalidate(unreadNotificationsCountProvider);
    });
  }

  Future<void> markAllAsRead() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      await ref.read(notificationApiServiceProvider).markAllAsRead();
      ref.invalidate(customerNotificationsProvider);
      ref.invalidate(unreadNotificationsCountProvider);
    });
  }
}

final notificationActionsNotifierProvider =
    AsyncNotifierProvider<NotificationActionsNotifier, void>(
  NotificationActionsNotifier.new,
  name: 'notificationActionsNotifier',
);
