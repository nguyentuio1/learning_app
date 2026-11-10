import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/error_message.dart';
import '../../../data/models/resolve_failed_delivery_request_dto.dart';
import '../../../domain/entities/resolution_action.dart';
import 'failed_delivery_controller.dart';

class ResolveFailedDeliverySheet extends ConsumerStatefulWidget {
  const ResolveFailedDeliverySheet({required this.orderId, super.key});

  final String orderId;

  @override
  ConsumerState<ResolveFailedDeliverySheet> createState() =>
      _ResolveFailedDeliverySheetState();
}

class _ResolveFailedDeliverySheetState
    extends ConsumerState<ResolveFailedDeliverySheet> {
  final _formKey = GlobalKey<FormState>();
  final _noteController = TextEditingController();
  ResolutionAction? _selectedAction;
  String? _actionError;
  bool _submitting = false;

  @override
  void dispose() {
    _noteController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(failedDeliveryControllerProvider, (previous, next) {
      if (!_submitting) return;
      if (next.hasError) {
        _submitting = false;
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(errorMessage(next.error!))));
      } else if (previous?.isLoading == true && next.hasValue) {
        _submitting = false;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(_successMessage(_selectedAction!))),
        );
        Navigator.of(context).pop();
      }
    });

    final isLoading = ref.watch(failedDeliveryControllerProvider).isLoading;
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.fromLTRB(
          24,
          24,
          24,
          24 + MediaQuery.viewInsetsOf(context).bottom,
        ),
        child: Form(
          key: _formKey,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  'Resolve failed delivery',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 12),
                IgnorePointer(
                  ignoring: isLoading,
                  child: RadioGroup<ResolutionAction>(
                    groupValue: _selectedAction,
                    onChanged: (value) => setState(() {
                      _selectedAction = value;
                      _actionError = null;
                    }),
                    child: Column(
                      children: [
                        for (final action in ResolutionAction.values)
                          RadioListTile<ResolutionAction>(
                            contentPadding: EdgeInsets.zero,
                            title: Text(action.value),
                            value: action,
                          ),
                      ],
                    ),
                  ),
                ),
                if (_actionError != null)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: Text(
                      _actionError!,
                      style: TextStyle(
                        color: Theme.of(context).colorScheme.error,
                      ),
                    ),
                  ),
                TextFormField(
                  controller: _noteController,
                  maxLength: 500,
                  maxLines: 3,
                  enabled: !isLoading,
                  decoration: const InputDecoration(
                    labelText: 'Resolution note',
                    alignLabelWithHint: true,
                  ),
                  validator: (value) {
                    if (value == null || value.trim().isEmpty) {
                      return 'Enter a resolution note.';
                    }
                    if (value.trim().length > 500) {
                      return 'Resolution note must be at most 500 characters.';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 8),
                FilledButton(
                  onPressed: isLoading ? null : _submit,
                  child: isLoading
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Text('Submit Resolution'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  void _submit() {
    final validNote = _formKey.currentState!.validate();
    final action = _selectedAction;
    setState(() {
      _actionError = action == null ? 'Select a resolution.' : null;
    });
    if (!validNote || action == null) return;

    _submitting = true;
    ref
        .read(failedDeliveryControllerProvider.notifier)
        .resolveFailedDelivery(
          widget.orderId,
          ResolveFailedDeliveryRequestDto(
            resolutionAction: action,
            resolutionNote: _noteController.text.trim(),
          ),
        );
  }

  String _successMessage(ResolutionAction action) => switch (action) {
    ResolutionAction.reschedule => 'Delivery rescheduled successfully.',
    ResolutionAction.returnToOrigin =>
      'Return requested. Waiting for receipt at the origin station.',
    ResolutionAction.cancel => 'Order cancelled successfully.',
  };
}
