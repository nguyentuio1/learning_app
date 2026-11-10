import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/data/providers/repository_providers.dart';
import 'package:smart_drone_delivery/domain/entities/resolution_action.dart';
import 'package:smart_drone_delivery/domain/repositories/delivery_order_repository.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/resolve_failed_delivery_sheet.dart';

class _FakeDeliveryOrderRepository implements DeliveryOrderRepository {
  String? orderId;
  ResolutionAction? action;
  String? note;

  @override
  Future<void> markFailed({
    required String id,
    required String failureReason,
    required String failureType,
  }) async {}

  @override
  Future<void> resolveFailedDelivery({
    required String id,
    required ResolutionAction resolutionAction,
    required String resolutionNote,
  }) async {
    orderId = id;
    action = resolutionAction;
    note = resolutionNote;
  }
}

class _SheetHost extends StatelessWidget {
  const _SheetHost();

  @override
  Widget build(BuildContext context) => Scaffold(
    body: Builder(
      builder: (context) => FilledButton(
        onPressed: () => showModalBottomSheet<void>(
          context: context,
          isScrollControlled: true,
          builder: (_) => const ResolveFailedDeliverySheet(orderId: 'order-1'),
        ),
        child: const Text('Open resolution'),
      ),
    ),
  );
}

void main() {
  testWidgets('validates an empty resolution note inline', (tester) async {
    final repository = _FakeDeliveryOrderRepository();
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          deliveryOrderRepositoryProvider.overrideWithValue(repository),
        ],
        child: const MaterialApp(home: _SheetHost()),
      ),
    );
    await tester.tap(find.text('Open resolution'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('RESCHEDULE'));
    await tester.tap(find.text('Submit Resolution'));
    await tester.pump();

    expect(find.text('Enter a resolution note.'), findsOneWidget);
    expect(repository.orderId, isNull);
  });

  testWidgets(
    'submits the selected action, note, and order to the controller',
    (tester) async {
      final repository = _FakeDeliveryOrderRepository();
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            deliveryOrderRepositoryProvider.overrideWithValue(repository),
          ],
          child: const MaterialApp(home: _SheetHost()),
        ),
      );
      await tester.tap(find.text('Open resolution'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('RETURN_TO_ORIGIN'));
      await tester.enterText(
        find.byType(TextFormField),
        'Return parcel to origin.',
      );
      await tester.tap(find.text('Submit Resolution'));
      await tester.pumpAndSettle();

      expect(repository.orderId, 'order-1');
      expect(repository.action, ResolutionAction.returnToOrigin);
      expect(repository.note, 'Return parcel to origin.');
    },
  );
}
