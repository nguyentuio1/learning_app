import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/models/delivery_order.dart';
import '../../../data/models/delivery_order_detail_response_dto.dart';
import '../../../data/models/station_operation_models.dart';
import '../../../routes/route_paths.dart';
import 'delivery_confirmation_controller.dart';
import 'my_orders_provider.dart';
import 'order_detail_page.dart';

class ConfirmDeliveryPage extends ConsumerStatefulWidget {
  const ConfirmDeliveryPage({
    required this.orderId,
    this.initialOrder,
    super.key,
  });

  final String orderId;
  final DeliveryOrder? initialOrder;

  @override
  ConsumerState<ConfirmDeliveryPage> createState() =>
      _ConfirmDeliveryPageState();
}

class _ConfirmDeliveryPageState extends ConsumerState<ConfirmDeliveryPage> {
  final _codeController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _codeController.addListener(_onCodeChanged);
  }

  void _onCodeChanged() {
    if (mounted) {
      setState(() {});
    }
  }

  @override
  void dispose() {
    _codeController
      ..removeListener(_onCodeChanged)
      ..dispose();
    super.dispose();
  }

  void _appendDigit(String digit, bool disabled) {
    if (disabled) return;
    final current = _codeController.text.trim();
    if (current.length >= 6) return;
    final next = '$current$digit';
    _codeController.value = TextEditingValue(
      text: next,
      selection: TextSelection.collapsed(offset: next.length),
    );
  }

  void _backspace(bool disabled) {
    if (disabled) return;
    final current = _codeController.text;
    if (current.isEmpty) return;
    final next = current.substring(0, current.length - 1);
    _codeController.value = TextEditingValue(
      text: next,
      selection: TextSelection.collapsed(offset: next.length),
    );
  }

  void _clearCode(bool disabled) {
    if (disabled) return;
    _codeController.clear();
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(
      deliveryConfirmationControllerProvider(widget.orderId),
    );
    final detailAsync = ref.watch(orderDetailProvider(widget.orderId));
    final detailError = detailAsync.hasError ? detailAsync.error : null;

    if (state.accessLost ||
        (detailError != null &&
            DeliveryConfirmationController.isAccessDeniedError(detailError))) {
      final message = state.accessLost && state.errorMessage != null
          ? state.errorMessage!
          : (detailError != null
                ? DeliveryConfirmationController.accessDeniedMessage(
                    detailError,
                  )
                : 'Order not found or unavailable to your account.');
      return Scaffold(
        appBar: AppBar(title: const Text('Confirm delivery')),
        body: _AccessLostView(message: message, onBackToOrders: _goToOrders),
      );
    }

    if (state.completedConfirmation != null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Confirm delivery')),
        body: _DeliveryCompletedView(
          confirmation: state.completedConfirmation!,
          onBackToOrders: _goToOrders,
          onViewOrderHistory: () =>
              _viewOrderHistory(state.completedConfirmation),
        ),
      );
    }

    return Scaffold(
      appBar: AppBar(title: const Text('Confirm delivery')),
      body: detailAsync.when(
        skipError: true,
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) {
          if (DeliveryConfirmationController.isAccessDeniedError(error)) {
            return _AccessLostView(
              message: DeliveryConfirmationController.accessDeniedMessage(
                error,
              ),
              onBackToOrders: _goToOrders,
            );
          }
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    orderDetailErrorMessage(error),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 12),
                  FilledButton(
                    onPressed: () => ref
                        .read(
                          deliveryConfirmationControllerProvider(widget.orderId)
                              .notifier,
                        )
                        .refreshOrderEligibility(),
                    child: const Text('Retry'),
                  ),
                ],
              ),
            ),
          );
        },
        data: (detail) {
          if (detail.status == 'DELIVERED') {
            final synthetic = ConfirmPickupResponse(
              orderId: detail.id,
              trackingNumber: detail.trackingNumber ?? detail.id,
              status: 'DELIVERED',
              actualDeliveryTime:
                  detail.actualDeliveryTime ??
                  detail.updatedAt ??
                  DateTime.now().toUtc(),
              slotId: '',
              slotReleased: true,
            );
            return _DeliveryCompletedView(
              confirmation: synthetic,
              onBackToOrders: _goToOrders,
              onViewOrderHistory: () => _viewOrderHistory(synthetic),
            );
          }

          if (detail.status != 'ARRIVED') {
            return _IneligibleOrderView(
              detail: detail,
              errorMessage: state.errorMessage,
              onRefresh: () => ref
                  .read(
                    deliveryConfirmationControllerProvider(widget.orderId)
                        .notifier,
                  )
                  .refreshOrderEligibility(),
              onBack: () => _viewOrderHistory(null),
            );
          }

          return _buildConfirmForm(context, detail, state, detailAsync);
        },
      ),
    );
  }

  Widget _buildConfirmForm(
    BuildContext context,
    DeliveryOrderDetailResponseDto detail,
    DeliveryConfirmationState state,
    AsyncValue<DeliveryOrderDetailResponseDto> detailAsync,
  ) {
    final theme = Theme.of(context);
    final code = _codeController.text.trim();
    final isRefreshPendingOrFailed =
        state.requiresOrderRefresh ||
        detailAsync.isLoading ||
        detailAsync.isRefreshing ||
        detailAsync.hasError;
    final inputDisabled =
        state.isLocked || state.isSubmitting || isRefreshPendingOrFailed;
    final canSubmit = !inputDisabled && RegExp(r'^\d{6}$').hasMatch(code);
    final trackingLabel =
        detail.trackingNumber ??
        widget.initialOrder?.trackingNumber ??
        detail.id;
    final stationLabel = _destinationLabel(detail);

    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            stationLabel.isEmpty
                ? trackingLabel
                : '$trackingLabel · $stationLabel',
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: theme.colorScheme.onSurfaceVariant,
              fontWeight: FontWeight.w500,
            ),
          ),
          const SizedBox(height: 16),
          Text(
            'Enter the pickup code',
            textAlign: TextAlign.center,
            style: theme.textTheme.headlineSmall?.copyWith(
              fontWeight: FontWeight.bold,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'Type the 6-digit code from your order. It opens the compartment and completes delivery.',
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: theme.colorScheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(height: 20),

          // 6-digit visual boxes (C11 / C12)
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: List.generate(6, (index) {
              final char = index < code.length ? code[index] : '';
              final isCurrent = index == code.length && !inputDisabled;
              final hasError = state.errorMessage != null;
              final borderColor = hasError
                  ? theme.colorScheme.error
                  : isCurrent
                  ? theme.colorScheme.primary
                  : theme.dividerColor;
              return Container(
                key: Key('otp-box-$index'),
                width: 44,
                height: 54,
                margin: const EdgeInsets.symmetric(horizontal: 4),
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(
                    color: borderColor,
                    width: (hasError || isCurrent) ? 2 : 1,
                  ),
                ),
                child: Text(
                  char,
                  style: theme.textTheme.titleLarge?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
                ),
              );
            }),
          ),
          const SizedBox(height: 16),

          // Direct text input for keyboard / accessibility / testing
          TextField(
            key: const Key('pickup-secure-code-input'),
            controller: _codeController,
            enabled: !inputDisabled,
            keyboardType: TextInputType.number,
            maxLength: 6,
            inputFormatters: [FilteringTextInputFormatter.digitsOnly],
            decoration: const InputDecoration(
              labelText: '6-digit Pickup Code',
              hintText: '593284',
              prefixIcon: Icon(Icons.pin_outlined),
              counterText: '',
            ),
          ),
          const SizedBox(height: 12),

          if (state.errorMessage != null || isRefreshPendingOrFailed) ...[
            _ErrorAndLockoutBanner(
              state: state,
              isRefreshPending:
                  detailAsync.isLoading || detailAsync.isRefreshing,
              onRefreshOrder: isRefreshPendingOrFailed
                  ? () => ref
                        .read(
                          deliveryConfirmationControllerProvider(widget.orderId)
                              .notifier,
                        )
                        .refreshOrderEligibility()
                  : null,
            ),
            const SizedBox(height: 16),
          ],

          // On-screen keypad (C11 / C12)
          _NumericKeypad(
            disabled: inputDisabled,
            onDigit: (digit) => _appendDigit(digit, inputDisabled),
            onBackspace: () => _backspace(inputDisabled),
            onClear: () => _clearCode(inputDisabled),
          ),
          const SizedBox(height: 24),

          FilledButton.icon(
            key: const Key('confirm-delivery-submit-button'),
            onPressed: canSubmit
                ? () async {
                    final confirmed = await ref
                        .read(
                          deliveryConfirmationControllerProvider(widget.orderId)
                              .notifier,
                        )
                        .confirmDelivery(
                          secureCode: _codeController.text,
                          currentOrderStatus: detail.status,
                        );
                    if (!confirmed && mounted) {
                      final latest = ref.read(
                        deliveryConfirmationControllerProvider(widget.orderId),
                      );
                      if (latest.remainingAttempts != null &&
                          !latest.isLocked) {
                        _codeController.clear();
                      }
                    }
                  }
                : null,
            icon: state.isSubmitting
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: Colors.white,
                    ),
                  )
                : const Icon(Icons.check_circle_outline),
            label: const Text('Confirm delivery'),
          ),
        ],
      ),
    );
  }

  String _destinationLabel(DeliveryOrderDetailResponseDto detail) {
    final station = detail.destinationStation;
    if (station == null) return '';
    return [
      station.stationCode,
      station.name,
    ].whereType<String>().where((value) => value.isNotEmpty).join(' · ');
  }

  void _goToOrders() {
    final router = GoRouter.maybeOf(context);
    if (router != null) {
      context.go(RoutePaths.customerOrders);
    } else {
      Navigator.of(context).maybePop();
    }
  }

  void _viewOrderHistory(ConfirmPickupResponse? result) {
    final router = GoRouter.maybeOf(context);
    if (Navigator.of(context).canPop()) {
      Navigator.of(context).pop(result);
    } else if (router != null) {
      context.go(
        '${RoutePaths.customerOrders}/${Uri.encodeComponent(widget.orderId)}',
      );
    }
  }
}

