import 'package:flutter/material.dart';

/// C23 — AI Assistant
///
/// ⚠️  PROPOSED API CONTRACT PREVIEW — Backend AI assistant endpoints are
/// NOT yet deployed. This screen operates entirely in local simulation mode
/// and MUST NOT make any HTTP calls to `/api/ai-assistant/*` or any AI
/// endpoint. The backend contracts listed below are proposed baselines only:
///   • POST /api/ai-assistant/sessions        (proposed, not implemented)
///   • GET  /api/ai-assistant/sessions/{id}/messages (proposed, not implemented)
///   • POST /api/ai-assistant/sessions/{id}/messages (proposed, not implemented)
///
/// The page receives optional [orderId] / [trackingNumber] when opened from
/// a specific order context (C06/C07/C08 or C24 profile).
class AiAssistantPage extends StatefulWidget {
  const AiAssistantPage({super.key, this.orderId, this.trackingNumber});

  final String? orderId;
  final String? trackingNumber;

  @override
  State<AiAssistantPage> createState() => _AiAssistantPageState();
}

class _AiAssistantPageState extends State<AiAssistantPage> {
  final _controller = TextEditingController();
  final _scrollController = ScrollController();
  final List<_ChatMessage> _messages = [];

  static const _quickChips = [
    'What is the current delivery status?',
    'How do I confirm pickup with OTP?',
    'Why can\'t I cancel my order?',
    'How long does delivery take?',
    'What happens if I miss the pickup window?',
  ];

