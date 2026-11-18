import 'package:flutter/material.dart';

import '../config/app_config.dart';
import 'app_colors.dart';

abstract final class AppTheme {
  static ThemeData light(AppFlavor flavor) => _build(flavor, Brightness.light);
  static ThemeData dark(AppFlavor flavor) => _build(flavor, Brightness.dark);

  static ThemeData _build(AppFlavor flavor, Brightness brightness) {
    final seedScheme = ColorScheme.fromSeed(
      seedColor: AppColors.seedFor(flavor),
      brightness: brightness,
    );
    final isCustomerLight =
        flavor == AppFlavor.customer && brightness == Brightness.light;
    final scheme = isCustomerLight
        ? seedScheme.copyWith(
            primary: AppColors.customerSeed,
            onPrimary: Colors.white,
            primaryContainer: AppColors.customerPrimaryContainer,
            onPrimaryContainer: AppColors.customerSeed,
            surface: AppColors.customerSurface,
            onSurface: AppColors.customerTextPrimary,
            onSurfaceVariant: AppColors.customerTextSecondary,
            outlineVariant: AppColors.customerBorder,
          )
        : seedScheme;
    const fieldRadius = BorderRadius.all(Radius.circular(12));
    const cardRadius = BorderRadius.all(Radius.circular(16));

    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      scaffoldBackgroundColor: isCustomerLight
          ? AppColors.customerBackground
          : scheme.surface,
      appBarTheme: AppBarTheme(
        centerTitle: false,
        backgroundColor: scheme.surface,
        foregroundColor: scheme.onSurface,
        elevation: 0,
        scrolledUnderElevation: 1,
      ),
      inputDecorationTheme: const InputDecorationTheme(
        border: OutlineInputBorder(
          borderRadius: fieldRadius,
          borderSide: BorderSide(color: AppColors.customerBorder),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: fieldRadius,
          borderSide: BorderSide(color: AppColors.customerBorder),
        ),
        contentPadding: EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(52),
          shape: const RoundedRectangleBorder(borderRadius: fieldRadius),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size.fromHeight(52),
          shape: const RoundedRectangleBorder(borderRadius: fieldRadius),
        ),
      ),
      cardTheme: CardThemeData(
        elevation: 0,
        margin: EdgeInsets.zero,
        color: scheme.surface,
        shape: RoundedRectangleBorder(
          borderRadius: cardRadius,
          side: BorderSide(color: scheme.outlineVariant),
        ),
      ),
    );
  }
}
