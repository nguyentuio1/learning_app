import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/error/error_message.dart';
import '../../../core/session/auth_controller.dart';
import '../../../data/models/delivery_order.dart';
import '../../../data/providers/notification_providers.dart';
import '../../../routes/route_paths.dart';
import '../orders/widgets/order_status_badge.dart';
import '../orders/my_orders_provider.dart';

class CustomerHomePage extends ConsumerWidget {
  const CustomerHomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final unreadCountAsync = ref.watch(unreadNotificationsCountProvider);
    final unreadCount = unreadCountAsync.value ?? 0;
    final ordersAsync = ref.watch(customerDashboardOrdersProvider);
    final rawName = ref.watch(currentUserProvider)?.fullName.trim() ?? '';
    final displayName = rawName.isEmpty ? 'Customer' : rawName;

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 640),
            child: ListView(
              padding: const EdgeInsets.fromLTRB(20, 16, 20, 28),
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Container(
                      width: 44,
                      height: 44,
                      decoration: BoxDecoration(
                        color: colorScheme.primaryContainer,
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: Icon(
                        Icons.inventory_2_outlined,
                        color: colorScheme.primary,
                      ),
                    ),
                    Tooltip(
                      message: 'Notifications',
                      child: InkWell(
                        borderRadius: BorderRadius.circular(12),
                        onTap: () =>
                            context.push(RoutePaths.customerNotifications),
                        child: Container(
                          width: 44,
                          height: 44,
                          decoration: BoxDecoration(
                            borderRadius: BorderRadius.circular(12),
                            border: Border.all(
                              color: colorScheme.outlineVariant,
                            ),
                          ),
                          child: Stack(
                            alignment: Alignment.center,
                            children: [
                              Icon(
                                Icons.notifications_none_outlined,
                                color: colorScheme.onSurface,
                              ),
                              if (unreadCount > 0)
                                Positioned(
                                  top: 10,
                                  right: 10,
                                  child: Container(
                                    width: 8,
                                    height: 8,
                                    decoration: BoxDecoration(
                                      color: colorScheme.error,
                                      shape: BoxShape.circle,
                                    ),
                                  ),
                                ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 20),
                Text(
                  'Hello, $displayName',
                  style: theme.textTheme.headlineMedium?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: colorScheme.onSurface,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  'Where would you like to deliver today?',
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: colorScheme.onSurfaceVariant,
                  ),
                ),
                const SizedBox(height: 20),
                Container(
                  padding: const EdgeInsets.all(20),
                  decoration: BoxDecoration(
                    color: colorScheme.primaryContainer.withValues(alpha: 0.45),
                    borderRadius: BorderRadius.circular(16),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Row(
                        children: [
                          Expanded(
                            child: Text(
                              'Send a delivery',
                              style: theme.textTheme.titleMedium?.copyWith(
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                          ),
                          Icon(Icons.send_outlined, color: colorScheme.primary),
                        ],
                      ),
                      const SizedBox(height: 6),
                      Text(
                        'Create and manage your delivery requests.',
                        style: theme.textTheme.bodyMedium?.copyWith(
                          color: colorScheme.onSurfaceVariant,
                        ),
                      ),
                      const SizedBox(height: 16),
                      FilledButton(
                        key: const Key('customer-create-delivery-button'),
                        style: FilledButton.styleFrom(
                          minimumSize: const Size.fromHeight(48),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(12),
                          ),
                        ),
                        onPressed: () =>
                            context.push(RoutePaths.customerOrdersCreate),
                        child: const Text('Create delivery'),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 20),
                ordersAsync.when(
                  loading: () => const Padding(
                    padding: EdgeInsets.all(24),
                    child: Center(child: CircularProgressIndicator()),
                  ),
                  error: (error, _) => Column(
                    children: [
                      Text(errorMessage(error), textAlign: TextAlign.center),
                      TextButton(
                        onPressed: () =>
                            ref.invalidate(customerDashboardOrdersProvider),
                        child: const Text('Retry'),
                      ),
                    ],
                  ),
                  data: (orders) => _buildDashboardSections(context, orders),
                ),
              ],
            ),
          ),
        ),
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: 0,
        onDestinationSelected: (index) {
          switch (index) {
            case 0:
              break;
            case 1:
              context.push(RoutePaths.customerOrders);
            case 2:
              context.push(RoutePaths.customerPackages);
            case 3:
              context.push(RoutePaths.profile);
          }
        },
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.home_outlined),
            selectedIcon: Icon(Icons.home),
            label: 'Home',
          ),
          NavigationDestination(
            icon: Icon(Icons.receipt_long_outlined),
            label: 'Orders',
          ),
          NavigationDestination(
            icon: Icon(Icons.inventory_2_outlined),
            label: 'Packages',
          ),
          NavigationDestination(
            icon: Icon(Icons.person_outline),
            label: 'Profile',
          ),
        ],
      ),
    );
  }

  Widget _buildDashboardSections(
    BuildContext context,
    List<DeliveryOrder> orders,
  ) {
    final theme = Theme.of(context);
    final liveOrder = orders
        .where((order) => order.status.toUpperCase() == 'IN_TRANSIT')
        .firstOrNull;

    final pendingCount = orders
        .where((order) => order.status.toUpperCase() == 'PENDING')
        .length;
    final inTransitCount = orders
        .where((order) => order.status.toUpperCase() == 'IN_TRANSIT')
        .length;
    final deliveredCount = orders
        .where((order) => order.status.toUpperCase() == 'DELIVERED')
        .length;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (liveOrder != null) ...[
          _LiveNowCard(order: liveOrder),
          const SizedBox(height: 20),
        ],
        Text(
          'Recent delivery overview',
          style: theme.textTheme.titleMedium?.copyWith(
            fontWeight: FontWeight.w700,
          ),
        ),
        const Text(
          'Counts below cover the latest 100 orders. See all for full history.',
        ),
        const SizedBox(height: 12),
        Row(
          children: [
            Expanded(
              child: _DeliveryStatCard(
                count: pendingCount,
                label: 'Pending',
                icon: Icons.schedule,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: _DeliveryStatCard(
                count: inTransitCount,
                label: 'In transit',
                icon: Icons.local_shipping_outlined,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: _DeliveryStatCard(
                count: deliveredCount,
                label: 'Delivered',
                icon: Icons.check_circle_outline,
              ),
            ),
          ],
        ),
        const SizedBox(height: 24),
        if (orders.isEmpty) ...[
          Text(
            'Recent deliveries',
            style: theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 12),
          _EmptyDeliveriesCard(
            onCreateDelivery: () =>
                context.push(RoutePaths.customerOrdersCreate),
          ),
        ] else ...[
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'Recent deliveries',
                style: theme.textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w700,
                ),
              ),
              TextButton(
                onPressed: () => context.push(RoutePaths.customerOrders),
                child: const Text('See all'),
              ),
            ],
          ),
          const SizedBox(height: 8),
          for (final order in orders) _OrderCard(order: order),
        ],
      ],
    );
  }
}

