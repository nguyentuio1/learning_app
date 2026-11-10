import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/error/error_message.dart';
import '../../../data/models/delivery_order.dart';
import '../../../routes/route_paths.dart';
import '../../customer/orders/my_orders_provider.dart';

class FailedOrdersPage extends ConsumerWidget {
  const FailedOrdersPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final orders = ref.watch(failedOrdersProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Failed deliveries')),
      body: orders.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(errorMessage(error), textAlign: TextAlign.center),
              const SizedBox(height: 12),
              FilledButton(
                onPressed: () => ref.invalidate(failedOrdersProvider),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (items) => items.isEmpty
            ? const Center(child: Text('No failed deliveries.'))
            : ListView.builder(
                itemCount: items.length,
                padding: const EdgeInsets.all(16),
                itemBuilder: (context, index) =>
                    _FailedOrderTile(order: items[index]),
              ),
      ),
    );
  }
}

class _FailedOrderTile extends StatelessWidget {
  const _FailedOrderTile({required this.order});

  final DeliveryOrder order;

  @override
  Widget build(BuildContext context) => Card(
    child: ListTile(
      leading: Icon(
        Icons.error_outline,
        color: Theme.of(context).colorScheme.error,
      ),
      title: Text(order.trackingNumber ?? order.id),
      subtitle: Text(order.recipientName),
      trailing: const Icon(Icons.chevron_right),
      onTap: () => context.push(
        RoutePaths.operatorOrderDetails.replaceFirst(
          ':orderId',
          Uri.encodeComponent(order.id),
        ),
        extra: order,
      ),
    ),
  );
}
