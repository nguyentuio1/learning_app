import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/theme_mode_provider.dart';
import '../../routes/route_paths.dart';

class ThemeSettingsPage extends ConsumerWidget {
  const ThemeSettingsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final selectedMode = ref.watch(themeModeProvider);
    return Scaffold(
      appBar: AppBar(
        title: const Text('Theme settings'),
        leading: IconButton(
          tooltip: 'Back',
          icon: const Icon(Icons.arrow_back),
          onPressed: () {
            if (context.canPop()) {
              context.pop();
            } else {
              context.go(RoutePaths.profile);
            }
          },
        ),
      ),
      body: RadioGroup<ThemeMode>(
        groupValue: selectedMode,
        onChanged: (mode) {
          if (mode != null) ref.read(themeModeProvider.notifier).set(mode);
        },
        child: const Column(
          children: [
            RadioListTile<ThemeMode>(
              key: Key('theme-system'),
              value: ThemeMode.system,
              title: Text('System Default'),
              subtitle: Text('Follow your device appearance setting'),
            ),
            RadioListTile<ThemeMode>(
              key: Key('theme-light'),
              value: ThemeMode.light,
              title: Text('Light'),
            ),
            RadioListTile<ThemeMode>(
              key: Key('theme-dark'),
              value: ThemeMode.dark,
              title: Text('Dark'),
            ),
          ],
        ),
      ),
    );
  }
}
