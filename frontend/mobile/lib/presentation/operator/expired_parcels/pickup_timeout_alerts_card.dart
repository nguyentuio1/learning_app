import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/error/error_message.dart';
import '../../../data/providers/expired_parcel_providers.dart';
import '../../../routes/route_paths.dart';

class PickupTimeoutAlertsCard extends ConsumerWidget {
  const PickupTimeoutAlertsCard({required this.stationId, super.key});

  final String stationId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final alerts = ref.watch(pickupTimeoutAlertsProvider(stationId));
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Pickup timeout alerts',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            alerts.when(
              loading: () => const LinearProgressIndicator(),
              error: (error, _) => Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(errorMessage(error)),
                  TextButton(
                    onPressed: () =>
                        ref.invalidate(pickupTimeoutAlertsProvider(stationId)),
                    child: const Text('Retry'),
                  ),
                ],
              ),
              data: (items) => items.isEmpty
                  ? const Text('No pickup timeout alerts.')
                  : Column(
                      children: [
                        for (final alert in items)
                          ListTile(
                            contentPadding: EdgeInsets.zero,
                            leading: Icon(
                              Icons.warning_amber_rounded,
                              color: Theme.of(context).colorScheme.error,
                            ),
                            title: Text(alert.title),
                            subtitle: Text(
                              '${alert.trackingNumber}: ${alert.message}',
                            ),
                            onTap: () => context.push(
                              RoutePaths.operatorExpiredParcelsFor(stationId),
                            ),
                          ),
                      ],
                    ),
            ),
          ],
        ),
      ),
    );
  }
}
