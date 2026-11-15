import 'package:flutter/material.dart';

import '../../../../domain/entities/tracking_snapshot.dart';

class TrackingStatusStepper extends StatelessWidget {
  const TrackingStatusStepper({required this.status, super.key});

  final DeliveryTrackingStatus status;

  static const _steps = [
    DeliveryTrackingStatus.pending,
    DeliveryTrackingStatus.approved,
    DeliveryTrackingStatus.readyForTakeoff,
    DeliveryTrackingStatus.inTransit,
    DeliveryTrackingStatus.arrived,
    DeliveryTrackingStatus.delivered,
  ];

  @override
  Widget build(BuildContext context) {
    final currentIndex = _steps.indexOf(status);
    final isTerminal =
        status == DeliveryTrackingStatus.cancelled ||
        status == DeliveryTrackingStatus.failed;
    return Column(
      key: const Key('tracking-stepper'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Delivery progress',
          style: Theme.of(context).textTheme.titleLarge,
        ),
        const SizedBox(height: 12),
        for (var index = 0; index < _steps.length; index++)
          _stepRow(context, _steps[index], index, currentIndex, isTerminal),
        if (isTerminal)
          ListTile(
            key: Key('terminal-status-${status.apiValue}'),
            contentPadding: EdgeInsets.zero,
            leading: Icon(
              status == DeliveryTrackingStatus.failed
                  ? Icons.error_outline
                  : Icons.cancel_outlined,
              color: const Color(0xFFDC2626),
            ),
            title: Text(_label(status)),
            subtitle: const Text('This order will not progress further.'),
          ),
      ],
    );
  }

  Widget _stepRow(
    BuildContext context,
    DeliveryTrackingStatus step,
    int index,
    int currentIndex,
    bool isTerminal,
  ) {
    final isCurrent = !isTerminal && step == status;
    final isComplete = !isTerminal && currentIndex > index;
    final color = isCurrent || isComplete
        ? Theme.of(context).colorScheme.primary
        : Theme.of(context).colorScheme.outline;
    return ListTile(
      key: Key('tracking-step-${step.apiValue}'),
      contentPadding: EdgeInsets.zero,
      leading: Icon(
        isComplete ? Icons.check_circle : Icons.radio_button_unchecked,
        color: color,
      ),
      title: Text(
        _label(step),
        style: TextStyle(
          color: color,
          fontWeight: isCurrent ? FontWeight.bold : FontWeight.normal,
        ),
      ),
      trailing: isCurrent ? const Text('Current') : null,
    );
  }

  String _label(DeliveryTrackingStatus value) =>
      value.apiValue.replaceAll('_', ' ');
}
