import 'package:flutter/material.dart';

class AiAssistantPage extends StatefulWidget {
  const AiAssistantPage({this.orderId, this.trackingNumber, super.key});

  final String? orderId;
  final String? trackingNumber;

  @override
  State<AiAssistantPage> createState() => _AiAssistantPageState();
}

class _AiAssistantPageState extends State<AiAssistantPage> {
  static const _prompts = [
    'Where is my order?',
    'What does my order status mean?',
    'How do I collect my delivery?',
  ];

  final _messageController = TextEditingController();
  final _messages = <({bool fromCustomer, String text})>[];

  @override
  void dispose() {
    _messageController.dispose();
    super.dispose();
  }

  void _send([String? prompt]) {
    final question = (prompt ?? _messageController.text).trim();
    if (question.isEmpty) return;
    setState(() {
      _messages
        ..add((fromCustomer: true, text: question))
        ..add((fromCustomer: false, text: _localReply(question)));
      _messageController.clear();
    });
  }

  String _localReply(String question) {
    final contextCodes = {
      if (widget.orderId?.isNotEmpty == true) widget.orderId!.toUpperCase(),
      if (widget.trackingNumber?.isNotEmpty == true)
        widget.trackingNumber!.toUpperCase(),
    };
    final codePattern = RegExp(
      r'\b[A-Z0-9]{2,}(?:-[A-Z0-9]{2,})+\b',
      caseSensitive: false,
    );
    final includesForeignCode = codePattern
        .allMatches(question)
        .any((match) => !contextCodes.contains(match.group(0)!.toUpperCase()));
    final asksAboutSomeoneElse = RegExp(
      r"\b(another customer|other customer|someone else's|their order|not my order)\b",
      caseSensitive: false,
    ).hasMatch(question);
    if (includesForeignCode || asksAboutSomeoneElse) {
      return "I can’t access or discuss another customer’s tracking information. "
          'For privacy, ask about your own order instead.';
    }

    final normalizedQuestion = question.toLowerCase();
    if (normalizedQuestion.contains('where') ||
        normalizedQuestion.contains('track') ||
        normalizedQuestion.contains('status')) {
      if (widget.trackingNumber != null || widget.orderId != null) {
        return 'This local demo has your order context '
            '(${widget.trackingNumber ?? widget.orderId}). '
            'Check the order tracking screen for its latest status.';
      }
      return 'This assistant is running in local simulation mode. '
          'Open one of your orders and ask again with its context to get '
          'order-specific guidance.';
    }
    if (normalizedQuestion.contains('collect') ||
        normalizedQuestion.contains('pickup')) {
      return 'When your order reaches ARRIVED, open its details and enter '
          'the pickup code to complete collection.';
    }
    return 'I can help with delivery status, tracking, and pickup guidance. '
        'This response is generated locally for the demo.';
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('AI Assistant')),
    body: Column(
      children: [
        if (widget.orderId != null || widget.trackingNumber != null)
          ListTile(
            leading: const Icon(Icons.local_shipping_outlined),
            title: const Text('Order context'),
            subtitle: Text(widget.trackingNumber ?? widget.orderId!),
          ),
        if (_messages.isEmpty) ...[
          const Padding(
            padding: EdgeInsets.fromLTRB(16, 20, 16, 8),
            child: Text(
              'Ask about your delivery or choose a suggestion. '
              'Replies are simulated locally.',
              textAlign: TextAlign.center,
            ),
          ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12),
            child: Wrap(
              spacing: 8,
              runSpacing: 4,
              alignment: WrapAlignment.center,
              children: [
                for (var index = 0; index < _prompts.length; index++)
                  ActionChip(
                    key: Key('assistant-prompt-$index'),
                    label: Text(_prompts[index]),
                    onPressed: () => _send(_prompts[index]),
                  ),
              ],
            ),
          ),
        ],
        Expanded(
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              for (final message in _messages)
                Align(
                  alignment: message.fromCustomer
                      ? Alignment.centerRight
                      : Alignment.centerLeft,
                  child: Card(
                    color: message.fromCustomer
                        ? Theme.of(context).colorScheme.primaryContainer
                        : Theme.of(context).colorScheme.surfaceContainerHighest,
                    child: Padding(
                      padding: const EdgeInsets.all(12),
                      child: Text(message.text),
                    ),
                  ),
                ),
            ],
          ),
        ),
        SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(12, 4, 12, 12),
            child: Row(
              children: [
                Expanded(
                  child: TextField(
                    key: const Key('assistant-message-input'),
                    controller: _messageController,
                    textInputAction: TextInputAction.send,
                    onSubmitted: (_) => _send(),
                    decoration: const InputDecoration(
                      hintText: 'Ask a delivery question',
                      border: OutlineInputBorder(),
                    ),
                  ),
                ),
                IconButton(
                  key: const Key('assistant-send-button'),
                  tooltip: 'Send',
                  onPressed: _send,
                  icon: const Icon(Icons.send),
                ),
              ],
            ),
          ),
        ),
      ],
    ),
  );
}
