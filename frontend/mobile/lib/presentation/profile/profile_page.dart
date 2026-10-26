import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/session/auth_controller.dart';
import '../../domain/entities/user.dart';
import '../../routes/route_paths.dart';
import '../common/widgets/async_value_view.dart';
import 'profile_controller.dart';
import 'widgets/avatar_widget.dart';

class ProfilePage extends ConsumerWidget {
  const ProfilePage({super.key});

  String _formatRole(String roleCode) {
    switch (roleCode.toUpperCase()) {
      case 'CUSTOMER':
        return 'Customer';
      case 'STATION_OPERATOR':
        return 'Station Operator';
      case 'DISPATCHER':
        return 'Dispatcher';
      case 'MANAGER':
        return 'Logistics Manager';
      case 'ADMIN':
        return 'System Administrator';
      default:
        return roleCode;
    }
  }

  String _formatDate(DateTime date) {
    return '${date.day.toString().padLeft(2, '0')}/${date.month.toString().padLeft(2, '0')}/${date.year}';
  }

  Future<void> _confirmSignOut(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Sign out'),
        content: const Text('Are you sure you want to sign out of your account?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(ctx).pop(true),
            child: const Text('Sign out'),
          ),
        ],
      ),
    );

    if (confirmed == true) {
      await ref.read(authControllerProvider.notifier).signOut();
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profileAsync = ref.watch(profileControllerProvider);
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Profile & Settings'),
        actions: [
          IconButton(
            tooltip: 'Refresh',
            icon: const Icon(Icons.refresh),
            onPressed: () =>
                ref.read(profileControllerProvider.notifier).reload(),
          ),
        ],
      ),
      body: SafeArea(
        child: AsyncValueView<User>(
          value: profileAsync,
          onRetry: () =>
              ref.read(profileControllerProvider.notifier).reload(),
          data: (user) => RefreshIndicator(
            onRefresh: () =>
                ref.read(profileControllerProvider.notifier).reload(),
            child: SingleChildScrollView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 24),
              child: Center(
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 540),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      // Header Card with Avatar & Info
                      Card(
                        elevation: 1,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(16),
                        ),
                        child: Padding(
                          padding: const EdgeInsets.all(24),
                          child: Column(
                            children: [
                              AvatarWidget(
                                avatarUrl: user.avatarUrl,
                                name: user.fullName,
                                radius: 46,
                                onTap: () => context.push(RoutePaths.editProfile),
                              ),
                              const SizedBox(height: 16),
                              Text(
                                user.fullName,
                                style: theme.textTheme.titleLarge?.copyWith(
                                  fontWeight: FontWeight.bold,
                                ),
                                textAlign: TextAlign.center,
                              ),
                              const SizedBox(height: 4),
                              Text(
                                user.email,
                                style: theme.textTheme.bodyMedium?.copyWith(
                                  color: theme.colorScheme.onSurfaceVariant,
                                ),
                              ),
                              const SizedBox(height: 12),
                              Wrap(
                                spacing: 8,
                                alignment: WrapAlignment.center,
                                children: [
                                  Chip(
                                    avatar: const Icon(Icons.badge_outlined, size: 16),
                                    label: Text(_formatRole(user.roleCode)),
                                    padding: EdgeInsets.zero,
                                    visualDensity: VisualDensity.compact,
                                  ),
                                  Chip(
                                    avatar: Icon(
                                      Icons.circle,
                                      size: 10,
                                      color: user.status.toUpperCase() == 'ACTIVE'
                                          ? Colors.green
                                          : Colors.grey,
                                    ),
                                    label: Text(user.status),
                                    padding: EdgeInsets.zero,
                                    visualDensity: VisualDensity.compact,
                                  ),
                                ],
                              ),
                            ],
                          ),
                        ),
                      ),
                      const SizedBox(height: 20),

                      // Account Details Section
                      Text(
                        'Account Information',
                        style: theme.textTheme.titleSmall?.copyWith(
                          color: theme.colorScheme.primary,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      const SizedBox(height: 8),
                      Card(
                        elevation: 0,
                        color: theme.colorScheme.surfaceVariant.withOpacity(0.35),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Column(
                          children: [
                            ListTile(
                              leading: const Icon(Icons.person_outline),
                              title: const Text('Full Name'),
                              subtitle: Text(user.fullName),
                            ),
                            const Divider(height: 1, indent: 56),
                            ListTile(
                              leading: const Icon(Icons.phone_outlined),
                              title: const Text('Phone Number'),
                              subtitle: Text(user.phoneNumber),
                            ),
                            const Divider(height: 1, indent: 56),
                            ListTile(
                              leading: const Icon(Icons.email_outlined),
                              title: const Text('Email Address'),
                              subtitle: Text(user.email),
                              trailing: const Icon(Icons.lock_outline, size: 18),
                            ),
                            const Divider(height: 1, indent: 56),
                            ListTile(
                              leading: const Icon(Icons.calendar_today_outlined),
                              title: const Text('Member Since'),
                              subtitle: Text(_formatDate(user.createdAt)),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 24),

                      // Settings & Security Section
                      Text(
                        'Settings & Security',
                        style: theme.textTheme.titleSmall?.copyWith(
                          color: theme.colorScheme.primary,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      const SizedBox(height: 8),
                      Card(
                        elevation: 0,
                        color: theme.colorScheme.surfaceVariant.withOpacity(0.35),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Column(
                          children: [
                            ListTile(
                              leading: const Icon(Icons.edit_outlined),
                              title: const Text('Edit Profile'),
                              subtitle: const Text('Update your name and phone number'),
                              trailing: const Icon(Icons.chevron_right),
                              onTap: () => context.push(RoutePaths.editProfile),
                            ),
                            const Divider(height: 1, indent: 56),
                            ListTile(
                              leading: const Icon(Icons.lock_reset_outlined),
                              title: const Text('Change Password'),
                              subtitle: const Text('Update your login password'),
                              trailing: const Icon(Icons.chevron_right),
                              onTap: () => context.push(RoutePaths.changePassword),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 28),

                      // Sign Out Button
                      OutlinedButton.icon(
                        style: OutlinedButton.styleFrom(
                          foregroundColor: theme.colorScheme.error,
                          side: BorderSide(color: theme.colorScheme.error),
                          padding: const EdgeInsets.symmetric(vertical: 14),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(12),
                          ),
                        ),
                        icon: const Icon(Icons.logout),
                        label: const Text('Sign out'),
                        onPressed: () => _confirmSignOut(context, ref),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
