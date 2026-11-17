enum AiAssistantOutcome {
  answered,
  clarificationRequired,
  help,
  refused;

  static AiAssistantOutcome parse(String value) => switch (value.trim()) {
    'ANSWERED' => AiAssistantOutcome.answered,
    'CLARIFICATION_REQUIRED' => AiAssistantOutcome.clarificationRequired,
    'HELP' => AiAssistantOutcome.help,
    'REFUSED' => AiAssistantOutcome.refused,
    _ => throw FormatException('Unknown assistant outcome: $value'),
  };

  String get apiValue => switch (this) {
    AiAssistantOutcome.answered => 'ANSWERED',
    AiAssistantOutcome.clarificationRequired => 'CLARIFICATION_REQUIRED',
    AiAssistantOutcome.help => 'HELP',
    AiAssistantOutcome.refused => 'REFUSED',
  };
}

class AiAssistantSession {
  const AiAssistantSession({
    required this.id,
    required this.createdAt,
    this.sessionTitle,
  });

  final String id;
  final String? sessionTitle;
  final DateTime createdAt;

  factory AiAssistantSession.fromJson(Map<String, dynamic> json) {
    final id = json['id'];
    final createdAtRaw = json['createdAt'];
    if (id is! String || id.isEmpty) {
      throw const FormatException('Expected session id.');
    }
    if (createdAtRaw is! String) {
      throw const FormatException('Expected session createdAt.');
    }
    final createdAt = DateTime.tryParse(createdAtRaw)?.toUtc();
    if (createdAt == null) {
      throw const FormatException('Invalid session createdAt.');
    }
    return AiAssistantSession(
      id: id,
      sessionTitle: json['sessionTitle'] as String?,
      createdAt: createdAt,
    );
  }
}

class AiAssistantMessage {
  const AiAssistantMessage({
    required this.senderType,
    required this.messageText,
    required this.createdAt,
    this.id,
    this.outcome,
  });

  final String? id;
  final String senderType;
  final String messageText;
  final DateTime createdAt;
  final AiAssistantOutcome? outcome;

  bool get isUser => senderType.toUpperCase() == 'USER';
  bool get isBot => senderType.toUpperCase() == 'BOT';
  bool get isPersisted => id != null && id!.isNotEmpty;

  factory AiAssistantMessage.fromJson(
    Map<String, dynamic> json, {
    bool allowNullId = false,
    AiAssistantOutcome? outcome,
  }) {
    final idRaw = json['id'];
    if (!allowNullId && (idRaw is! String || idRaw.isEmpty)) {
      throw const FormatException('Expected decimal string message id.');
    }
    if (idRaw != null && idRaw is! String) {
      throw const FormatException('Expected message id to be a string or null.');
    }
    final senderType = json['senderType'];
    if (senderType is! String ||
        (senderType != 'USER' && senderType != 'BOT')) {
      throw const FormatException('Expected senderType USER or BOT.');
    }
    final messageText = json['messageText'];
    if (messageText is! String) {
      throw const FormatException('Expected string messageText.');
    }
    final createdAtRaw = json['createdAt'];
    if (createdAtRaw is! String) {
      throw const FormatException('Expected string createdAt.');
    }
    final createdAt = DateTime.tryParse(createdAtRaw)?.toUtc();
    if (createdAt == null) {
      throw const FormatException('Invalid message createdAt.');
    }
    return AiAssistantMessage(
      id: idRaw as String?,
      senderType: senderType,
      messageText: messageText,
      createdAt: createdAt,
      outcome: outcome,
    );
  }
}

class AiAssistantSessionsPage {
  const AiAssistantSessionsPage({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
  });

  final List<AiAssistantSession> items;
  final int page;
  final int pageSize;
  final int totalCount;

  bool get hasMore => page * pageSize < totalCount;

