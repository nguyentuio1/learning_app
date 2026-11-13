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
    return PagedResult<NotificationModel>(
      items: filtered,
      page: page,
      pageSize: pageSize,
      totalCount: filtered.length,
      totalPages: filtered.isEmpty ? 0 : 1,
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
        items[i] = items[i].copyWith(
          isRead: true,
          readAt: DateTime.now(),
        );
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
        overrides: [
          notificationApiServiceProvider.overrideWithValue(fakeApi),
        ],
        child: const MaterialApp(
          home: NotificationsListPage(),
        ),
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
        overrides: [
          notificationApiServiceProvider.overrideWithValue(fakeApi),
        ],
        child: const MaterialApp(
          home: NotificationsListPage(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Mark all read'), findsOneWidget);
    await tester.tap(find.text('Mark all read'));
    await tester.pumpAndSettle();

    expect(fakeApi.markAllAsReadCalls, 1);
  });
}
