import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/data/models/failure_models.dart';
import 'package:smart_drone_delivery/data/providers/failure_providers.dart';
import 'package:smart_drone_delivery/data/services/failure_api_service.dart';
import 'package:smart_drone_delivery/presentation/operator/failures/failed_order_detail_page.dart';

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
);

class _FakeFailureApiService extends FailureApiService {
  _FakeFailureApiService() : super(Dio());

  @override
  Future<FailureOrderDetail> getFailureDetail(String orderId) async => _detail;

  @override
  Future<OrderDecision> resolveFailure(
    String orderId,
    FailureResolutionRequest request,
  ) async => OrderDecision(
    orderId: orderId,
    trackingNumber: 'TRACK-1',
    status: 'FAILED',
    updatedAt: DateTime.utc(2026, 11, 10),
  );
}

void main() {
  testWidgets('RETURN_TO_ORIGIN shows the MSG-P-DW-17 standard message', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(800, 1800);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          failureApiServiceProvider.overrideWithValue(_FakeFailureApiService()),
        ],
        child: const MaterialApp(
          home: FailedOrderDetailPage(orderId: 'order-1'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Resolve failure'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Hoàn hàng về trạm đi'));
    await tester.enterText(find.byType(TextFormField), 'Return to origin');
    await tester.tap(find.byKey(const Key('resolve-failure-submit')));
    await tester.pumpAndSettle();

    expect(find.byKey(const ValueKey('MSG-P-DW-17')), findsOneWidget);
    expect(
      find.text('Return requested. Waiting for receipt at the origin station.'),
      findsOneWidget,
    );
  });
}