  @override
  void initState() {
    super.initState();
    // Greeting message
    _messages.add(
      _ChatMessage(
        role: _Role.bot,
        text:
            'Hello! I\'m your SmartDroneDelivery assistant. I can help you '
            'track your deliveries, answer questions about pickup codes, '
            'cancellation policies, and more.\n\n'
            'Please note: I can only provide information about your own '
            'delivery orders.${widget.trackingNumber != null ? '\n\nIt looks like you\'re asking about order ${widget.trackingNumber}.' : ''}',
      ),
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  void _send(String text) {
    final trimmed = text.trim();
    if (trimmed.isEmpty) return;

    setState(() {
      _messages.add(_ChatMessage(role: _Role.user, text: trimmed));
      _controller.clear();
    });

    // Scroll to bottom
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scrollController.hasClients) {
        _scrollController.animateTo(
          _scrollController.position.maxScrollExtent,
          duration: const Duration(milliseconds: 300),
          curve: Curves.easeOut,
        );
      }
    });

    // Local simulated response — no HTTP call made
    final response = _localReply(trimmed);
    Future.delayed(const Duration(milliseconds: 400), () {
      if (!mounted) return;
      setState(() {
        _messages.add(_ChatMessage(role: _Role.bot, text: response));
      });
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (_scrollController.hasClients) {
          _scrollController.animateTo(
            _scrollController.position.maxScrollExtent,
            duration: const Duration(milliseconds: 300),
            curve: Curves.easeOut,
          );
        }
      });
    });
  }

  /// Local rule-based reply — never calls any network endpoint.
  /// Enforces data-privacy rule: refuses questions about another customer's order.
  String _localReply(String input) {
    final lower = input.toLowerCase();

    // Privacy guard: reject access to another customer's order data
    if (_mentionsOtherOrder(lower)) {
      return 'I can only provide information about your own delivery orders. '
          'Please sign in with the account associated with the order you\'re enquiring about.';
    }

    if (lower.contains('status') || lower.contains('track')) {
      if (widget.trackingNumber != null) {
        return 'For order ${widget.trackingNumber}, please check the Order Details '
            'screen for the most up-to-date status. Live status is updated automatically.';
      }
      return 'You can check the status of any order from the My Orders screen. '
          'Tap an order to see its full details and live tracking information.';
    }

    if (lower.contains('otp') ||
        lower.contains('pickup code') ||
        lower.contains('confirm pickup') ||
        lower.contains('pickup with')) {
      return 'When your package arrives at the destination station, '
          'go to Order Details and tap "Enter pickup code". '
          'Enter the 6-digit code shown on the Order Submitted confirmation screen. '
          'You have up to 3 attempts; after that the code is locked for 10 minutes.';
    }

    if (lower.contains('cancel')) {
      return 'You can cancel an order while its status is PENDING or APPROVED. '
          'From the Order Details screen, tap "Cancel order" and provide a reason. '
          'Once the order reaches READY_FOR_TAKEOFF or later, cancellation is no longer '
          'available through the app — please contact your dispatcher.';
    }

    if (lower.contains('how long') ||
        lower.contains('duration') ||
        lower.contains('eta')) {
      return 'Delivery duration depends on the route, weather, and drone availability. '
          'An estimated arrival time (ETA) is shown in Order Details once the delivery '
          'is in transit. ETA values are estimates and may change.';
    }

    if (lower.contains('miss') ||
        lower.contains('expire') ||
        lower.contains('collection period')) {
      return 'If you miss the collection window, the package may be held at the station '
          'for a limited time. Contact station staff or the dispatcher for assistance. '
          'Check your notifications for any expiry alerts.';
    }

    if (lower.contains('address') || lower.contains('recipient')) {
      return 'You can manage your saved delivery addresses from Profile & Settings → '
          'Delivery Addresses. These addresses can be reused when creating new orders.';
    }

    if (lower.contains('package') ||
        lower.contains('weight') ||
        lower.contains('dimension')) {
      return 'Saved package templates are available from Profile & Settings → My Packages. '
          'Each package stores name, weight, dimensions, and a fragile flag. '
          'Packages attached to unfinished orders cannot be deleted.';
    }

    return 'I\'m here to help with your delivery questions! You can ask me about '
        'order status, pickup codes, cancellation, delivery addresses, or package management. '
        'For account or billing issues, please contact our support team.';
  }

  /// Heuristic: input mentions a tracking number pattern that differs from the
  /// current context order, suggesting the user is asking about another order.
  bool _mentionsOtherOrder(String lower) {
    // Simple pattern: "DD-" tracking number mentioned while we have a context order
    // and it differs from the current one.
    if (widget.trackingNumber == null) return false;
    final ddPattern = RegExp(r'dd-\d{6}-\d{4}');
    final matches = ddPattern.allMatches(lower);
    for (final m in matches) {
      final mentioned = lower.substring(m.start, m.end).toUpperCase();
      if (mentioned != widget.trackingNumber!.toUpperCase()) {
        return true;
      }
    }
    return false;
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Scaffold(
      resizeToAvoidBottomInset: true,
      appBar: AppBar(
        title: const Text('AI Assistant'),
        leading: IconButton(
          tooltip: 'Back',
          icon: const Icon(Icons.arrow_back),
          onPressed: () {
            if (Navigator.of(context).canPop()) {
              Navigator.of(context).pop();
            }
          },
        ),
      ),
      body: Column(
        children: [
          // ── Proposed-contract banner ──────────────────────────────────────
          Material(
            color: colorScheme.tertiaryContainer,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(
                    Icons.info_outline,
                    size: 18,
                    color: colorScheme.onTertiaryContainer,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'Proposed API Contract Preview — Backend AI assistant endpoints '
                      'not yet deployed. Responses are simulated locally.',
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: colorScheme.onTertiaryContainer,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),

          // ── Chat messages ─────────────────────────────────────────────────
          Expanded(
            child: ListView.builder(
              key: const Key('ai-chat-list'),
              controller: _scrollController,
              padding: const EdgeInsets.all(16),
              itemCount: _messages.length,
              itemBuilder: (context, index) {
                final msg = _messages[index];
                return _BubbleTile(message: msg);
              },
            ),
          ),

          // ── Quick suggestion chips ────────────────────────────────────────
          SizedBox(
            height: 44,
            child: ListView.separated(
              key: const Key('ai-quick-chips'),
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 16),
              itemCount: _quickChips.length,
              separatorBuilder: (_, _) => const SizedBox(width: 8),
              itemBuilder: (context, index) {
                final chip = _quickChips[index];
                return ActionChip(
                  label: Text(chip),
                  onPressed: () => _send(chip),
                );
              },
            ),
          ),
          const SizedBox(height: 8),

          // ── Message input ─────────────────────────────────────────────────
          SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
              child: Row(
                children: [
                  Expanded(
                    child: TextField(
                      key: const Key('ai-message-input'),
                      controller: _controller,
                      minLines: 1,
                      maxLines: 4,
                      textInputAction: TextInputAction.send,
                      decoration: const InputDecoration(
                        hintText: 'Ask a question…',
                        border: OutlineInputBorder(),
                        contentPadding: EdgeInsets.symmetric(
                          horizontal: 16,
                          vertical: 12,
                        ),
                      ),
                      onSubmitted: _send,
                    ),
                  ),
                  const SizedBox(width: 8),
                  FilledButton(
                    key: const Key('ai-send-button'),
                    style: FilledButton.styleFrom(
                      minimumSize: const Size(52, 52),
                      padding: EdgeInsets.zero,
                    ),
                    onPressed: () => _send(_controller.text),
                    child: const Icon(Icons.send),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

enum _Role { user, bot }

class _ChatMessage {
  const _ChatMessage({required this.role, required this.text});
  final _Role role;
  final String text;
}

class _BubbleTile extends StatelessWidget {
  const _BubbleTile({required this.message});
  final _ChatMessage message;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final isUser = message.role == _Role.user;

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Row(
        mainAxisAlignment: isUser
            ? MainAxisAlignment.end
            : MainAxisAlignment.start,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          if (!isUser) ...[
            CircleAvatar(
              radius: 14,
              backgroundColor: colorScheme.secondaryContainer,
              child: Icon(
                Icons.smart_toy_outlined,
                size: 16,
                color: colorScheme.onSecondaryContainer,
              ),
            ),
            const SizedBox(width: 8),
          ],
          Flexible(
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
              decoration: BoxDecoration(
                color: isUser
                    ? colorScheme.primary
                    : colorScheme.surfaceContainerHighest,
                borderRadius: BorderRadius.only(
                  topLeft: const Radius.circular(16),
                  topRight: const Radius.circular(16),
                  bottomLeft: Radius.circular(isUser ? 16 : 4),
                  bottomRight: Radius.circular(isUser ? 4 : 16),
                ),
              ),
              child: Text(
                message.text,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: isUser ? colorScheme.onPrimary : colorScheme.onSurface,
                ),
              ),
            ),
          ),
          if (isUser) const SizedBox(width: 8),
        ],
      ),
    );
  }
}
