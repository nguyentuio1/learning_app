import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';

/// Thay lớp mạng thật bằng hàm giả: test Dio mà không cần internet.
class FakeHttpAdapter implements HttpClientAdapter {
  FakeHttpAdapter(this.handler);
  final ResponseBody Function(RequestOptions options) handler;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async =>
      handler(options);

  @override
  void close({bool force = false}) {}
}

ResponseBody jsonBody(Object body, [int status = 200]) => ResponseBody.fromString(
      jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
