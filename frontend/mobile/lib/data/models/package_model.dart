class Package {
  const Package({
    required this.id,
    required this.packageName,
    required this.weightGram,
    required this.lengthCm,
    required this.widthCm,
    required this.heightCm,
    required this.isFragile,
    this.packageImageUrl,
    this.createdAt,
  });

  final String id;
  final String packageName;
  final double weightGram;
  final double lengthCm;
  final double widthCm;
  final double heightCm;
  final String? packageImageUrl;
  final bool isFragile;
  final DateTime? createdAt;

  factory Package.fromJson(Map<String, dynamic> json) => Package(
    id: json['id'] as String,
    packageName: json['packageName'] as String,
    weightGram: (json['weightGram'] as num).toDouble(),
    lengthCm: (json['lengthCm'] as num).toDouble(),
    widthCm: (json['widthCm'] as num).toDouble(),
    heightCm: (json['heightCm'] as num).toDouble(),
    packageImageUrl: json['packageImageUrl'] as String?,
    isFragile: json['isFragile'] as bool? ?? false,
    createdAt: json['createdAt'] == null
        ? null
        : DateTime.parse(json['createdAt'] as String).toUtc(),
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'packageName': packageName,
    'weightGram': weightGram,
    'lengthCm': lengthCm,
    'widthCm': widthCm,
    'heightCm': heightCm,
    'packageImageUrl': packageImageUrl,
    'isFragile': isFragile,
    'createdAt': createdAt?.toUtc().toIso8601String(),
  };
}

class PackageRequest {
  const PackageRequest({
    required this.packageName,
    required this.weightGram,
    required this.lengthCm,
    required this.widthCm,
    required this.heightCm,
    required this.isFragile,
    this.packageImageUrl,
  });

  final String packageName;
  final double weightGram;
  final double lengthCm;
  final double widthCm;
  final double heightCm;
  final String? packageImageUrl;
  final bool isFragile;

  Map<String, dynamic> toJson() => {
    'packageName': packageName,
    'weightGram': weightGram,
    'lengthCm': lengthCm,
    'widthCm': widthCm,
    'heightCm': heightCm,
    'packageImageUrl': packageImageUrl,
    'isFragile': isFragile,
  };
}
