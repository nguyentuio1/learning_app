import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/data/models/notification_model.dart';
import 'package:smart_drone_delivery/data/models/paged_result.dart';
import 'package:smart_drone_delivery/data/providers/notification_providers.dart';
import 'package:smart_drone_delivery/data/services/notification_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/notifications/notifications_list_page.dart';

class _FakeNotificationApiService extends NotificationApiService {
  _FakeNotificationApiService({List<NotificationModel>? initial})
    : items = initial ?? [],
      super(Dio());

  final List<NotificationModel> items;
  int markAsReadCalls = 0;
  int markAllAsReadCalls = 0;

  @override
  Future<PagedResult<NotificationModel>> getNotifications({
    bool? isRead,
    int page = 1,
    int pageSize = 20,
  }) async {
    final filtered = isRead != null
        ? items.where((n) => n.isRead == isRead).toList()
        : items;
    final totalCount = filtered.length;
    final totalPages = totalCount == 0 ? 0 : (totalCount / pageSize).ceil();
    final start = (page - 1) * pageSize;
    final paged = start >= totalCount
        ? <NotificationModel>[]
        : filtered.skip(start).take(pageSize).toList();
    return PagedResult<NotificationModel>(
      items: paged,
      page: page,
      pageSize: pageSize,
      totalCount: totalCount,
      totalPages: totalPages,
    );
  }

  @override
  Future<int> getUnreadCount() async {
    return items.where((n) => !n.isRead).length;
  }

  @override
  Future<NotificationModel> markAsRead(String id) async {
    markAsReadCalls++;
    final index = items.indexWhere((n) => n.id == id);
    if (index >= 0) {
      final updated = items[index].copyWith(
        isRead: true,
        readAt: DateTime.now(),
      );
      items[index] = updated;
      return updated;
    }
    throw Exception('Notification not found');
  }

  @override
  Future<int> markAllAsRead() async {
    markAllAsReadCalls++;
    int count = 0;
    for (int i = 0; i < items.length; i++) {
      if (!items[i].isRead) {
        items[i] = items[i].copyWith(isRead: true, readAt: DateTime.now());
        count++;
      }
    }
    return count;
  }
}

