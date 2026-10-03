import 'package:flutter/material.dart';

import '../config/app_config.dart';
import 'app_colors.dart';

abstract final class AppTheme {
  static ThemeData light(AppFlavor flavor) => _build(flavor, Brightness.light);
  static ThemeData dark(AppFlavor flavor) => _build(flavor, Brightness.dark);

  static ThemeData _build(AppFlavor flavor, Brightness brightness) {
    final scheme = ColorScheme.fromSeed(
      seedColor: AppColors.seedFor(flavor),
      brightness: brightness,
    );
    const radius = BorderRadius.all(Radius.circular(12));

    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      scaffoldBackgroundColor: scheme.surface,
      appBarTheme: AppBarTheme(
        centerTitle: false,
        backgroundColor: scheme.surface,
        foregroundColor: scheme.onSurface,
        elevation: 0,
        scrolledUnderElevation: 1,
      ),
      inputDecorationTheme: const InputDecorationTheme(
        border: OutlineInputBorder(borderRadius: radius),
        contentPadding: EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(52),
          shape: const RoundedRectangleBorder(borderRadius: radius),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size.fromHeight(52),
          shape: const RoundedRectangleBorder(borderRadius: radius),
        ),
      ),
      cardTheme: const CardThemeData(
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(borderRadius: radius),
      ),
    );
  }
}
