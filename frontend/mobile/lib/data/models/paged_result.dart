class PagedResult<T> {
  const PagedResult({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  final List<T> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  bool get hasMore => page < totalPages;

  factory PagedResult.fromJson(
    Map<String, dynamic> json,
    T Function(Map<String, dynamic>) parseItem,
  ) {
    final rawItems = json['items'];
    if (rawItems is! List) {
      throw const FormatException('Expected a paged result items list.');
    }

    return PagedResult<T>(
      items: List.unmodifiable(
        rawItems.map((item) {
          if (item is! Map) {
            throw const FormatException('Expected a paged result item object.');
          }
          return parseItem(Map<String, dynamic>.from(item));
        }),
      ),
      page: json['page'] as int? ?? 1,
      pageSize: json['pageSize'] as int? ?? rawItems.length,
      totalCount: json['totalCount'] as int? ?? rawItems.length,
      totalPages: json['totalPages'] as int? ?? ((rawItems.isEmpty) ? 0 : 1),
    );
  }
}