class _LiveNowCard extends StatelessWidget {
  const _LiveNowCard({required this.order});

  final DeliveryOrder order;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final tracking = order.trackingNumber ?? order.id;

    return Card(
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: BorderSide(color: colorScheme.outlineVariant),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: () =>
            context.push(RoutePaths.customerOrderTrackingFor(order.id)),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Container(
                        width: 8,
                        height: 8,
                        decoration: BoxDecoration(
                          color: colorScheme.error,
                          shape: BoxShape.circle,
                        ),
                      ),
                      const SizedBox(width: 8),
                      Text(
                        'Live now',
                        style: theme.textTheme.labelLarge?.copyWith(
                          color: colorScheme.error,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ],
                  ),
                  OrderStatusBadge(status: order.status),
                ],
              ),
              const SizedBox(height: 10),
              Text(
                tracking,
                style: theme.textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w700,
                  fontFeatures: const [FontFeature.tabularFigures()],
                ),
              ),
              const SizedBox(height: 4),
              _LiveOrderDetails(order: order),
              const SizedBox(height: 12),
              ClipRRect(
                borderRadius: BorderRadius.circular(999),
                child: LinearProgressIndicator(
                  value: 0.6,
                  minHeight: 6,
                  color: colorScheme.primary,
                  backgroundColor: colorScheme.primaryContainer,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _DeliveryStatCard extends StatelessWidget {
  const _DeliveryStatCard({
    required this.count,
    required this.label,
    required this.icon,
  });

  final int count;
  final String label;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: colorScheme.outlineVariant),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                '$count',
                style: theme.textTheme.headlineSmall?.copyWith(
                  fontWeight: FontWeight.w700,
                  fontFeatures: const [FontFeature.tabularFigures()],
                ),
              ),
              Icon(icon, size: 20, color: colorScheme.onSurfaceVariant),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            label,
            style: theme.textTheme.bodySmall?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
          ),
        ],
      ),
    );
  }
}

class _EmptyDeliveriesCard extends StatelessWidget {
  const _EmptyDeliveriesCard({required this.onCreateDelivery});

  final VoidCallback onCreateDelivery;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 28),
      decoration: BoxDecoration(
        color: colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: colorScheme.outlineVariant),
      ),
      child: Column(
        children: [
          Container(
            width: 48,
            height: 48,
            decoration: BoxDecoration(
              color: colorScheme.primaryContainer.withValues(alpha: 0.6),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Icon(Icons.inventory_2_outlined, color: colorScheme.primary),
          ),
          const SizedBox(height: 14),
          Text(
            'No deliveries yet',
            textAlign: TextAlign.center,
            style: theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            'Your delivery orders will appear here.',
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(height: 16),
          FilledButton(
            onPressed: onCreateDelivery,
            child: const Text('Create delivery'),
          ),
        ],
      ),
    );
  }
}

