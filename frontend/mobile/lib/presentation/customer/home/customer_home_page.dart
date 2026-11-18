import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/error/error_message.dart';
import '../../../core/session/auth_controller.dart';
import '../../../data/models/delivery_order.dart';
import '../../../data/providers/notification_providers.dart';
import '../../common/widgets/empty_state_view.dart';
import '../../../routes/route_paths.dart';
import '../orders/my_orders_provider.dart';

class CustomerHomePage extends ConsumerWidget {
  const CustomerHomePage({super.key});

  static const _activeStatuses = {
    'PENDING',
    'APPROVED',
    'SCHEDULED',
    'READY_FOR_TAKEOFF',
    'IN_TRANSIT',
    'ARRIVED',
  };

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unreadCountAsync = ref.watch(unreadNotificationsCountProvider);
    final unreadCount = unreadCountAsync.value ?? 0;
    final ordersAsync = ref.watch(myOrdersProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Customer Home'),
        actions: [
          IconButton(
            tooltip: 'Notifications',
            icon: Badge(
              isLabelVisible: unreadCount > 0,
              label: Text('$unreadCount'),
              child: const Icon(Icons.notifications_outlined),
            ),
            onPressed: () => context.push(RoutePaths.customerNotifications),
          ),
          IconButton(
            tooltip: 'Profile & Settings',
            icon: const Icon(Icons.account_circle_outlined),
            onPressed: () => context.push(RoutePaths.profile),
          ),
          IconButton(
            tooltip: 'Sign out',
            icon: const Icon(Icons.logout),
            onPressed: () =>
                ref.read(authControllerProvider.notifier).signOut(),
          ),
        ],
      ),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 640),
          child: ListView(
            padding: const EdgeInsets.all(24),
            children: [
              FilledButton.icon(
                key: const Key('customer-create-delivery-button'),
                style: FilledButton.styleFrom(
                  minimumSize: const Size.fromHeight(48),
                ),
                onPressed: () => context.push(RoutePaths.customerOrdersCreate),
                icon: const Icon(Icons.add),
                label: const Text('Create delivery'),
              ),
              const SizedBox(height: 24),
              Text(
                'Your orders',
                style: Theme.of(context).textTheme.titleLarge,
              ),
              const SizedBox(height: 12),
              ordersAsync.when(
                loading: () => const Padding(
                  padding: EdgeInsets.all(24),
                  child: Center(child: CircularProgressIndicator()),
                ),
                error: (error, _) => Column(
                  children: [
                    Text(errorMessage(error), textAlign: TextAlign.center),
                    TextButton(
                      onPressed: () => ref.invalidate(myOrdersProvider),
                      child: const Text('Retry'),
                    ),
                  ],
                ),
                data: (orders) => _buildOrders(context, orders),
              ),
              const SizedBox(height: 24),
              Text(
                'Quick links',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                runSpacing: 4,
                children: [
                  TextButton.icon(
                    onPressed: () => context.push(RoutePaths.customerAddresses),
                    icon: const Icon(Icons.location_on_outlined),
                    label: const Text('Manage addresses'),
                  ),
                  TextButton.icon(
                    onPressed: () => context.push(RoutePaths.customerPackages),
                    icon: const Icon(Icons.inventory_2_outlined),
                    label: const Text('Manage packages'),
                  ),
                  TextButton.icon(
                    onPressed: () => context.push(RoutePaths.customerOrders),
                    icon: const Icon(Icons.local_shipping_outlined),
                    label: const Text('All orders'),
                  ),
                  TextButton.icon(
                    key: const Key('home-ai-assistant-button'),
                    onPressed: () => context.push(RoutePaths.customerAssistant),
                    icon: const Icon(Icons.smart_toy_outlined),
                    label: const Text('AI Assistant'),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildOrders(BuildContext context, List<DeliveryOrder> orders) {
    if (orders.isEmpty) {
      return EmptyStateView(
        title: 'No orders yet',
        description: 'Create a delivery to see its progress here.',
        actionLabel: 'Create delivery',
        onAction: () => context.push(RoutePaths.customerOrdersCreate),
      );
    }

    final active = orders
        .where((order) => _activeStatuses.contains(order.status.toUpperCase()))
        .toList();
    final recent = orders
        .where((order) => !_activeStatuses.contains(order.status.toUpperCase()))
        .toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (active.isNotEmpty) ...[
          Text('Active orders', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          for (final order in active) _OrderCard(order: order),
        ],
        if (recent.isNotEmpty) ...[
          if (active.isNotEmpty) const SizedBox(height: 12),
          Text('Recent orders', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          for (final order in recent) _OrderCard(order: order),
        ],
      ],
    );
  }
}

class _OrderCard extends StatelessWidget {
  const _OrderCard({required this.order});

  final DeliveryOrder order;

  @override
  Widget build(BuildContext context) {
    final isArrived = order.status.toUpperCase() == 'ARRIVED';
    final title = order.trackingNumber ?? order.id;
    final humanStatus = order.status
        .toLowerCase()
        .split('_')
        .map(
          (word) => word.isEmpty
              ? word
              : '${word[0].toUpperCase()}${word.substring(1)}',
        )
        .join(' ');

    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: isArrived
            ? () => context.push(
                RoutePaths.customerOrderDetailsFor(order.id),
                extra: order,
              )
            : null,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      title,
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                  ),
                  Chip(
                    label: Text(humanStatus),
                    visualDensity: VisualDensity.compact,
                  ),
                ],
              ),
              Text('Recipient: ${order.recipientName}'),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                children: [
                  TextButton.icon(
                    onPressed: () => context.push(
                      RoutePaths.customerOrderTrackingFor(order.id),
                    ),
                    icon: const Icon(Icons.location_searching),
                    label: const Text('Track'),
                  ),
                  if (isArrived)
                    FilledButton.tonal(
                      onPressed: () => context.push(
                        RoutePaths.customerOrderConfirmDeliveryFor(order.id),
                        extra: order,
                      ),
                      child: const Text('Confirm receipt'),
                    ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