  factory AiAssistantSessionsPage.fromJson(Map<String, dynamic> json) {
    final rawItems = json['items'];
    if (rawItems is! List) {
      throw const FormatException('Expected sessions items list.');
    }
    final items = rawItems
        .map((item) {
          if (item is! Map) {
            throw const FormatException('Expected session object.');
          }
          return AiAssistantSession.fromJson(Map<String, dynamic>.from(item));
        })
        .toList(growable: false);
    return AiAssistantSessionsPage(
      items: List.unmodifiable(items),
      page: (json['page'] as num?)?.toInt() ?? 1,
      pageSize: (json['pageSize'] as num?)?.toInt() ?? 20,
      totalCount: (json['totalCount'] as num?)?.toInt() ?? items.length,
    );
  }
}

class AiAssistantMessagesPage {
  const AiAssistantMessagesPage({
    required this.sessionId,
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
  });

  final String sessionId;
  final List<AiAssistantMessage> items;
  final int page;
  final int pageSize;
  final int totalCount;

  bool get hasMore => page * pageSize < totalCount;

  factory AiAssistantMessagesPage.fromJson(Map<String, dynamic> json) {
    final sessionId = json['sessionId'];
    if (sessionId is! String || sessionId.isEmpty) {
      throw const FormatException('Expected sessionId in messages page.');
    }
    final rawItems = json['items'];
    if (rawItems is! List) {
      throw const FormatException('Expected messages items list.');
    }
    final items = rawItems
        .map((item) {
          if (item is! Map) {
            throw const FormatException('Expected message object.');
          }
          return AiAssistantMessage.fromJson(Map<String, dynamic>.from(item));
        })
        .toList(growable: false);
    return AiAssistantMessagesPage(
      sessionId: sessionId,
      items: List.unmodifiable(items),
      page: (json['page'] as num?)?.toInt() ?? 1,
      pageSize: (json['pageSize'] as num?)?.toInt() ?? 20,
      totalCount: (json['totalCount'] as num?)?.toInt() ?? items.length,
    );
  }
}

class AiAssistantSendRequest {
  const AiAssistantSendRequest({
    required this.message,
    this.sessionId,
    this.orderId,
  });

  final String? sessionId;
  final String? orderId;
  final String message;

  Map<String, dynamic> toJson() => {
    'sessionId': sessionId,
    'orderId': orderId,
    'message': message,
  };
}

class AiAssistantSendResponse {
  const AiAssistantSendResponse({
    required this.outcome,
    required this.reply,
    this.sessionId,
    this.userMessageId,
  });

  final AiAssistantOutcome outcome;
  final String? sessionId;
  final String? userMessageId;
  final AiAssistantMessage reply;

  factory AiAssistantSendResponse.fromJson(Map<String, dynamic> json) {
    final outcomeRaw = json['outcome'];
    if (outcomeRaw is! String) {
      throw const FormatException('Expected assistant outcome.');
    }
    final outcome = AiAssistantOutcome.parse(outcomeRaw);
    final rawReply = json['reply'];
    if (rawReply is! Map) {
      throw const FormatException('Expected assistant reply object.');
    }

    final isAnswered = outcome == AiAssistantOutcome.answered;
    final reply = AiAssistantMessage.fromJson(
      Map<String, dynamic>.from(rawReply),
      allowNullId: !isAnswered,
      outcome: outcome,
    );
    if (!reply.isBot) {
      throw const FormatException('Expected BOT reply.');
    }

    final sessionId = json['sessionId'] as String?;
    final userMessageId = json['userMessageId'] as String?;

    if (isAnswered) {
      if (sessionId == null ||
          sessionId.isEmpty ||
          userMessageId == null ||
          userMessageId.isEmpty ||
          reply.id == null ||
          reply.id!.isEmpty) {
        throw const FormatException(
          'Expected non-null sessionId, userMessageId, and reply.id for ANSWERED.',
        );
      }
    } else {
      if (sessionId != null || userMessageId != null || reply.id != null) {
        throw const FormatException(
          'Expected null sessionId, userMessageId, and reply.id for transient outcome.',
        );
      }
    }

    return AiAssistantSendResponse(
      outcome: outcome,
      sessionId: sessionId,
      userMessageId: userMessageId,
      reply: reply,
    );
  }
}
