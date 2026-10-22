import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/error/api_exception.dart';
import '../../../core/error/error_message.dart';
import '../../../data/models/package_model.dart';
import '../../../routes/route_paths.dart';
import 'package_notifier.dart';

class PackagesListPage extends ConsumerWidget {
  const PackagesListPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final packages = ref.watch(packageNotifierProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Manage packages')),
      body: packages.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(_packageErrorMessage(error)),
              const SizedBox(height: 12),
              FilledButton(
                onPressed: () =>
                    ref.read(packageNotifierProvider.notifier).refresh(),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (items) => items.isEmpty
            ? const Center(child: Text('No packages'))
            : ListView.separated(
                padding: const EdgeInsets.all(16),
                itemCount: items.length,
                separatorBuilder: (_, _) => const SizedBox(height: 12),
                itemBuilder: (context, index) =>
                    _PackageCard(package: items[index]),
              ),
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => context.push(RoutePaths.customerPackagesAdd),
        icon: const Icon(Icons.add),
        label: const Text('Add package'),
      ),
    );
  }
}

class _PackageCard extends ConsumerWidget {
  const _PackageCard({required this.package});

  final Package package;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isLoading = ref.watch(packageNotifierProvider).isLoading;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    package.packageName,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                if (package.isFragile)
                  const Chip(
                    avatar: Icon(Icons.warning_amber, size: 18),
                    label: Text('Dễ vỡ'),
                  ),
              ],
            ),
            const SizedBox(height: 8),
            Text('Weight: ${_display(package.weightGram)} g'),
            Text(
              'Dimensions: ${_display(package.lengthCm)} × '
              '${_display(package.widthCm)} × ${_display(package.heightCm)} cm',
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              children: [
                OutlinedButton.icon(
                  onPressed: isLoading
                      ? null
                      : () => context.push(
                          '${RoutePaths.customerPackagesEdit}?id=${Uri.encodeComponent(package.id)}',
                        ),
                  icon: const Icon(Icons.edit),
                  label: const Text('Edit'),
                ),
                TextButton.icon(
                  onPressed: isLoading
                      ? null
                      : () => _confirmDelete(context, ref),
                  icon: const Icon(Icons.delete_outline),
                  label: const Text('Delete'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  String _display(double value) =>
      value == value.roundToDouble() ? value.toInt().toString() : '$value';

  Future<void> _confirmDelete(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        content: const Text('Delete this package?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (confirmed == true && context.mounted) {
      await ref
          .read(packageNotifierProvider.notifier)
          .deletePackage(package.id);
    }
  }
}

String _packageErrorMessage(Object error) {
  if (error is ApiException &&
      (error.statusCode == 409 || error.code == 'MSG-P-CO-12')) {
    return 'This package is in use and cannot be deleted.';
  }
  return errorMessage(error);
}
