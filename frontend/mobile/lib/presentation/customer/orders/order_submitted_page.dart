import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../routes/route_paths.dart';
import 'create_order_notifier.dart';

class OrderSubmittedPage extends ConsumerWidget {
  const OrderSubmittedPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final result = ref.watch(createOrderNotifierProvider).result;
    if (result == null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Order submitted')),
        body: const Center(child: Text('Order confirmation is unavailable.')),
      );
    }
    return Scaffold(
      appBar: AppBar(title: const Text('Order submitted')),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.check_circle, size: 64, color: Colors.green),
              const SizedBox(height: 16),
              Text(
                'Order ${result.trackingNumber} submitted successfully.',
                key: const Key('create-order-success-message'),
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.titleLarge,
              ),
              const SizedBox(height: 24),
              const Text('Tracking number'),
              SelectableText(
                result.trackingNumber,
                key: const Key('create-order-tracking-number'),
              ),
              const SizedBox(height: 16),
              const Text('Pickup secure code'),
              SelectableText(
                result.pickupSecureCode!,
                key: const Key('create-order-pickup-code'),
              ),
              const SizedBox(height: 28),
              FilledButton(
                onPressed: () => context.go(
                  RoutePaths.customerOrderDetails.replaceFirst(
                    ':orderId',
                    Uri.encodeComponent(result.id),
                  ),
                ),
                child: const Text('View order details'),
              ),
              TextButton(
                onPressed: () {
                  ref.read(createOrderNotifierProvider.notifier).reset();
                  context.go(RoutePaths.customerHome);
                },
                child: const Text('Back to home'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
