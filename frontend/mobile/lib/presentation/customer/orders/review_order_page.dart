import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../routes/route_paths.dart';
import 'create_order_notifier.dart';

class ReviewOrderPage extends ConsumerWidget {
  const ReviewOrderPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final order = ref.watch(createOrderNotifierProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Review order')),
      body: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          _row('Package', order.package?.packageName ?? ''),
          _row('Origin station', order.originStationId),
          _row('Destination station', order.destinationStationId),
          _row('Delivery address', order.address?.streetAddress ?? 'None'),
          _row('Recipient name', order.recipientName),
          _row('Recipient phone', order.recipientPhone),
          if (order.error != null)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 12),
              child: Text(
                order.error!.message,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ),
          const SizedBox(height: 20),
          FilledButton(
            key: const Key('order-submit'),
            onPressed: order.isSubmitting
                ? null
                : () async {
                    final result = await ref
                        .read(createOrderNotifierProvider.notifier)
                        .submit();
                    if (result != null && context.mounted) {
                      context.push(
                        RoutePaths.customerOrderSubmitted,
                        extra: result,
                      );
                    }
                  },
            child: order.isSubmitting
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Submit order'),
          ),
        ],
      ),
    );
  }

  Widget _row(String title, String value) => Padding(
    padding: const EdgeInsets.only(bottom: 16),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(title, style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        SelectableText(value),
      ],
    ),
  );
}