class _ErrorAndLockoutBanner extends StatelessWidget {
  const _ErrorAndLockoutBanner({
    required this.state,
    this.isRefreshPending = false,
    this.onRefreshOrder,
  });

  final DeliveryConfirmationState state;
  final bool isRefreshPending;
  final VoidCallback? onRefreshOrder;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tries = state.remainingAttempts;
    final message = state.isLocked
        ? 'Too many attempts. Try again after '
              '${_formatRemainingTime(state.lockoutSecondsRemaining ?? 0)}.'
        : state.errorMessage ??
              'Refreshing order status before confirmation can continue.';

    return Container(
      key: const Key('confirm-delivery-error-banner'),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: theme.colorScheme.errorContainer,
        borderRadius: BorderRadius.circular(10),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(
                state.isLocked ? Icons.lock_clock : Icons.warning_amber_rounded,
                color: theme.colorScheme.onErrorContainer,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  message,
                  style: TextStyle(
                    color: theme.colorScheme.onErrorContainer,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ],
          ),
          if (tries != null && !state.isLocked) ...[
            const SizedBox(height: 6),
            Text(
              'Wrong code. $tries ${tries == 1 ? 'try' : 'tries'} left. (Attempts remaining: $tries)',
              style: TextStyle(
                fontSize: 13,
                color: theme.colorScheme.onErrorContainer,
              ),
            ),
          ],
          if (onRefreshOrder != null) ...[
            const SizedBox(height: 8),
            OutlinedButton.icon(
              key: const Key('refresh-order-status-button'),
              onPressed: isRefreshPending ? null : onRefreshOrder,
              icon: const Icon(Icons.refresh, size: 18),
              label: const Text('Refresh order status'),
            ),
          ],
        ],
      ),
    );
  }

  String _formatRemainingTime(int seconds) {
    final minutes = seconds ~/ 60;
    final remainder = seconds % 60;
    return '${minutes.toString().padLeft(2, '0')}:'
        '${remainder.toString().padLeft(2, '0')}';
  }
}

