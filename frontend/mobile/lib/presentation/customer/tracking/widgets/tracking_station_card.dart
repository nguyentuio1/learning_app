import 'package:flutter/material.dart';

import '../../../../domain/entities/tracking_snapshot.dart';

class TrackingStationCard extends StatelessWidget {
  const TrackingStationCard({
    required this.snapshot,
    required this.onAskAssistant,
    required this.onViewHistory,
    super.key,
  });

  final TrackingSnapshot snapshot;
  final VoidCallback onAskAssistant;
  final VoidCallback onViewHistory;

  @override
  Widget build(BuildContext context) => Card(
    key: const Key('tracking-station-card'),
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Stations', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 12),
          _station('From', snapshot.originStation.name),
          const SizedBox(height: 8),
          _station('To', snapshot.destinationStation.name),
          const SizedBox(height: 16),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              OutlinedButton.icon(
                key: const Key('ask-ai-assistant'),
                onPressed: onAskAssistant,
                icon: const Icon(Icons.smart_toy_outlined),
                label: const Text('Ask AI Assistant'),
              ),
              OutlinedButton.icon(
                key: const Key('view-order-history'),
                onPressed: onViewHistory,
                icon: const Icon(Icons.history),
                label: const Text('View Order History'),
              ),
            ],
          ),
        ],
      ),
    ),
  );

  Widget _station(String label, String name) => Row(
    children: [
      SizedBox(width: 48, child: Text(label)),
      const SizedBox(width: 8),
      Expanded(child: Text(name)),
    ],
  );
}
