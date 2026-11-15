import 'package:flutter/material.dart';

import '../../../../domain/entities/tracking_snapshot.dart';

class LiveTelemetryCard extends StatelessWidget {
  const LiveTelemetryCard({required this.snapshot, super.key});

  final TrackingSnapshot snapshot;

  @override
  Widget build(BuildContext context) {
    final position = snapshot.lastPosition;
    return Card(
      key: const Key('live-telemetry-card'),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Live telemetry',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 20,
              runSpacing: 12,
              children: [
                _reading(
                  'Speed',
                  position == null ? 'Unavailable' : '${position.speedMps} m/s',
                ),
                _reading(
                  'Altitude',
                  position == null
                      ? 'Unavailable'
                      : '${position.altitudeMeters} m',
                ),
                _reading(
                  'Battery',
                  position == null
                      ? 'Unavailable'
                      : '${position.batteryPercentage}%',
                ),
                _reading('ETA', _formatEta(snapshot.estimatedDeliveryTime)),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _reading(String label, String value) => SizedBox(
    width: 125,
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(fontSize: 12)),
        const SizedBox(height: 3),
        Text(value, style: const TextStyle(fontWeight: FontWeight.w600)),
      ],
    ),
  );

  String _formatEta(DateTime? eta) {
    if (eta == null) return 'Unavailable';
    final local = eta.toLocal();
    final hour = local.hour.toString().padLeft(2, '0');
    final minute = local.minute.toString().padLeft(2, '0');
    return '$hour:$minute';
  }
}
