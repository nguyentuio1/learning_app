import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../data/models/eta_estimation_model.dart';
import '../eta_estimation_controller.dart';

class EtaEstimationCard extends ConsumerWidget {
  const EtaEstimationCard({
    required this.orderId,
    super.key,
  });

  final String orderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(etaEstimationControllerProvider(orderId));
    final theme = Theme.of(context);

    if (state.accessLost) {
      return Card(
        key: const Key('eta-access-lost-card'),
        color: theme.colorScheme.errorContainer,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Icon(
                Icons.lock_person_outlined,
                color: theme.colorScheme.error,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  state.errorMessage ??
                      'Order not found or unavailable to your account.',
                ),
              ),
            ],
          ),
        ),
      );
    }

    final estimate = state.estimate;
    if (estimate == null) {
      if (state.isLoading) {
        return const Card(
          key: Key('eta-loading-card'),
          child: Padding(
            padding: EdgeInsets.all(16),
            child: Center(child: CircularProgressIndicator()),
          ),
        );
      }
      return Card(
        key: const Key('eta-error-card'),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  state.errorMessage ??
                      'Unable to load ETA estimate. Please try again.',
                ),
              ),
              TextButton.icon(
                key: const Key('refresh-eta-button'),
                onPressed: () => ref
                    .read(etaEstimationControllerProvider(orderId).notifier)
                    .refreshEta(),
                icon: const Icon(Icons.refresh, size: 18),
                label: const Text('Refresh ETA'),
              ),
            ],
          ),
        ),
      );
    }

    return Card(
      key: const Key('eta-estimation-card'),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.auto_awesome_outlined, size: 20),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'AI ETA prediction',
                    style: theme.textTheme.titleMedium,
                  ),
                ),
                _FreshnessBadge(status: estimate.freshnessStatus),
                IconButton(
                  key: const Key('refresh-eta-button'),
                  tooltip: 'Refresh ETA',
                  onPressed: state.isRefreshing
                      ? null
                      : () => ref
                            .read(
                              etaEstimationControllerProvider(orderId).notifier,
                            )
                            .refreshEta(),
                  icon: state.isRefreshing
                      ? const SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.refresh, size: 20),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Text(
              _summaryText(estimate),
              key: const Key('eta-summary-text'),
              style: theme.textTheme.bodyMedium,
            ),
            if (state.errorMessage != null) ...[
              const SizedBox(height: 8),
              Text(
                state.errorMessage!,
                key: const Key('eta-inline-error'),
                style: TextStyle(color: theme.colorScheme.error),
              ),
            ],
            if (estimate.hasPrediction) ...[
              const Divider(height: 24),
              if (estimate.calculatedDistanceKm != null)
                _field(
                  context,
                  'Distance',
                  '${estimate.calculatedDistanceKm} km',
                ),
              if (estimate.predictedDurationMinutes != null)
                _field(
                  context,
                  'Predicted duration',
                  '${estimate.predictedDurationMinutes} minutes',
                ),
              if (estimate.weatherCondition != null)
                _field(context, 'Weather', estimate.weatherCondition!),
              if (estimate.confidenceScore != null)
                _field(
                  context,
                  'Confidence',
                  '${estimate.confidencePercentage}% (${estimate.confidenceScore})',
                ),
              if (estimate.predictionCreatedAt != null)
                _field(
                  context,
                  'Prediction created',
                  _formatTimestamp(estimate.predictionCreatedAt!),
                ),
            ],
          ],
        ),
      ),
    );
  }

  String _summaryText(OrderEtaEstimate estimate) {
    switch (estimate.freshnessStatus) {
      case EtaFreshnessStatus.waitingForApproval:
        return 'Waiting for approval — ETA will be calculated once approved.';
      case EtaFreshnessStatus.unavailable:
        return 'ETA unavailable — awaiting route or telemetry calculation.';
      case EtaFreshnessStatus.stale:
        return 'Estimate stale — previous arrival window has passed or telemetry is delayed.';
      case EtaFreshnessStatus.arrivedAtDestination:
        return 'Arrived at destination station — awaiting recipient pickup confirmation.';
      case EtaFreshnessStatus.completed:
        return 'Delivery completed — see Actual delivery timestamp for the recorded arrival.';
      case EtaFreshnessStatus.inactive:
        return estimate.orderStatus == 'CANCELLED'
            ? 'Live ETA is no longer active because this order was cancelled.'
            : 'Live ETA is no longer active because this delivery failed.';
      case EtaFreshnessStatus.current:
        final arrival = estimate.estimatedDeliveryTime;
        final remaining = estimate.remainingMinutes;
        if (arrival != null && remaining != null) {
          return 'Estimated arrival at ${_formatClock(arrival)} (in $remaining min)';
        }
        if (arrival != null) {
          return 'Estimated arrival at ${_formatClock(arrival)}';
        }
        if (estimate.predictedDurationMinutes != null) {
          return 'Estimated flight duration: about ${estimate.predictedDurationMinutes} min';
        }
        return 'Live ETA estimate active.';
    }
  }

  Widget _field(BuildContext context, String label, String value) => Padding(
    padding: const EdgeInsets.only(bottom: 12),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: Theme.of(context).textTheme.labelLarge),
        const SizedBox(height: 4),
        SelectableText(value),
      ],
    ),
  );

  String _formatClock(DateTime value) {
    final local = value.toLocal();
    final hour = local.hour.toString().padLeft(2, '0');
    final minute = local.minute.toString().padLeft(2, '0');
    return '$hour:$minute';
  }

  String _formatTimestamp(DateTime value) => value.toLocal().toString();
}

class _FreshnessBadge extends StatelessWidget {
  const _FreshnessBadge({required this.status});

  final EtaFreshnessStatus status;

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (status) {
      EtaFreshnessStatus.current => ('Live estimate', const Color(0xFF2563EB)),
      EtaFreshnessStatus.stale => ('Estimate stale', const Color(0xFFD97706)),
      EtaFreshnessStatus.waitingForApproval => (
        'Waiting for approval',
        const Color(0xFF6B7280),
      ),
      EtaFreshnessStatus.arrivedAtDestination => (
        'Arrived at station',
        const Color(0xFFF97316),
      ),
      EtaFreshnessStatus.completed => (
        'Completed',
        const Color(0xFF16A34A),
      ),
      EtaFreshnessStatus.inactive => ('ETA inactive', const Color(0xFFDC2626)),
      EtaFreshnessStatus.unavailable => (
        'ETA unavailable',
        const Color(0xFF6B7280),
      ),
    };

    return DecoratedBox(
      key: const Key('eta-freshness-badge'),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
        child: Text(
          label,
          style: TextStyle(
            color: color,
            fontSize: 12,
            fontWeight: FontWeight.w600,
          ),
        ),
      ),
    );
  }
}

class RouteEtaPreviewCard extends StatelessWidget {
  const RouteEtaPreviewCard({
    required this.preview,
    super.key,
  });

  final RouteEtaPreview preview;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final handling = preview.isFragile ? ' · fragile handling' : '';
    return Card(
      key: const Key('route-eta-preview-card'),
      color: theme.colorScheme.secondaryContainer.withValues(alpha: 0.45),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Row(
          children: [
            Icon(
              Icons.auto_awesome_outlined,
              color: theme.colorScheme.primary,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'AI estimate',
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    '${preview.distanceKm} km · about ${preview.estimatedDurationMinutes} min$handling',
                    key: const Key('route-eta-preview-text'),
                    style: theme.textTheme.bodyMedium,
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
