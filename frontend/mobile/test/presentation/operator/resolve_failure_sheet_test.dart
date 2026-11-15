import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/data/models/failure_models.dart';
import 'package:smart_drone_delivery/data/providers/failure_providers.dart';
import 'package:smart_drone_delivery/data/services/failure_api_service.dart';
import 'package:smart_drone_delivery/presentation/operator/failures/resolve_failure_sheet.dart';

final _detail = FailureOrderDetail(
  orderId: 'order-1',
  trackingNumber: 'TRACK-1',
  status: 'FAILED',
  latestFailure: FailureEpisode(
    failureLogId: 1,
    failureType: 'HARDWARE_FAULT',
    failureReason: 'Motor warning',
    failedAt: DateTime.utc(2026, 11, 10),
    resolutionStatus: 'UNRESOLVED',
  ),
  returnToOriginStatus: 'NOT_REQUESTED',
  failureHistory: [],
  resolutionHistory: [],
  resolutionHistoryComplete: true,
  statusLogs: [],
  parcelLocation: ParcelLocation(stationId: 'station-1', slotId: 'slot-1'),
);

class _FakeFailureApiService extends FailureApiService {
  _FakeFailureApiService({this.pendingResolution}) : super(Dio());

  final Completer<OrderDecision>? pendingResolution;
  FailureResolutionRequest? request;

  @override
  Future<OrderDecision> resolveFailure(
    String orderId,
    FailureResolutionRequest request,
  ) async {
    this.request = request;
    if (pendingResolution != null) return pendingResolution!.future;
    return OrderDecision(
      orderId: 'order-1',
      trackingNumber: 'TRACK-1',
      status: 'APPROVED',
      updatedAt: DateTime.utc(2026, 11, 10),
    );
  }
}

Future<void> _openSheet(
  WidgetTester tester,
  _FakeFailureApiService service,
) async {
  tester.view.physicalSize = const Size(800, 1800);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [failureApiServiceProvider.overrideWithValue(service)],
      child: MaterialApp(
        home: Builder(
          builder: (context) => Scaffold(
            body: Center(
              child: TextButton(
                onPressed: () => showModalBottomSheet<String>(
                  context: context,
                  isScrollControlled: true,
                  builder: (_) => ResolveFailureSheet(detail: _detail),
                ),
                child: const Text('Open sheet'),
              ),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.text('Open sheet'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('validates required note inline without sending request', (
    tester,
  ) async {
    final service = _FakeFailureApiService();
    await _openSheet(tester, service);
    await tester.tap(find.text('Hủy đơn hàng'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('resolve-failure-submit')));
    await tester.pumpAndSettle();

    expect(find.text('Enter a resolution note.'), findsOneWidget);
    expect(service.request, isNull);
  });

  testWidgets('rejects notes longer than 500 characters inline', (
    tester,
  ) async {
    final service = _FakeFailureApiService();
    await _openSheet(tester, service);
    await tester.tap(find.text('Hủy đơn hàng'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextFormField), 'n' * 501);
    await tester.tap(find.byKey(const Key('resolve-failure-submit')));
    await tester.pumpAndSettle();

    expect(
      find.text('Resolution note must be at most 500 characters.'),
      findsOneWidget,
    );
    expect(service.request, isNull);
  });

  testWidgets('requires package verification only when rescheduling', (
    tester,
  ) async {
    final service = _FakeFailureApiService();
    await _openSheet(tester, service);
    await tester.enterText(find.byType(TextFormField), 'Ready to retry');
    await tester.tap(find.byKey(const Key('resolve-failure-submit')));
    await tester.pumpAndSettle();

    expect(
      find.text('Verify the package before rescheduling.'),
      findsOneWidget,
    );
    expect(service.request, isNull);

    await tester.tap(find.text('Hủy đơn hàng'));
    await tester.pumpAndSettle();
    expect(find.byType(CheckboxListTile), findsNothing);
  });

  testWidgets('submits the exact action and closes after success', (
    tester,
  ) async {
    final service = _FakeFailureApiService();
    await _openSheet(tester, service);
    await tester.tap(find.text('Hủy đơn hàng'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextFormField), '  Stop delivery  ');
    await tester.tap(find.byKey(const Key('resolve-failure-submit')));
    await tester.pumpAndSettle();

    expect(service.request?.action, 'CANCEL');
    expect(service.request?.note, 'Stop delivery');
    expect(service.request?.packageVerified, isFalse);
    expect(find.text('Resolve delivery failure'), findsNothing);
  });

  testWidgets(
    'disables submission and shows progress while request is pending',
    (tester) async {
      final pending = Completer<OrderDecision>();
      final service = _FakeFailureApiService(pendingResolution: pending);
      await _openSheet(tester, service);
      await tester.tap(find.text('Hủy đơn hàng'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextFormField), 'Stop delivery');
      await tester.tap(find.byKey(const Key('resolve-failure-submit')));
      await tester.pump();

      expect(
        tester
            .widget<FilledButton>(
              find.byKey(const Key('resolve-failure-submit')),
            )
            .onPressed,
        isNull,
      );
      expect(find.byType(CircularProgressIndicator), findsOneWidget);

      pending.complete(
        OrderDecision(
          orderId: 'order-1',
          trackingNumber: 'TRACK-1',
          status: 'CANCELLED',
          updatedAt: DateTime.utc(2026, 11, 10),
        ),
      );
      await tester.pumpAndSettle();
    },
  );
}
