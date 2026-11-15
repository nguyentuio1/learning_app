import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/data/models/failure_models.dart';
import 'package:smart_drone_delivery/data/models/paged_result.dart';
import 'package:smart_drone_delivery/data/providers/failure_providers.dart';
import 'package:smart_drone_delivery/data/services/failure_api_service.dart';
import 'package:smart_drone_delivery/presentation/operator/failures/failed_orders_page.dart';

class _FakeFailureApiService extends FailureApiService {
  _FakeFailureApiService({this.orders = const []}) : super(Dio());

  final List<FailedOrderSummary> orders;
  final List<FailureOrderQuery> queries = [];

  @override
  Future<PagedResult<FailedOrderSummary>> getFailedOrders(
    FailureOrderQuery query,
  ) async {
    queries.add(query);
    return PagedResult(
      items: orders,
      page: query.page,
      pageSize: query.pageSize,
      totalCount: orders.length,
      totalPages: orders.isEmpty ? 0 : 1,
    );
  }
}

FailedOrderSummary _order(String id, String status) => FailedOrderSummary(
  orderId: id,
  trackingNumber: 'TRACK-$id',
  status: status,
  packageName: 'Parcel',
  originStation: null,
  destinationStation: null,
  failureLogId: 1,
  failureType: 'HARDWARE_FAULT',
  failureReason: 'Test failure',
  failedAt: DateTime.utc(2026, 11, 10),
  resolutionStatus: 'UNRESOLVED',
);

Future<ProviderContainer> _pumpPage(
  WidgetTester tester, {
  _FakeFailureApiService? service,
  FailureOrderQuery initialQuery = const FailureOrderQuery(),
}) async {
  tester.view.physicalSize = const Size(1000, 2000);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  final api = service ?? _FakeFailureApiService();
  final container = ProviderContainer(
    overrides: [failureApiServiceProvider.overrideWithValue(api)],
  );
  container.listen(failureOrderQueryProvider, (_, _) {});
  container
      .read(failureOrderQueryProvider.notifier)
      .update(
        page: initialQuery.page,
        pageSize: initialQuery.pageSize,
        failureType: initialQuery.failureType,
        resolutionStatus: initialQuery.resolutionStatus,
        searchKeyword: initialQuery.searchKeyword,
        fromDate: initialQuery.fromDate,
        toDate: initialQuery.toDate,
        clearFailureType: initialQuery.failureType == null,
        clearResolutionStatus: initialQuery.resolutionStatus == null,
        clearSearchKeyword: initialQuery.searchKeyword == null,
        clearFromDate: initialQuery.fromDate == null,
        clearToDate: initialQuery.toDate == null,
      );
  addTearDown(container.dispose);

  await tester.pumpWidget(
    UncontrolledProviderScope(
      container: container,
      child: const MaterialApp(home: FailedOrdersPage()),
    ),
  );
  await tester.pumpAndSettle();
  return container;
}

void main() {
  testWidgets(
    'date picker serializes local boundaries to UTC and clears them',
    (tester) async {
      final service = _FakeFailureApiService();
      final container = await _pumpPage(tester, service: service);

      await tester.tap(find.text('Date range'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('2').first);
      await tester.pumpAndSettle();
      await tester.tap(find.text('3').first);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();

      var query = container.read(failureOrderQueryProvider);
      expect(query.fromDate, isNotNull);
      expect(query.toDate, isNotNull);
      expect(query.fromDate, endsWith('Z'));
      expect(query.toDate, endsWith('Z'));
      final start = DateTime.parse(query.fromDate!);
      final end = DateTime.parse(query.toDate!);
      expect(start.toLocal().hour, 0);
      expect(start.toLocal().minute, 0);
      expect(end.toLocal().hour, 23);
      expect(end.toLocal().minute, 59);
      expect(end.toLocal().second, 59);
      expect(end.toLocal().microsecond, 999);

      await tester.tap(find.byKey(const Key('clear-date-range')));
      await tester.pumpAndSettle();
      query = container.read(failureOrderQueryProvider);
      expect(query.fromDate, isNull);
      expect(query.toDate, isNull);
    },
  );

  testWidgets('initial and explicitly selected all statuses send ALL', (
    tester,
  ) async {
    final service = _FakeFailureApiService();
    await _pumpPage(tester, service: service);

    expect(service.queries.first.resolutionStatus, 'ALL');
    final dropdown = find.byType(DropdownButtonFormField<String>).at(1);
    expect(
      tester.widget<DropdownButtonFormField<String>>(dropdown).initialValue,
      'ALL',
    );
    await tester.tap(dropdown);
    await tester.pumpAndSettle();
    await tester.tap(find.text('All statuses').last);
    await tester.pumpAndSettle();

    expect(service.queries.last.resolutionStatus, 'ALL');
  });

  testWidgets('rows show actual order status separately from failure badges', (
    tester,
  ) async {
    await _pumpPage(
      tester,
      service: _FakeFailureApiService(
        orders: [_order('1', 'APPROVED'), _order('2', 'CANCELLED')],
      ),
    );

    expect(find.text('APPROVED'), findsOneWidget);
    expect(find.text('CANCELLED'), findsOneWidget);
    expect(find.text('HARDWARE_FAULT'), findsNWidgets(2));
    expect(find.text('UNRESOLVED'), findsNWidgets(2));
    expect(find.text('FAILED'), findsNothing);
  });

  testWidgets('failure type menu exposes all read-side enum options', (
    tester,
  ) async {
    final service = _FakeFailureApiService();
    final container = await _pumpPage(tester, service: service);
    final dropdown = find.byType(DropdownButtonFormField<String>).first;
    const options = [
      ('MISSION_ABORTED', 'Mission Aborted'),
      ('EMERGENCY_LANDING', 'Emergency Landing'),
      ('HARDWARE_FAULT', 'Hardware Fault'),
      ('COLLECTION_TIMEOUT', 'Collection Timeout'),
      ('UNKNOWN', 'Unclassified Failure'),
    ];

    for (final (enumValue, label) in options) {
      await tester.tap(dropdown);
      await tester.pumpAndSettle();
      expect(find.text(label), findsOneWidget);
      await tester.tap(find.text(label).last);
      await tester.pumpAndSettle();
      expect(container.read(failureOrderQueryProvider).failureType, enumValue);
      expect(service.queries.last.failureType, enumValue);
    }
  });

  testWidgets('empty state offers Reset Filters', (tester) async {
    final container = await _pumpPage(
      tester,
      initialQuery: FailureOrderQuery(
        failureType: 'UNKNOWN',
        resolutionStatus: 'RESOLVED',
        fromDate: '2026-11-01T00:00:00.000Z',
        toDate: '2026-11-02T23:59:59.999Z',
      ),
    );
    expect(find.text('No failed deliveries found.'), findsOneWidget);
    await tester.tap(find.byKey(const Key('reset-failure-filters')));
    await tester.pumpAndSettle();
    final query = container.read(failureOrderQueryProvider);
    expect(query.failureType, isNull);
    expect(query.resolutionStatus, 'ALL');
    expect(query.fromDate, isNull);
    expect(query.toDate, isNull);
  });
}