class _NumericKeypad extends StatelessWidget {
  const _NumericKeypad({
    required this.disabled,
    required this.onDigit,
    required this.onBackspace,
    required this.onClear,
  });

  final bool disabled;
  final ValueChanged<String> onDigit;
  final VoidCallback onBackspace;
  final VoidCallback onClear;

  @override
  Widget build(BuildContext context) {
    Widget digitButton(String digit) => Expanded(
      child: Padding(
        padding: const EdgeInsets.all(4),
        child: OutlinedButton(
          key: Key('keypad-digit-$digit'),
          onPressed: disabled ? null : () => onDigit(digit),
          style: OutlinedButton.styleFrom(
            padding: const EdgeInsets.symmetric(vertical: 14),
          ),
          child: Text(
            digit,
            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w600),
          ),
        ),
      ),
    );

    return Column(
      children: [
        Row(children: [digitButton('1'), digitButton('2'), digitButton('3')]),
        Row(children: [digitButton('4'), digitButton('5'), digitButton('6')]),
        Row(children: [digitButton('7'), digitButton('8'), digitButton('9')]),
        Row(
          children: [
            Expanded(
              child: Padding(
                padding: const EdgeInsets.all(4),
                child: OutlinedButton(
                  key: const Key('keypad-clear'),
                  onPressed: disabled ? null : onClear,
                  style: OutlinedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(vertical: 14),
                  ),
                  child: const Text('Clear'),
                ),
              ),
            ),
            digitButton('0'),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.all(4),
                child: OutlinedButton(
                  key: const Key('keypad-backspace'),
                  onPressed: disabled ? null : onBackspace,
                  style: OutlinedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(vertical: 14),
                  ),
                  child: const Icon(Icons.backspace_outlined),
                ),
              ),
            ),
          ],
        ),
      ],
    );
  }
}