class _OrderCard extends StatelessWidget {
  const _OrderCard({required this.order});

  final DeliveryOrder order;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final isArrived = order.status.toUpperCase() == 'ARRIVED';
    final title = order.trackingNumber ?? order.id;

    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: 12),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: BorderSide(color: colorScheme.outlineVariant),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: () => context.push(
          RoutePaths.customerOrderDetailsFor(order.id),
          extra: order,
        ),
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
                      style: theme.textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.w700,
                        fontFeatures: const [FontFeature.tabularFigures()],
                      ),
                    ),
                  ),
                  OrderStatusBadge(status: order.status),
                  const SizedBox(width: 4),
                  Icon(
                    Icons.chevron_right,
                    size: 20,
                    color: colorScheme.onSurfaceVariant,
                  ),
                ],
              ),
              const SizedBox(height: 6),
              Text(
                order.packageName?.trim().isNotEmpty == true
                    ? order.packageName!
                    : 'Delivery package',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: theme.textTheme.bodyMedium?.copyWith(
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                '${_stationLabel(order.originStationName, 'Origin station')}'
                ' → '
                '${_stationLabel(order.destinationStationName, 'Destination station')}',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: theme.textTheme.bodySmall?.copyWith(
                  color: colorScheme.onSurfaceVariant,
                ),
              ),
              const SizedBox(height: 10),
              Text(
                _orderTimeLabel(order),
                style: theme.textTheme.bodySmall?.copyWith(
                  color: colorScheme.onSurfaceVariant,
                ),
              ),
              if (isArrived)
                Align(
                  alignment: Alignment.centerRight,
                  child: TextButton(
                    onPressed: () => context.push(
                      RoutePaths.customerOrderConfirmDeliveryFor(order.id),
                      extra: order,
                    ),
                    child: const Text('Confirm receipt'),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _LiveOrderDetails extends ConsumerWidget {
  const _LiveOrderDetails({required this.order});

  final DeliveryOrder order;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(orderDetailProvider(order.id)).asData?.value;
    final eta = detail?.estimatedDeliveryTime ?? order.estimatedDeliveryTime;
    final now = DateTime.now().toUtc();
    final remainingMinutes = eta?.difference(now).inMinutes;
    final packageName = detail?.package?.packageName ?? order.packageName;
    final origin =
        detail?.originStation?.stationCode ??
        detail?.originStation?.name ??
        order.originStationName ??
        'Origin station';
    final destination =
        detail?.destinationStation?.stationCode ??
        detail?.destinationStation?.name ??
        order.destinationStationName ??
        'Destination station';
    final etaText = remainingMinutes == null
        ? 'ETA unavailable'
        : remainingMinutes <= 0
        ? 'ETA passed'
        : 'arrives in $remainingMinutes min';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                eta == null ? 'ETA unavailable' : 'ETA ${_formatTime(eta)}',
                textAlign: TextAlign.end,
                style: Theme.of(context).textTheme.labelMedium?.copyWith(
                  color: Theme.of(context).colorScheme.onSurfaceVariant,
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 4),
        Text(
          '${packageName?.trim().isNotEmpty == true ? packageName : 'Delivery package'}'
          ' · $origin → $destination · $etaText',
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: Theme.of(context).textTheme.bodyMedium
              ?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant),
        ),
      ],
    );
  }
}

String _stationLabel(String? value, String fallback) =>
    value?.trim().isNotEmpty == true ? value! : fallback;

String _orderTimeLabel(DeliveryOrder order) {
  final status = order.status.toUpperCase();
  if (status == 'ARRIVED' ||
      status == 'DELIVERED' ||
      status == 'CANCELLED' ||
      status == 'FAILED') {
    return _humanizeStatus(status);
  }
  final eta = order.estimatedDeliveryTime;
  if (eta != null) return 'ETA ${_formatTime(eta)}';
  final createdAt = order.createdAt;
  return createdAt == null
      ? 'Time unavailable'
      : 'Created ${_formatTime(createdAt)}';
}

String _humanizeStatus(String status) => status
    .toLowerCase()
    .split('_')
    .map(
      (word) =>
          word.isEmpty ? word : '${word[0].toUpperCase()}${word.substring(1)}',
    )
    .join(' ');

String _formatTime(DateTime time) {
  final local = time.toLocal();
  final hour = local.hour % 12 == 0 ? 12 : local.hour % 12;
  final minute = local.minute.toString().padLeft(2, '0');
  final period = local.hour < 12 ? 'AM' : 'PM';
  return '$hour:$minute $period';
}
