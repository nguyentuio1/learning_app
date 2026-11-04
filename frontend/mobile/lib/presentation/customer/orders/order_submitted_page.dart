import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../data/models/delivery_order.dart';
import '../../../routes/route_paths.dart';

class OrderSubmittedPage extends StatelessWidget {
  const OrderSubmittedPage({required this.order, super.key});

  final CreateDeliveryOrderResponseDto order;

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Order submitted')),
    body: Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.check_circle, color: Colors.green, size: 64),
            const SizedBox(height: 16),
            Text(
              'Order ${order.trackingNumber} submitted successfully.',
              key: const Key('order-success-message'),
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 20),
            const Text('Status'),
            SelectableText(order.status, key: const Key('order-status')),
            const SizedBox(height: 12),
            const Text('Tracking number'),
            SelectableText(
              order.trackingNumber,
              key: const Key('order-tracking-number'),
            ),
            const SizedBox(height: 12),
            const Text('Pickup secure code'),
            SelectableText(
              order.pickupSecureCode,
              key: const Key('order-pickup-secure-code'),
              style: Theme.of(context).textTheme.headlineMedium,
            ),
            const SizedBox(height: 24),
            FilledButton(
              onPressed: () => context.go(RoutePaths.customerOrders),
              child: const Text('View my orders'),
            ),
            TextButton(
              onPressed: () => context.go(RoutePaths.customerHome),
              child: const Text('Back to home'),
            ),
          ],
        ),
      ),
    ),
  );
}
