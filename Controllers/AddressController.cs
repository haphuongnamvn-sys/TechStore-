using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AddressController : ControllerBase
    {
        private static List<VnProvince>? _cachedData;
        private static readonly object _lock = new();

        private static List<VnProvince> GetAddressData()
        {
            if (_cachedData != null) return _cachedData;
            lock (_lock)
            {
                if (_cachedData != null) return _cachedData;
                try
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "data-address.json");
                    if (System.IO.File.Exists(filePath))
                    {
                        var json = System.IO.File.ReadAllText(filePath);
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        _cachedData = JsonSerializer.Deserialize<List<VnProvince>>(json, options) ?? new();
                    }
                    else
                    {
                        _cachedData = new();
                    }
                }
                catch
                {
                    _cachedData = new();
                }
                return _cachedData;
            }
        }

        [HttpGet("provinces")]
        public IActionResult GetProvinces()
        {
            var data = GetAddressData();
            var names = data.Select(p => p.Name).ToList();
            return Ok(names);
        }

        [HttpGet("districts")]
        public IActionResult GetDistricts([FromQuery] string province)
        {
            if (string.IsNullOrWhiteSpace(province)) return Ok(new List<string>());
            var data = GetAddressData();
            var cleanProv = province.Replace("Thành phố ", "").Replace("Tỉnh ", "").Trim();
            var prov = data.FirstOrDefault(p => p.Name.Equals(province, StringComparison.OrdinalIgnoreCase)
                                             || p.Name.Replace("Thành phố ", "").Replace("Tỉnh ", "").Trim()
                                                .Equals(cleanProv, StringComparison.OrdinalIgnoreCase));
            if (prov == null) return Ok(new List<string>());
            return Ok(prov.Districts.Select(d => d.Name).ToList());
        }

        [HttpGet("wards")]
        public IActionResult GetWards([FromQuery] string district)
        {
            if (string.IsNullOrWhiteSpace(district)) return Ok(new List<string>());
            var data = GetAddressData();
            foreach (var prov in data)
            {
                var dist = prov.Districts.FirstOrDefault(d => d.Name.Equals(district, StringComparison.OrdinalIgnoreCase));
                if (dist != null)
                {
                    return Ok(dist.Wards.Select(w => w.Name).ToList());
                }
            }
            return Ok(new List<string>());
        }

        public class VnProvince
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public List<VnDistrict> Districts { get; set; } = new();
        }

        public class VnDistrict
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public List<VnWard> Wards { get; set; } = new();
        }

        public class VnWard
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Level { get; set; } = "";
        }
    }
}
