import 'dart:async';

import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/network_providers.dart';
import '../services/notification_api_service.dart';

final notificationApiServiceProvider = Provider<NotificationApiService>(
  (ref) => NotificationApiService(ref.watch(dioProvider)),
  name: 'notificationApiService',
);

final unreadNotificationsCountProvider = FutureProvider.autoDispose<int>(
  (ref) {
    final timer = Timer.periodic(
      const Duration(seconds: 60),
      (_) => ref.invalidateSelf(),
    );
    final observer = _UnreadResumeObserver(() => ref.invalidateSelf());
    WidgetsBinding.instance.addObserver(observer);
    ref.onDispose(() {
      timer.cancel();
      WidgetsBinding.instance.removeObserver(observer);
    });
    return ref.watch(notificationApiServiceProvider).getUnreadCount();
  },
  name: 'unreadNotificationsCount',
);

class _UnreadResumeObserver extends WidgetsBindingObserver {
  _UnreadResumeObserver(this.onResumed);

  final VoidCallback onResumed;

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      onResumed();
    }
  }
}
