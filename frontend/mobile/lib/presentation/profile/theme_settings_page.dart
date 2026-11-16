import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/theme_mode_provider.dart';

/// C27 — Theme Settings
///
/// Lets the customer choose between Light, Dark, or System Default theme.
/// Reads and writes to [themeModeProvider] (ThemeModeNotifier) which is
/// already wired into [MaterialApp.themeMode] in app.dart — no changes to
/// app.dart or bootstrap.dart are required.
class ThemeSettingsPage extends ConsumerWidget {
  const ThemeSettingsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final current = ref.watch(themeModeProvider);
    final theme = Theme.of(context);

    void select(ThemeMode mode) {
      ref.read(themeModeProvider.notifier).set(mode);
    }

    return Scaffold(
      appBar: AppBar(title: const Text('Theme Settings')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 24),
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 540),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'Appearance',
                    style: theme.textTheme.titleSmall?.copyWith(
                      color: theme.colorScheme.primary,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Card(
                    elevation: 0,
                    color: theme.colorScheme.surfaceContainerLow,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Column(
                      children: [
                        RadioListTile<ThemeMode>(
                          key: const Key('theme-light'),
                          title: const Text('Light'),
                          subtitle: const Text('Always use light theme'),
                          secondary: const Icon(Icons.light_mode_outlined),
                          value: ThemeMode.light,
                          groupValue: current,
                          onChanged: (v) => select(v!),
                        ),
                        const Divider(height: 1, indent: 56),
                        RadioListTile<ThemeMode>(
                          key: const Key('theme-dark'),
                          title: const Text('Dark'),
                          subtitle: const Text('Always use dark theme'),
                          secondary: const Icon(Icons.dark_mode_outlined),
                          value: ThemeMode.dark,
                          groupValue: current,
                          onChanged: (v) => select(v!),
                        ),
                        const Divider(height: 1, indent: 56),
                        RadioListTile<ThemeMode>(
                          key: const Key('theme-system'),
                          title: const Text('System Default'),
                          subtitle: const Text(
                            'Follow the device system setting',
                          ),
                          secondary: const Icon(Icons.brightness_auto_outlined),
                          value: ThemeMode.system,
                          groupValue: current,
                          onChanged: (v) => select(v!),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 24),
                  Text(
                    'Your selection is applied immediately and remembered for '
                    'this session. Persistent storage will be added in a future update.',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: theme.colorScheme.onSurfaceVariant,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
