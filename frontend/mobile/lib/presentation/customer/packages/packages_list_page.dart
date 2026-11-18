import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/error/api_exception.dart';
import '../../../core/error/error_message.dart';
import '../../../core/theme/app_colors.dart';
import '../../../data/models/package_model.dart';
import '../../../routes/route_paths.dart';
import '../../common/widgets/empty_state_view.dart';
import 'package_notifier.dart';

class PackagesListPage extends ConsumerStatefulWidget {
  const PackagesListPage({super.key});

  @override
  ConsumerState<PackagesListPage> createState() => _PackagesListPageState();
}

class _PackagesListPageState extends ConsumerState<PackagesListPage> {
  final _searchController = TextEditingController();
  String _filter = 'All';

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final packages = ref.watch(packageNotifierProvider);
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () {
            if (context.canPop()) {
              context.pop();
            } else {
              context.go(RoutePaths.customerHome);
            }
          },
        ),
      ),
      body: packages.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(
                    isNetworkError(error)
                        ? Icons.wifi_off_rounded
                        : Icons.error_outline,
                    size: 48,
                    color: theme.colorScheme.error,
                  ),
                  const SizedBox(height: 16),
                  Text(
                    _packageErrorMessage(error),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 16),
                  FilledButton(
                    style: FilledButton.styleFrom(
                      minimumSize: const Size(120, 48),
                    ),
                    onPressed: () =>
                        ref.read(packageNotifierProvider.notifier).refresh(),
                    child: const Text('Retry'),
                  ),
                ],
              ),
            ),
          ),
        ),
        data: (items) {
          final query = _searchController.text.trim().toLowerCase();
          final filteredItems = items.where((package) {
            final matchesName = package.packageName.toLowerCase().contains(
              query,
            );
            final matchesFragility = switch (_filter) {
              'Fragile' => package.isFragile,
              'Non-fragile' => !package.isFragile,
              _ => true,
            };
            return matchesName && matchesFragility;
          }).toList();

          return Column(
            children: [
              Expanded(
                child: ListView(
                  padding: const EdgeInsets.fromLTRB(20, 8, 20, 20),
                  children: [
                    Text(
                      'My Packages',
                      style: theme.textTheme.headlineMedium?.copyWith(
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      'Manage your saved package details.',
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: AppColors.customerTextSecondary,
                      ),
                    ),
                    const SizedBox(height: 20),
                    TextField(
                      controller: _searchController,
                      onChanged: (_) => setState(() {}),
                      decoration: InputDecoration(
                        hintText: 'Search packages',
                        prefixIcon: const Icon(Icons.search),
                        suffixIcon: _searchController.text.isEmpty
                            ? null
                            : IconButton(
                                tooltip: 'Clear search',
                                onPressed: () {
                                  _searchController.clear();
                                  setState(() {});
                                },
                                icon: const Icon(Icons.close),
                              ),
                      ),
                    ),
                    const SizedBox(height: 12),
                    Wrap(
                      spacing: 8,
                      children: ['All', 'Fragile', 'Non-fragile']
                          .map(
                            (filter) => ChoiceChip(
                              label: Text(filter),
                              selected: _filter == filter,
                              onSelected: (_) =>
                                  setState(() => _filter = filter),
                              selectedColor: AppColors.customerPrimaryContainer,
                              labelStyle: TextStyle(
                                color: _filter == filter
                                    ? AppColors.customerSeed
                                    : AppColors.customerTextSecondary,
                                fontWeight: FontWeight.w600,
                              ),
                              side: BorderSide(
                                color: _filter == filter
                                    ? AppColors.customerSeed
                                    : AppColors.customerBorder,
                              ),
                              showCheckmark: false,
                            ),
                          )
                          .toList(),
                    ),
                    const SizedBox(height: 24),
                    Row(
                      children: [
                        Text(
                          'Saved packages',
                          style: theme.textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        const Spacer(),
                        Text(
                          '${filteredItems.length} '
                          '${filteredItems.length == 1 ? 'package' : 'packages'}',
                          style: theme.textTheme.bodySmall?.copyWith(
                            color: AppColors.customerTextSecondary,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 12),
                    if (filteredItems.isEmpty)
                      EmptyStateView(
                        title: items.isEmpty
                            ? 'No packages'
                            : 'No matching packages',
                        icon: Icons.inventory_2_outlined,
                      )
                    else
                      for (final package in filteredItems) ...[
                        _PackageCard(package: package),
                        const SizedBox(height: 12),
                      ],
                  ],
                ),
              ),
            ],
          );
        },
      ),
      bottomNavigationBar: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(20, 12, 20, 12),
              child: FilledButton.icon(
                onPressed: () => context.push(RoutePaths.customerPackagesAdd),
                icon: const Icon(Icons.add),
                label: const Text('Add Package'),
              ),
            ),
          ),
          NavigationBar(
            selectedIndex: 2,
            onDestinationSelected: (index) {
              switch (index) {
                case 0:
                  context.push(RoutePaths.customerHome);
                case 1:
                  context.push(RoutePaths.customerOrders);
                case 2:
                  break;
                case 3:
                  context.push(RoutePaths.profile);
              }
            },
            destinations: const [
              NavigationDestination(
                icon: Icon(Icons.home_outlined),
                selectedIcon: Icon(Icons.home),
                label: 'Home',
              ),
              NavigationDestination(
                icon: Icon(Icons.receipt_long_outlined),
                label: 'Orders',
              ),
              NavigationDestination(
                icon: Icon(Icons.inventory_2_outlined),
                label: 'Packages',
              ),
              NavigationDestination(
                icon: Icon(Icons.person_outline),
                label: 'Profile',
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _PackageCard extends ConsumerWidget {
  const _PackageCard({required this.package});

  final PackageModel package;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isLoading = ref.watch(packageNotifierProvider).isLoading;
    final theme = Theme.of(context);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (package.packageImageUrl?.isNotEmpty == true) ...[
              Image.network(
                package.packageImageUrl!,
                height: 140,
                width: double.infinity,
                fit: BoxFit.cover,
                errorBuilder: (_, _, _) =>
                    const Text('Package photo unavailable'),
              ),
              const SizedBox(height: 12),
            ],
            Row(
              children: [
                Container(
                  width: 44,
                  height: 44,
                  decoration: BoxDecoration(
                    color: AppColors.customerPrimaryContainer,
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: const Icon(
                    Icons.inventory_2_outlined,
                    color: AppColors.customerSeed,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    package.packageName,
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 18),
            Row(
              children: [
                Expanded(
                  child: _PackageMetric(
                    label: 'Weight',
                    value: '${_display(package.weightGram)} g',
                  ),
                ),
                Expanded(
                  child: _PackageMetric(
                    label: 'Dimensions',
                    value:
                        '${_display(package.lengthCm)} × '
                        '${_display(package.widthCm)} × '
                        '${_display(package.heightCm)} cm',
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            const Divider(height: 1, color: AppColors.customerBorder),
            const SizedBox(height: 8),
            Row(
              children: [
                _FragilityBadge(isFragile: package.isFragile),
                const Spacer(),
                IconButton(
                  tooltip: 'Edit',
                  visualDensity: VisualDensity.compact,
                  onPressed: isLoading
                      ? null
                      : () => context.push(
                          '${RoutePaths.customerPackagesEdit}?id=${Uri.encodeComponent(package.id)}',
                        ),
                  icon: const Icon(
                    Icons.edit_outlined,
                    color: AppColors.customerSeed,
                  ),
                ),
                IconButton(
                  tooltip: 'Delete',
                  visualDensity: VisualDensity.compact,
                  onPressed: isLoading
                      ? null
                      : () => _confirmDelete(context, ref),
                  icon: const Icon(
                    Icons.delete_outline,
                    color: AppColors.customerTextSecondary,
                  ),
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

class _PackageMetric extends StatelessWidget {
  const _PackageMetric({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text(
        label,
        style: Theme.of(context).textTheme.bodySmall
            ?.copyWith(color: AppColors.customerTextSecondary),
      ),
      const SizedBox(height: 4),
      Text(
        value,
        style: Theme.of(context).textTheme.bodyMedium
            ?.copyWith(fontWeight: FontWeight.w700),
      ),
    ],
  );
}

class _FragilityBadge extends StatelessWidget {
  const _FragilityBadge({required this.isFragile});

  final bool isFragile;

  @override
  Widget build(BuildContext context) {
    final color = isFragile ? AppColors.customerSeed : const Color(0xFF475569);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: isFragile ? const Color(0xFFEFF6FF) : const Color(0xFFF1F5F9),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        isFragile ? 'Fragile' : 'Non-fragile',
        style: TextStyle(
          color: color,
          fontSize: 12,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}

String _packageErrorMessage(Object error) {
  if (error is ApiException &&
      (error.statusCode == 409 || error.code == 'MSG-P-CO-12')) {
    return 'This package is in use and cannot be deleted.';
  }
  return errorMessage(error);
}
