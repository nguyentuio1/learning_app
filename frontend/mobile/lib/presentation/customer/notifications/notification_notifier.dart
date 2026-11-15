import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/models/notification_model.dart';
import '../../../data/providers/notification_providers.dart';

class NotificationsFilterNotifier extends Notifier<bool> {
  @override
  bool build() => false;

  void setOnlyUnread(bool value) => state = value;
}

final notificationsFilterProvider =
    NotifierProvider.autoDispose<NotificationsFilterNotifier, bool>(
  NotificationsFilterNotifier.new,
  name: 'notificationsFilter',
);

class CustomerNotificationsState {
  const CustomerNotificationsState({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
    this.isLoadingMore = false,
  });

  final List<NotificationModel> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;
  final bool isLoadingMore;

  bool get hasMore => page < totalPages;

  CustomerNotificationsState copyWith({
    List<NotificationModel>? items,
    int? page,
    int? pageSize,
    int? totalCount,
    int? totalPages,
    bool? isLoadingMore,
  }) {
    return CustomerNotificationsState(
      items: items ?? this.items,
      page: page ?? this.page,
      pageSize: pageSize ?? this.pageSize,
      totalCount: totalCount ?? this.totalCount,
      totalPages: totalPages ?? this.totalPages,
      isLoadingMore: isLoadingMore ?? this.isLoadingMore,
    );
  }
}

class CustomerNotificationsNotifier
    extends AsyncNotifier<CustomerNotificationsState> {
  static const int _defaultPageSize = 50;
  int _generation = 0;

  @override
  FutureOr<CustomerNotificationsState> build() async {
    final generation = ++_generation;
    ref.onDispose(() {
      _generation++;
    });

    final onlyUnread = ref.watch(notificationsFilterProvider);
    final api = ref.watch(notificationApiServiceProvider);
    final result = await api.getNotifications(
      isRead: onlyUnread ? false : null,
      page: 1,
      pageSize: _defaultPageSize,
    );
    if (!ref.mounted || generation != _generation) {
      return CustomerNotificationsState(
        items: const [],
        page: 1,
        pageSize: _defaultPageSize,
        totalCount: 0,
        totalPages: 0,
      );
    }
    return CustomerNotificationsState(
      items: result.items,
      page: result.page,
      pageSize: result.pageSize,
      totalCount: result.totalCount,
      totalPages: result.totalPages,
    );
  }

  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore || current.isLoadingMore) {
      return;
    }

    final generation = _generation;
    state = AsyncData(current.copyWith(isLoadingMore: true));
    try {
      final onlyUnread = ref.read(notificationsFilterProvider);
      final api = ref.read(notificationApiServiceProvider);
      final next = await api.getNotifications(
        isRead: onlyUnread ? false : null,
        page: current.page + 1,
        pageSize: current.pageSize,
      );
      if (!ref.mounted || generation != _generation) {
        return;
      }
      final existingIds = current.items.map((e) => e.id).toSet();
      final merged = [
        ...current.items,
        ...next.items.where((item) => !existingIds.contains(item.id)),
      ];
      state = AsyncData(
        CustomerNotificationsState(
          items: List.unmodifiable(merged),
          page: next.page,
          pageSize: next.pageSize,
          totalCount: next.totalCount,
          totalPages: next.totalPages,
          isLoadingMore: false,
        ),
      );
    } catch (e, st) {
      if (!ref.mounted || generation != _generation) {
        return;
      }
      state = AsyncData(current.copyWith(isLoadingMore: false));
      Error.throwWithStackTrace(e, st);
    }
  }
}

final customerNotificationsProvider = AsyncNotifierProvider.autoDispose<
    CustomerNotificationsNotifier, CustomerNotificationsState>(
  CustomerNotificationsNotifier.new,
  name: 'customerNotifications',
);

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