void main() {
  testWidgets('renders notifications list and allows marking as read', (
    tester,
  ) async {
    final fakeApi = _FakeNotificationApiService(
      initial: [
        NotificationModel(
          id: 'n-1',
          orderId: 'o-101',
          type: 'ORDER_STATUS_CHANGED',
          title: 'Order Approved',
          message: 'Your order was approved',
          isRead: false,
          createdAt: DateTime.now(),
        ),
        NotificationModel(
          id: 'n-2',
          orderId: null,
          type: 'ORDER_STATUS_CHANGED',
          title: 'Delivery in transit',
          message: 'Your delivery is on its way',
          isRead: true,
          createdAt: DateTime.now(),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: [notificationApiServiceProvider.overrideWithValue(fakeApi)],
        child: const MaterialApp(home: NotificationsListPage()),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Notifications'), findsOneWidget);
    expect(find.text('Order Approved'), findsOneWidget);
    expect(find.text('Delivery in transit'), findsOneWidget);
    expect(find.text('Mark as read'), findsOneWidget);
    expect(find.text('View order'), findsOneWidget);

    // Tap Mark as read
    await tester.tap(find.text('Mark as read'));
    await tester.pumpAndSettle();

    expect(fakeApi.markAsReadCalls, 1);
  });

  testWidgets('mark all as read triggers markAllAsRead on service', (
    tester,
  ) async {
    final fakeApi = _FakeNotificationApiService(
      initial: [
        NotificationModel(
          id: 'n-1',
          type: 'ORDER_STATUS_CHANGED',
          title: 'Unread 1',
          message: 'Message 1',
          isRead: false,
          createdAt: DateTime.now(),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: [notificationApiServiceProvider.overrideWithValue(fakeApi)],
        child: const MaterialApp(home: NotificationsListPage()),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Mark all read'), findsOneWidget);
    await tester.tap(find.text('Mark all read'));
    await tester.pumpAndSettle();

    expect(fakeApi.markAllAsReadCalls, 1);
  });

  testWidgets('loads later pages beyond 50 items and marks item 51 as read', (
    tester,
  ) async {
    final fakeApi = _FakeNotificationApiService(
      initial: List.generate(
        51,
        (i) => NotificationModel(
          id: 'n-${i + 1}',
          type: 'ORDER_STATUS_CHANGED',
          title: 'Notification ${i + 1}',
          message: 'Message ${i + 1}',
          isRead: i < 50,
          createdAt: DateTime.now().subtract(Duration(minutes: i)),
        ),
      ),
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: [notificationApiServiceProvider.overrideWithValue(fakeApi)],
        child: const MaterialApp(home: NotificationsListPage()),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Notification 51'), findsNothing);

    await tester.scrollUntilVisible(
      find.text('Load more'),
      500,
      scrollable: find.byType(Scrollable).first,
    );
    await tester.ensureVisible(find.text('Load more'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Load more'));
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('Notification 51'),
      500,
      scrollable: find.byType(Scrollable).first,
    );
    await tester.ensureVisible(find.text('Notification 51'));
    await tester.pumpAndSettle();
    expect(find.text('Notification 51'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('Mark as read'),
      200,
      scrollable: find.byType(Scrollable).first,
    );
    await tester.ensureVisible(find.text('Mark as read'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Mark as read'));
    await tester.pumpAndSettle();

    expect(fakeApi.markAsReadCalls, 1);
    expect(fakeApi.items[50].isRead, isTrue);
  });

  testWidgets(
    'unread badge provider refreshes on app resume and 60s interval',
    (tester) async {
      final fakeApi = _FakeNotificationApiService(
        initial: [
          NotificationModel(
            id: 'n-1',
            type: 'ORDER_STATUS_CHANGED',
            title: 'Initial',
            message: 'Msg',
            isRead: false,
            createdAt: DateTime.now(),
          ),
        ],
      );

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            notificationApiServiceProvider.overrideWithValue(fakeApi),
          ],
          child: MaterialApp(
            home: Consumer(
              builder: (context, ref, _) {
                final count = ref.watch(unreadNotificationsCountProvider);
                return Text('unread:${count.value ?? -1}');
              },
            ),
          ),
        ),
      );

      await tester.pumpAndSettle();
      expect(find.text('unread:1'), findsOneWidget);

      // Simulate a new notification arriving while app is in background, then resuming
      fakeApi.items.add(
        NotificationModel(
          id: 'n-2',
          type: 'ORDER_STATUS_CHANGED',
          title: 'Second',
          message: 'Msg 2',
          isRead: false,
          createdAt: DateTime.now(),
        ),
      );
      tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.paused);
      tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.resumed);
      await tester.pumpAndSettle();
      expect(find.text('unread:2'), findsOneWidget);

      // Simulate a third notification and advance 60 seconds
      fakeApi.items.add(
        NotificationModel(
          id: 'n-3',
          type: 'ORDER_STATUS_CHANGED',
          title: 'Third',
          message: 'Msg 3',
          isRead: false,
          createdAt: DateTime.now(),
        ),
      );
      await tester.pump(const Duration(seconds: 60));
      await tester.pumpAndSettle();
      expect(find.text('unread:3'), findsOneWidget);

      await tester.pumpWidget(const SizedBox.shrink());
      await tester.pump();
    },
  );

  testWidgets(
    'ignores stale loadMore response when filter changes while page 2 is in flight',
    (tester) async {
      final page2Completer = Completer<PagedResult<NotificationModel>>();
      final items = List.generate(
        51,
        (i) => NotificationModel(
          id: 'n-${i + 1}',
          type: 'ORDER_STATUS_CHANGED',
          title: i == 50 ? 'Unread Item 51' : 'Read Item ${i + 1}',
          message: 'Message ${i + 1}',
          isRead: i < 50,
          createdAt: DateTime.now().subtract(Duration(minutes: i)),
        ),
      );

      final fakeApi = _DelayedPage2NotificationApiService(
        initial: items,
        page2AllCompleter: page2Completer,
      );

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            notificationApiServiceProvider.overrideWithValue(fakeApi),
          ],
          child: const MaterialApp(home: NotificationsListPage()),
        ),
      );

      await tester.pumpAndSettle();

      // Scroll to Load more on All tab and trigger page 2 (which stays pending)
      await tester.scrollUntilVisible(
        find.text('Load more'),
        500,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.ensureVisible(find.text('Load more'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Load more'));
      await tester.pump();

      // Switch to Unread filter while All page 2 is still in flight
      await tester.tap(find.text('Unread'));
      await tester.pumpAndSettle();
      expect(find.text('Unread Item 51'), findsOneWidget);
      expect(find.text('Read Item 1'), findsNothing);

      // Complete the stale All page 2 request late
      page2Completer.complete(
        PagedResult<NotificationModel>(
          items: [items[50]],
          page: 2,
          pageSize: 50,
          totalCount: 51,
          totalPages: 2,
        ),
      );
      await tester.pumpAndSettle();

      // Unread list must remain intact and not be overwritten by stale All data
      expect(find.text('Unread Item 51'), findsOneWidget);
      expect(find.text('Read Item 1'), findsNothing);
    },
  );
}

class _DelayedPage2NotificationApiService extends _FakeNotificationApiService {
  _DelayedPage2NotificationApiService({
    required super.initial,
    required this.page2AllCompleter,
  });

  final Completer<PagedResult<NotificationModel>> page2AllCompleter;

  @override
  Future<PagedResult<NotificationModel>> getNotifications({
    bool? isRead,
    int page = 1,
    int pageSize = 20,
  }) {
    if (isRead == null && page == 2) {
      return page2AllCompleter.future;
    }
    return super.getNotifications(
      isRead: isRead,
      page: page,
      pageSize: pageSize,
    );
  }
}
