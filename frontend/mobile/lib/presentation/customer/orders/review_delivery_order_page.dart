import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../routes/route_paths.dart';
import 'create_order_notifier.dart';

class ReviewDeliveryOrderPage extends ConsumerWidget {
  const ReviewDeliveryOrderPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final order = ref.watch(createOrderNotifierProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Review delivery order')),
      body: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          _reviewItem('Package', order.package?.packageName ?? 'Not selected'),
          _reviewItem('Origin station', order.originStationId),
          _reviewItem('Destination station', order.destinationStationId),
          _reviewItem(
            'Delivery address',
            order.address?.streetAddress ?? 'None',
          ),
          _reviewItem('Recipient', order.recipientName),
          _reviewItem('Phone', order.recipientPhone),
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
            key: const Key('create-order-submit'),
            onPressed: order.isSubmitting
                ? null
                : () async {
                    final result = await ref
                        .read(createOrderNotifierProvider.notifier)
                        .submit();
                    if (result != null && context.mounted) {
                      context.go(RoutePaths.customerOrderSubmitted);
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

  Widget _reviewItem(String label, String value) => Padding(
    padding: const EdgeInsets.only(bottom: 16),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(fontWeight: FontWeight.bold)),
        const SizedBox(height: 4),
        SelectableText(value),
      ],
    ),
  );
}
