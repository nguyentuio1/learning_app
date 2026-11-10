import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter/services.dart';

import 'cancel_order_controller.dart';
import 'order_messages.dart';

class CancelOrderSheet extends ConsumerStatefulWidget {
  const CancelOrderSheet({
    required this.orderId,
    required this.currentStatus,
    super.key,
  });

  final String orderId;
  final String currentStatus;

  @override
  ConsumerState<CancelOrderSheet> createState() => _CancelOrderSheetState();
}

class _CancelOrderSheetState extends ConsumerState<CancelOrderSheet> {
  final _formKey = GlobalKey<FormState>();
  final _reasonController = TextEditingController();

  @override
  void dispose() {
    _reasonController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final cancellationState = ref.watch(cancelOrderControllerProvider);
    final isLoading = cancellationState.isLoading;
    final error = cancellationState.hasError
        ? orderOperationErrorMessage(cancellationState.error!, cancelling: true)
        : null;
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.fromLTRB(
          24,
          24,
          24,
          24 + MediaQuery.of(context).viewInsets.bottom,
        ),
        child: Form(
          key: _formKey,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  'Cancel this order?',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 16),
                TextFormField(
                  controller: _reasonController,
                  maxLength: 500,
                  maxLengthEnforcement: MaxLengthEnforcement.none,
                  maxLines: 3,
                  enabled: !isLoading,
                  decoration: const InputDecoration(
                    labelText: 'Cancellation reason',
                    alignLabelWithHint: true,
                  ),
                  validator: (value) => value == null || value.trim().isEmpty
                      ? 'Enter a cancellation reason.'
                      : value.trim().length > 500
                      ? 'Cancellation reason must be at most 500 characters.'
                      : null,
                ),
                if (error != null) ...[
                  const SizedBox(height: 8),
                  Text(
                    error,
                    key: const Key('cancel-order-error'),
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ],
                const SizedBox(height: 8),
                Row(
                  mainAxisAlignment: MainAxisAlignment.end,
                  children: [
                    TextButton(
                      key: const Key('cancel-order-dismiss'),
                      onPressed: isLoading
                          ? null
                          : () => Navigator.of(context).pop(),
                      child: const Text('Keep order'),
                    ),
                    const SizedBox(width: 8),
                    FilledButton(
                      key: const Key('cancel-order-confirm'),
                      onPressed: isLoading ? null : _submit,
                      child: isLoading
                          ? const SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Text('Confirm cancellation'),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    ref
        .read(cancelOrderControllerProvider.notifier)
        .cancelOrder(
          widget.orderId,
          widget.currentStatus,
          _reasonController.text,
        );
  }
}
