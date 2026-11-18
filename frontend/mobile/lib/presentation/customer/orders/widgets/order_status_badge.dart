import 'package:flutter/material.dart';

class OrderStatusBadge extends StatelessWidget {
  const OrderStatusBadge({required this.status, super.key});

  final String status;

  @override
  Widget build(BuildContext context) {
    final normalized = status.toUpperCase();
    final (backgroundColor, foregroundColor) = switch (normalized) {
      'IN_TRANSIT' => (const Color(0xFFEFF6FF), const Color(0xFF1E56A0)),
      'PENDING' => (const Color(0xFFFEF3C7), const Color(0xFFB45309)),
      'ARRIVED' => (const Color(0xFF1E56A0), Colors.white),
      'DELIVERED' => (const Color(0xFFDCFCE7), const Color(0xFF15803D)),
      'CANCELLED' => (const Color(0xFFF1F5F9), const Color(0xFF475569)),
      _ => (const Color(0xFFF1F5F9), const Color(0xFF475569)),
    };
    final label = status
        .toLowerCase()
        .split('_')
        .map(
          (word) => word.isEmpty
              ? word
              : '${word[0].toUpperCase()}${word.substring(1)}',
        )
        .join(' ');

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: backgroundColor,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: foregroundColor,
          fontSize: 12,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}
