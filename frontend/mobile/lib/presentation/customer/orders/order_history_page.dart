import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/models/delivery_order_detail_response_dto.dart';
import '../../../routes/route_paths.dart';
import 'my_orders_provider.dart';

class OrderHistoryPage extends ConsumerWidget {
  const OrderHistoryPage({required this.orderId, super.key});

  final String orderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final order = ref.watch(orderDetailProvider(orderId));
    return Scaffold(
      appBar: AppBar(
        title: const Text('Order history'),
        leading: IconButton(
          tooltip: 'Back',
          icon: const Icon(Icons.arrow_back),
          onPressed: () {
            if (context.canPop()) {
              context.pop();
            } else {
              context.go(RoutePaths.customerOrderDetailsFor(orderId));
            }
          },
        ),
      ),
      body: order.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  orderHistoryErrorMessage(error),
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 12),
                FilledButton(
                  onPressed: () => ref.invalidate(orderDetailProvider(orderId)),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
        ),
        data: (detail) => detail.statusLogs.isEmpty
            ? const Center(
                child: Padding(
                  padding: EdgeInsets.all(24),
                  child: Text(
                    'No order history found.',
                    textAlign: TextAlign.center,
                  ),
                ),
              )
            : ListView(
                padding: const EdgeInsets.all(24),
                children: [
                  Text(
                    detail.trackingNumber ?? detail.id,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 20),
                  for (var index = 0; index < detail.statusLogs.length; index++)
                    _HistoryEntry(
                      log: detail.statusLogs[index],
                      hasNext: index < detail.statusLogs.length - 1,
                    ),
                ],
              ),
      ),
    );
  }
}

class _HistoryEntry extends StatelessWidget {
  const _HistoryEntry({required this.log, required this.hasNext});

  final DeliveryOrderStatusLogDto log;
  final bool hasNext;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 20),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Column(
          children: [
            Icon(
              Icons.radio_button_checked,
              color: Theme.of(context).colorScheme.primary,
            ),
            if (hasNext)
              Container(
                width: 2,
                height: 64,
                color: Theme.of(context).dividerColor,
              ),
          ],
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                '${log.fromStatus ?? '—'} → ${log.toStatus}',
                style: Theme.of(context).textTheme.titleSmall,
              ),
              if (log.changedBy?.isNotEmpty == true)
                Padding(
                  padding: const EdgeInsets.only(top: 4),
                  child: Text('Changed by ${log.changedBy}'),
                ),
              if (log.remarks?.isNotEmpty == true)
                Padding(
                  padding: const EdgeInsets.only(top: 4),
                  child: Text(log.remarks!),
                ),
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(log.createdAt.toUtc().toIso8601String()),
              ),
            ],
          ),
        ),
      ],
    ),
  );
}

String orderHistoryErrorMessage(Object error) =>
    'Unable to load order history. Please try again.';
