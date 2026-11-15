import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../../../../domain/entities/tracking_snapshot.dart';

class MapTrajectory extends StatelessWidget {
  const MapTrajectory({required this.snapshot, super.key});

  final TrackingSnapshot snapshot;

  @override
  Widget build(BuildContext context) {
    final origin = LatLng(
      snapshot.originStation.latitude,
      snapshot.originStation.longitude,
    );
    final destination = LatLng(
      snapshot.destinationStation.latitude,
      snapshot.destinationStation.longitude,
    );
    final position = snapshot.lastPosition;
    final hasDrone =
        snapshot.status == DeliveryTrackingStatus.inTransit && position != null;
    final points = [
      origin,
      if (hasDrone) LatLng(position.latitude, position.longitude),
      destination,
    ];

    return ClipRRect(
      borderRadius: BorderRadius.circular(12),
      child: SizedBox(
        height: 240,
        child: FlutterMap(
          options: MapOptions(
            initialCenter: LatLng(
              (origin.latitude + destination.latitude) / 2,
              (origin.longitude + destination.longitude) / 2,
            ),
            initialZoom: 11,
            initialCameraFit: CameraFit.coordinates(
              coordinates: points,
              padding: const EdgeInsets.all(32),
            ),
          ),
          children: [
            TileLayer(
              urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
              userAgentPackageName: 'com.smartdrone.smart_drone_delivery',
            ),
            PolylineLayer(
              polylines: [
                Polyline(
                  points: points,
                  strokeWidth: 4,
                  color: Theme.of(context).colorScheme.primary,
                ),
              ],
            ),
            MarkerLayer(
              markers: [
                Marker(
                  point: origin,
                  width: 96,
                  height: 64,
                  child: const _StationMarker(
                    key: Key('origin-marker'),
                    label: 'Origin',
                    icon: Icons.flight_takeoff,
                    color: Color(0xFF3B82F6),
                  ),
                ),
                Marker(
                  point: destination,
                  width: 112,
                  height: 64,
                  child: const _StationMarker(
                    key: Key('destination-marker'),
                    label: 'Destination',
                    icon: Icons.location_on,
                    color: Color(0xFF16A34A),
                  ),
                ),
              ],
            ),
            if (hasDrone)
              TweenAnimationBuilder<LatLng>(
                tween: LatLngTween(
                  begin: LatLng(position.latitude, position.longitude),
                  end: LatLng(position.latitude, position.longitude),
                ),
                duration: const Duration(milliseconds: 450),
                builder: (context, animatedPosition, child) => MarkerLayer(
                  markers: [
                    Marker(
                      point: animatedPosition,
                      width: 72,
                      height: 48,
                      child: child!,
                    ),
                  ],
                ),
                child: const _DroneMarker(key: Key('drone-marker')),
              ),
            const RichAttributionWidget(
              attributions: [
                TextSourceAttribution('OpenStreetMap contributors'),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _StationMarker extends StatelessWidget {
  const _StationMarker({
    required this.label,
    required this.icon,
    required this.color,
    super.key,
  });

  final String label;
  final IconData icon;
  final Color color;

  @override
  Widget build(BuildContext context) => Column(
    mainAxisSize: MainAxisSize.min,
    children: [
      Icon(icon, color: color, size: 25),
      Container(
        padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 2),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(4),
        ),
        child: Text(label, style: const TextStyle(fontSize: 10)),
      ),
    ],
  );
}

class _DroneMarker extends StatelessWidget {
  const _DroneMarker({super.key});

  @override
  Widget build(BuildContext context) => const Icon(
    Icons.local_shipping,
    key: Key('drone-marker-icon'),
    color: Color(0xFF1D4ED8),
    size: 30,
  );
}
