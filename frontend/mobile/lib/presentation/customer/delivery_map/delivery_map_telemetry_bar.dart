import 'package:flutter/material.dart';

import '../../../domain/entities/delivery_map_snapshot.dart';

class DeliveryMapTelemetryBar extends StatelessWidget {
  const DeliveryMapTelemetryBar({required this.snapshot, super.key});

  final DeliveryMapSnapshot snapshot;

  @override
  Widget build(BuildContext context) {
    final telemetry = snapshot.lastPosition;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Wrap(
          alignment: WrapAlignment.spaceAround,
          spacing: 16,
          runSpacing: 12,
          children: [
            _TelemetryValue(
              label: 'Speed',
              value: telemetry == null ? '—' : '${telemetry.speedMps} m/s',
            ),
            _TelemetryValue(
              label: 'Altitude',
              value: telemetry == null ? '—' : '${telemetry.altitudeMeters} m',
            ),
            _TelemetryValue(
              label: 'Battery',
              value: telemetry == null
                  ? '—'
                  : '${telemetry.batteryPercentage}%',
            ),
            _TelemetryValue(
              label: 'Estimated delivery',
              value: snapshot.estimatedDeliveryTime == null
                  ? '—'
                  : _formatEta(snapshot.estimatedDeliveryTime!),
            ),
          ],
        ),
      ),
    );
  }

  String _formatEta(DateTime value) {
    final local = value.toLocal();
    final hour = local.hour.toString().padLeft(2, '0');
    final minute = local.minute.toString().padLeft(2, '0');
    return '$hour:$minute';
  }
}

class _TelemetryValue extends StatelessWidget {
  const _TelemetryValue({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Column(
    mainAxisSize: MainAxisSize.min,
    children: [
      Text(label, style: Theme.of(context).textTheme.labelMedium),
      const SizedBox(height: 4),
      Text(value, style: Theme.of(context).textTheme.titleSmall),
    ],
  );
}
