import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../domain/entities/tracking_snapshot.dart';
import '../../../routes/route_paths.dart';
import '../../common/widgets/async_value_view.dart';
import 'order_tracking_controller.dart';
import 'widgets/live_telemetry_card.dart';
import 'widgets/map_trajectory.dart';
import 'widgets/tracking_station_card.dart';
import 'widgets/tracking_status_stepper.dart';

class OrderTrackingPage extends ConsumerWidget {
  const OrderTrackingPage({required this.orderId, super.key});

  final String orderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final provider = orderTrackingControllerProvider(orderId);
    final tracking = ref.watch(provider);
    return Scaffold(
      appBar: AppBar(
        leading: IconButton(
          key: const Key('tracking-back'),
          tooltip: 'Back',
          icon: const Icon(Icons.arrow_back),
          onPressed: () {
            if (context.canPop()) {
              context.pop();
            } else {
              context.go(RoutePaths.customerOrders);
            }
          },
        ),
        title: const Text('Delivery tracking'),
      ),
      body: AsyncValueView(
        value: tracking,
        onRetry: () => ref.read(provider.notifier).retry(),
        data: (value) => _TrackingContent(
          state: value,
          onAskAssistant: () => context.push(
            RoutePaths.customerAssistantFor(
              orderId: value.snapshot.orderId,
              trackingNumber: value.snapshot.trackingNumber,
            ),
          ),
          onViewHistory: () => context.push(
            RoutePaths.customerOrderHistoryFor(value.snapshot.orderId),
          ),
        ),
      ),
    );
  }
}

class _TrackingContent extends StatelessWidget {
  const _TrackingContent({
    required this.state,
    required this.onAskAssistant,
    required this.onViewHistory,
  });

  final OrderTrackingState state;
  final VoidCallback onAskAssistant;
  final VoidCallback onViewHistory;

  @override
  Widget build(BuildContext context) {
    final snapshot = state.snapshot;
    final isInTransit = snapshot.status == DeliveryTrackingStatus.inTransit;
    return ListView(
      key: const Key('tracking-data'),
      padding: const EdgeInsets.all(16),
      children: [
        _TrackingHeader(snapshot: snapshot),
        if (state.isReconnecting) ...[
          const SizedBox(height: 12),
          const _TrackingBanner(
            key: Key('tracking-reconnecting-banner'),
            text: 'Live tracking is temporarily unavailable. Reconnecting…',
          ),
        ],
        if (isInTransit && snapshot.telemetryStale) ...[
          const SizedBox(height: 8),
          const _TrackingBanner(
            key: Key('tracking-stale-banner'),
            text: 'Live position stale (>60s)',
            warning: true,
          ),
        ],
        const SizedBox(height: 16),
        MapTrajectory(snapshot: snapshot),
        const SizedBox(height: 20),
        TrackingStatusStepper(status: snapshot.status),
        if (isInTransit) ...[
          const SizedBox(height: 12),
          LiveTelemetryCard(snapshot: snapshot),
        ],
        const SizedBox(height: 12),
        TrackingStationCard(
          snapshot: snapshot,
          onAskAssistant: onAskAssistant,
          onViewHistory: onViewHistory,
        ),
      ],
    );
  }
}

class _TrackingHeader extends StatelessWidget {
  const _TrackingHeader({required this.snapshot});

  final TrackingSnapshot snapshot;

  @override
  Widget build(BuildContext context) {
    final color = switch (snapshot.status) {
      DeliveryTrackingStatus.inTransit ||
      DeliveryTrackingStatus.readyForTakeoff => const Color(0xFF2563EB),
      DeliveryTrackingStatus.delivered => const Color(0xFF16A34A),
      DeliveryTrackingStatus.failed ||
      DeliveryTrackingStatus.cancelled => const Color(0xFFDC2626),
      DeliveryTrackingStatus.arrived ||
      DeliveryTrackingStatus.pending => const Color(0xFFF97316),
      DeliveryTrackingStatus.approved => const Color(0xFF2563EB),
    };
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          snapshot.trackingNumber,
          key: const Key('tracking-number'),
          style: Theme.of(context).textTheme.titleLarge,
        ),
        const SizedBox(height: 8),
        DecoratedBox(
          key: const Key('tracking-status-badge'),
          decoration: BoxDecoration(
            color: color.withValues(alpha: 0.12),
            borderRadius: BorderRadius.circular(20),
          ),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
            child: Text(
              snapshot.status.apiValue.replaceAll('_', ' '),
              style: TextStyle(color: color, fontWeight: FontWeight.w600),
            ),
          ),
        ),
      ],
    );
  }
}

class _TrackingBanner extends StatelessWidget {
  const _TrackingBanner({required this.text, this.warning = false, super.key});

  final String text;
  final bool warning;

  @override
  Widget build(BuildContext context) => Material(
    color: warning
        ? const Color(0xFFFFF3E0)
        : Theme.of(context).colorScheme.errorContainer,
    borderRadius: BorderRadius.circular(8),
    child: Padding(
      padding: const EdgeInsets.all(12),
      child: Row(
        children: [
          Icon(
            warning ? Icons.warning_amber_outlined : Icons.wifi_off,
            size: 20,
          ),
          const SizedBox(width: 8),
          Expanded(child: Text(text)),
        ],
      ),
    ),
  );
}
