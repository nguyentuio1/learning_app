import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/data/models/failure_models.dart';
import 'package:smart_drone_delivery/data/providers/failure_providers.dart';
import 'package:smart_drone_delivery/data/services/failure_api_service.dart';
import 'package:smart_drone_delivery/presentation/operator/failures/mark_failed_sheet.dart';

class _FakeFailureApiService extends FailureApiService {
  _FakeFailureApiService() : super(Dio());

  String? orderId;
  String? failureType;
  String? reason;

  @override
  Future<OrderDecision> reportFailure(
    String orderId,
    String failureType,
    String reason,
  ) async {
    this.orderId = orderId;
    this.failureType = failureType;
    this.reason = reason;
    return OrderDecision(
      orderId: orderId,
      trackingNumber: 'TRACK-1',
      status: 'FAILED',
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
            body: TextButton(
              onPressed: () => showModalBottomSheet<bool>(
                context: context,
                isScrollControlled: true,
                builder: (_) => const MarkFailedSheet(),
              ),
              child: const Text('Open sheet'),
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
  testWidgets('requires order ID and reason', (tester) async {
    final service = _FakeFailureApiService();
    await _openSheet(tester, service);
    await tester.tap(find.byKey(const Key('mark-failure-submit')));
    await tester.pumpAndSettle();

    expect(find.text('Enter an order ID.'), findsOneWidget);
    expect(find.text('Enter a failure reason.'), findsOneWidget);
    expect(service.orderId, isNull);
  });

  testWidgets('submits the selected failure type and trimmed fields', (
    tester,
  ) async {
    final service = _FakeFailureApiService();
    await _openSheet(tester, service);
    await tester.enterText(find.byType(TextFormField).at(0), '  order-1  ');
    await tester.enterText(
      find.byType(TextFormField).at(1),
      '  Emergency landing  ',
    );
    await tester.tap(find.byKey(const Key('mark-failure-submit')));
    await tester.pumpAndSettle();

    expect(service.orderId, 'order-1');
    expect(service.failureType, 'MISSION_ABORTED');
    expect(service.reason, 'Emergency landing');
    expect(find.text('Report delivery failure'), findsNothing);
  });
}
