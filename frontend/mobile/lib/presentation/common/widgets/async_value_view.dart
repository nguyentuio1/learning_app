import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/error_message.dart';

/// Hiển thị chuẩn hoá cho `AsyncValue<T>`: loading / lỗi (có Retry) / dữ liệu.
///
///   AsyncValueView(
///     value: ref.watch(stationsProvider),
///     onRetry: () => ref.invalidate(stationsProvider),
///     data: (stations) => StationList(stations),
///   )
class AsyncValueView<T> extends StatelessWidget {
  const AsyncValueView({
    super.key,
    required this.value,
    required this.data,
    this.onRetry,
    this.loading,
    this.isEmpty,
    this.emptyBuilder,
  });

  final AsyncValue<T> value;
  final Widget Function(T data) data;
  final VoidCallback? onRetry;
  final Widget? loading;
  final bool Function(T data)? isEmpty;
  final WidgetBuilder? emptyBuilder;

  @override
  Widget build(BuildContext context) {
    if (value.hasError) {
      return _ErrorView(error: value.error!, onRetry: onRetry);
    }

    return value.when(
      skipError: false,
      data: (d) {
        if (isEmpty != null && isEmpty!(d) && emptyBuilder != null) {
          return emptyBuilder!(context);
        }
        return data(d);
      },
      loading: () =>
          loading ?? const Center(child: CircularProgressIndicator()),
      error: (e, _) => _ErrorView(error: e, onRetry: onRetry),
    );
  }
}

class _ErrorView extends StatelessWidget {
  const _ErrorView({required this.error, this.onRetry});

  final Object error;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final isNetwork = isNetworkError(error);

    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 420),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(
                isNetwork ? Icons.wifi_off_rounded : Icons.error_outline,
                size: 48,
                color: colorScheme.error,
              ),
              const SizedBox(height: 16),
              Text(
                errorMessage(error),
                textAlign: TextAlign.center,
                style: theme.textTheme.bodyMedium,
              ),
              if (onRetry != null) ...[
                const SizedBox(height: 24),
                OutlinedButton(
                  style: OutlinedButton.styleFrom(
                    minimumSize: const Size(120, 48),
                  ),
                  onPressed: onRetry,
                  child: const Text('Retry'),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
