import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/presentation/common/widgets/async_value_view.dart';
import 'package:smart_drone_delivery/presentation/common/widgets/empty_state_view.dart';

void main() {
  group('EmptyStateView', () {
    testWidgets('renders title, description and default icon', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: EmptyStateView(
              title: 'No items found',
              description: 'Try adjusting your filters.',
            ),
          ),
        ),
      );

      expect(find.byIcon(Icons.inbox_outlined), findsOneWidget);
      expect(find.text('No items found'), findsOneWidget);
      expect(find.text('Try adjusting your filters.'), findsOneWidget);
      expect(find.byType(FilledButton), findsNothing);
    });

    testWidgets('renders custom icon and triggers action callback on CTA tap', (
      tester,
    ) async {
      var actionTriggered = false;

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: EmptyStateView(
              title: 'No saved addresses',
              icon: Icons.location_on_outlined,
              description: 'Add an address to start ordering.',
              actionLabel: 'Add address',
              onAction: () => actionTriggered = true,
            ),
          ),
        ),
      );

      expect(find.byIcon(Icons.location_on_outlined), findsOneWidget);
      expect(find.text('No saved addresses'), findsOneWidget);
      expect(find.text('Add address'), findsOneWidget);

      await tester.tap(find.text('Add address'));
      await tester.pumpAndSettle();

      expect(actionTriggered, isTrue);
    });

    testWidgets('does not overflow on narrow 320x568 viewport', (tester) async {
      tester.view.physicalSize = const Size(320, 568);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: EmptyStateView(
              title:
                  'A somewhat lengthy empty state title for small screen check',
              description: 'Detailed description explaining what happened and how to proceed on smaller devices without causing RenderFlex overflow.',
              actionLabel: 'Take Action Now',
              onAction: () {},
            ),
          ),
        ),
      );

      expect(tester.takeException(), isNull);
      expect(find.textContaining('A somewhat lengthy'), findsOneWidget);
    });
  });

  group('AsyncValueView', () {
    testWidgets('renders data when AsyncValue is AsyncData and not empty', (
      tester,
    ) async {
      const value = AsyncValue.data(['item1', 'item2']);

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: AsyncValueView<List<String>>(
              value: value,
              isEmpty: (data) => data.isEmpty,
              emptyBuilder: (_) => const Text('Empty list'),
              data: (data) => Text('Count: ${data.length}'),
            ),
          ),
        ),
      );

      expect(find.text('Count: 2'), findsOneWidget);
      expect(find.text('Empty list'), findsNothing);
    });

    testWidgets('renders emptyBuilder when AsyncData satisfies isEmpty', (
      tester,
    ) async {
      const value = AsyncValue.data(<String>[]);

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: AsyncValueView<List<String>>(
              value: value,
              isEmpty: (data) => data.isEmpty,
              emptyBuilder: (_) =>
                  const EmptyStateView(title: 'No items available'),
              data: (data) => Text('Count: ${data.length}'),
            ),
          ),
        ),
      );

      expect(find.text('No items available'), findsOneWidget);
      expect(find.text('Count: 0'), findsNothing);
    });

    testWidgets(
      'displays network icon for network error and triggers onRetry',
      (tester) async {
        var retryClicked = false;
        const networkError = ApiException(
          message: 'Network problem. Check your connection and try again.',
          isNetwork: true,
        );

        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: AsyncValueView<String>(
                value: const AsyncValue<String>.error(
                  networkError,
                  StackTrace.empty,
                ),
                onRetry: () => retryClicked = true,
                data: (data) => Text(data),
              ),
            ),
          ),
        );

        expect(find.byIcon(Icons.wifi_off_rounded), findsOneWidget);
        expect(find.byIcon(Icons.error_outline), findsNothing);
        expect(
          find.text('Network problem. Check your connection and try again.'),
          findsOneWidget,
        );

        await tester.tap(find.text('Retry'));
        await tester.pumpAndSettle();
        expect(retryClicked, isTrue);
      },
    );

    testWidgets('displays error_outline icon for non-network error', (
      tester,
    ) async {
      const apiError = ApiException(
        message: 'The requested resource was not found.',
        statusCode: 404,
        isNetwork: false,
      );

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: AsyncValueView<String>(
              value: const AsyncValue<String>.error(apiError, StackTrace.empty),
              onRetry: () {},
              data: (data) => Text(data),
            ),
          ),
        ),
      );

      expect(find.byIcon(Icons.error_outline), findsOneWidget);
      expect(find.byIcon(Icons.wifi_off_rounded), findsNothing);
      expect(
        find.text('The requested resource was not found.'),
        findsOneWidget,
      );
    });

    testWidgets(
      'never renders emptyBuilder or data when state is AsyncError (Trap 7 guard)',
      (tester) async {
        // AsyncError with previous data
        final asyncErrorWithPrevious = AsyncValue<List<String>>.error(
          const ApiException(message: 'Failed to fetch items'),
          StackTrace.empty,
        );

        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: AsyncValueView<List<String>>(
                value: asyncErrorWithPrevious,
                isEmpty: (data) => data.isEmpty,
                emptyBuilder: (_) => const Text('EMPTY_STATE_SHOULD_NOT_SHOW'),
                data: (data) => const Text('DATA_STATE_SHOULD_NOT_SHOW'),
              ),
            ),
          ),
        );

        // Verify that error is displayed and emptyBuilder / data is strictly not shown
        expect(find.text('Failed to fetch items'), findsOneWidget);
        expect(find.text('EMPTY_STATE_SHOULD_NOT_SHOW'), findsNothing);
        expect(find.text('DATA_STATE_SHOULD_NOT_SHOW'), findsNothing);
      },
    );

    testWidgets('does not overflow on narrow 320x568 viewport', (tester) async {
      tester.view.physicalSize = const Size(320, 568);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      const longError = ApiException(
        message: 'A very descriptive and long error message returned by server to verify that the error card does not cause RenderFlex overflow on small screens.',
      );

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: AsyncValueView<String>(
              value: const AsyncValue<String>.error(
                longError,
                StackTrace.empty,
              ),
              onRetry: () {},
              data: (data) => Text(data),
            ),
          ),
        ),
      );

      expect(tester.takeException(), isNull);
      expect(find.textContaining('A very descriptive'), findsOneWidget);
    });
  });
}
