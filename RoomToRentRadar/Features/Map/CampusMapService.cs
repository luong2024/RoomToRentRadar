using System.Text;
using System.Text.Json;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Features.Map
{
    public interface ICampusMapService
    {
        string GenerateMapHtml(IEnumerable<RoomListing> rooms, int? selectedRoomId = null);
    }

    /// <summary>
    /// Service for US06 / Task T06.1: Generates Leaflet.js + OpenStreetMap interactive HTML
    /// centered around ICTU campus coordinates (21.5850, 105.8064) with interactive price-pill room markers.
    /// </summary>
    public class CampusMapService : ICampusMapService
    {
        public string GenerateMapHtml(IEnumerable<RoomListing> rooms, int? selectedRoomId = null)
        {
            var roomPins = new List<object>();

            foreach (var room in rooms)
            {
                if (room.Latitude <= 0 || room.Longitude <= 0)
                    continue;

                string formattedRent = (room.MonthlyRent / 1_000_000m).ToString("0.#") + " tr";

                roomPins.Add(new
                {
                    id = room.Id,
                    title = room.Title,
                    rent = formattedRent,
                    fullRent = room.MonthlyRent.ToString("N0") + " đ/tháng",
                    address = room.Address,
                    lat = room.Latitude,
                    lng = room.Longitude,
                    distance = room.DistanceToCampusKm.ToString("0.1") + " km",
                    isSelected = selectedRoomId.HasValue && selectedRoomId.Value == room.Id
                });
            }

            string pinsJson = JsonSerializer.Serialize(roomPins);
            double centerLat = Constants.IctuLatitude;
            double centerLng = Constants.IctuLongitude;

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"vi\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"utf-8\" />");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no\" />");
            sb.AppendLine("  <title>Bản đồ phòng trọ ICTU</title>");
            sb.AppendLine("  <link rel=\"stylesheet\" href=\"https://unpkg.com/leaflet@1.9.4/dist/leaflet.css\" />");
            sb.AppendLine("  <style>");
            sb.AppendLine("    * { box-sizing: border-box; }");
            sb.AppendLine("    html, body, #map { width: 100%; height: 100%; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }");
            sb.AppendLine("    .price-pill {");
            sb.AppendLine("      background-color: #2563eb; color: #ffffff; font-weight: 700; font-size: 11px;");
            sb.AppendLine("      padding: 4px 8px; border-radius: 12px; white-space: nowrap; border: 2px solid #ffffff;");
            sb.AppendLine("      box-shadow: 0 2px 6px rgba(0,0,0,0.35); cursor: pointer; text-align: center; transition: transform 0.15s ease;");
            sb.AppendLine("    }");
            sb.AppendLine("    .price-pill:hover, .price-pill.selected {");
            sb.AppendLine("      background-color: #dc2626; transform: scale(1.15); z-index: 1000 !important;");
            sb.AppendLine("    }");
            sb.AppendLine("    .campus-badge {");
            sb.AppendLine("      background-color: #059669; color: #ffffff; font-weight: bold; font-size: 11px;");
            sb.AppendLine("      padding: 5px 10px; border-radius: 14px; border: 2px solid #ffffff;");
            sb.AppendLine("      box-shadow: 0 2px 8px rgba(0,0,0,0.4); text-align: center; white-space: nowrap;");
            sb.AppendLine("    }");
            sb.AppendLine("    .custom-icon { background: none; border: none; }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <div id=\"map\"></div>");
            sb.AppendLine("  <script src=\"https://unpkg.com/leaflet@1.9.4/dist/leaflet.js\"></script>");
            sb.AppendLine("  <script>");
            sb.AppendLine("    try {");
            sb.AppendLine($"      var map = L.map('map', {{ zoomControl: true, attributionControl: false }}).setView([{centerLat}, {centerLng}], 15);");
            sb.AppendLine("      L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19 }).addTo(map);");
            sb.AppendLine();
            sb.AppendLine("      // Campus Center Marker");
            sb.AppendLine("      var campusIcon = L.divIcon({");
            sb.AppendLine("        className: 'custom-icon',");
            sb.AppendLine($"        html: '<div class=\"campus-badge\">🎓 ICTU Thái Nguyên</div>',");
            sb.AppendLine("        iconSize: [120, 30],");
            sb.AppendLine("        iconAnchor: [60, 15]");
            sb.AppendLine("      });");
            sb.AppendLine($"      L.marker([{centerLat}, {centerLng}], {{ icon: campusIcon }}).addTo(map);");
            sb.AppendLine();
            sb.AppendLine("      // Room Pins");
            sb.AppendLine($"      var pins = {pinsJson};");
            sb.AppendLine("      pins.forEach(function(p) {");
            sb.AppendLine("        var pillClass = p.isSelected ? 'price-pill selected' : 'price-pill';");
            sb.AppendLine("        var icon = L.divIcon({");
            sb.AppendLine("          className: 'custom-icon',");
            sb.AppendLine("          html: '<div class=\"' + pillClass + '\">' + p.rent + '</div>',");
            sb.AppendLine("          iconSize: [60, 24],");
            sb.AppendLine("          iconAnchor: [30, 12]");
            sb.AppendLine("        });");
            sb.AppendLine("        var marker = L.marker([p.lat, p.lng], { icon: icon }).addTo(map);");
            sb.AppendLine("        marker.on('click', function() {");
            sb.AppendLine("          window.location.href = 'rtr://pin?roomId=' + p.id;");
            sb.AppendLine("        });");
            sb.AppendLine("      });");
            sb.AppendLine("    } catch (err) {");
            sb.AppendLine("      console.error('Leaflet load error:', err);");
            sb.AppendLine("    }");
            sb.AppendLine("  </script>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}