class _DeliveryCompletedView extends StatelessWidget {
  const _DeliveryCompletedView({
    required this.confirmation,
    required this.onBackToOrders,
    required this.onViewOrderHistory,
  });

  final ConfirmPickupResponse confirmation;
  final VoidCallback onBackToOrders;
  final VoidCallback onViewOrderHistory;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final localTime = confirmation.actualDeliveryTime.toLocal();
    final formattedTime =
        '${localTime.hour.toString().padLeft(2, '0')}:'
        '${localTime.minute.toString().padLeft(2, '0')}';

    return Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CircleAvatar(
            radius: 40,
            backgroundColor: Colors.green.shade50,
            child: Icon(
              Icons.check_circle,
              size: 56,
              color: Colors.green.shade700,
            ),
          ),
          const SizedBox(height: 20),
          Text(
            'Delivered',
            textAlign: TextAlign.center,
            style: theme.textTheme.headlineMedium?.copyWith(
              fontWeight: FontWeight.bold,
            ),
          ),
          const SizedBox(height: 8),
          Center(
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
              decoration: BoxDecoration(
                color: Colors.green.shade50,
                borderRadius: BorderRadius.circular(16),
              ),
              child: Text(
                confirmation.status,
                style: TextStyle(
                  color: Colors.green.shade800,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
          ),
          const SizedBox(height: 16),
          Text(
            'Order ${confirmation.trackingNumber} confirmed at $formattedTime.',
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyLarge,
          ),
          const SizedBox(height: 6),
          Text(
            'Thank you for using SmartDroneDelivery.',
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: theme.colorScheme.onSurfaceVariant,
            ),
          ),
          if (confirmation.slotId.isNotEmpty && confirmation.slotReleased) ...[
            const SizedBox(height: 8),
            Text(
              'Slot ${confirmation.slotId} has been released.',
              textAlign: TextAlign.center,
              style: theme.textTheme.bodySmall?.copyWith(
                color: Colors.green.shade700,
              ),
            ),
          ],
          const SizedBox(height: 32),
          FilledButton(
            key: const Key('back-to-orders-button'),
            onPressed: onBackToOrders,
            child: const Text('Back to orders'),
          ),
          const SizedBox(height: 12),
          TextButton(
            key: const Key('view-order-history-button'),
            onPressed: onViewOrderHistory,
            child: const Text('View Order History'),
          ),
        ],
      ),
    );
  }
}

class _IneligibleOrderView extends StatelessWidget {
  const _IneligibleOrderView({
    required this.detail,
    required this.onRefresh,
    required this.onBack,
    this.errorMessage,
  });

  final DeliveryOrderDetailResponseDto detail;
  final String? errorMessage;
  final VoidCallback onRefresh;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.info_outline,
              size: 48,
              color: theme.colorScheme.primary,
            ),
            const SizedBox(height: 16),
            Text(
              'Order not ready for confirmation',
              style: theme.textTheme.titleLarge?.copyWith(
                fontWeight: FontWeight.bold,
              ),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 8),
            Text(
              errorMessage ??
                  'Order ${detail.trackingNumber ?? detail.id} is currently ${detail.status}. Only ARRIVED orders can be confirmed for delivery.',
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 20),
            FilledButton.icon(
              onPressed: onRefresh,
              icon: const Icon(Icons.refresh),
              label: const Text('Refresh status'),
            ),
            const SizedBox(height: 8),
            TextButton(
              onPressed: onBack,
              child: const Text('Back to order details'),
            ),
          ],
        ),
      ),
    );
  }
}

class _AccessLostView extends StatelessWidget {
  const _AccessLostView({required this.message, required this.onBackToOrders});

  final String message;
  final VoidCallback onBackToOrders;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.lock_person_outlined,
              size: 48,
              color: Theme.of(context).colorScheme.error,
            ),
            const SizedBox(height: 16),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: onBackToOrders,
              child: const Text('Back to orders'),
            ),
          ],
        ),
      ),
    );
  }
}
