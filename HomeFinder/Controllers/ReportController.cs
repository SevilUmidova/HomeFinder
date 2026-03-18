using HomeFinder.Context;
using HomeFinder.Models.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace HomeFinder.Controllers
{
    public class ReportController : Controller
    {
        private readonly HomeFinderContext _context;

        public ReportController(HomeFinderContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult MostViewedApartments(
            int top = 5,
            string? dateFrom = null,
            string? dateTo = null,
            decimal? priceMin = null,
            decimal? priceMax = null,
            string? district = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));

            var vm = BuildMostViewedApartmentsVm(top, from, to, priceMin, priceMax, district);
            return View(vm);
        }

        [HttpGet]
        public IActionResult MostViewedApartmentsData(
            int top = 5,
            string? dateFrom = null,
            string? dateTo = null,
            decimal? priceMin = null,
            decimal? priceMax = null,
            string? district = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));

            var vm = BuildMostViewedApartmentsVm(top, from, to, priceMin, priceMax, district);
            return PartialView("_MostViewedApartmentsDataResponse", vm);
        }

        public IActionResult MostViewedDistricts(int top = 5, string? dateFrom = null, string? dateTo = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));
            var vm = BuildMostViewedDistrictsVm(top, from, to);
            return View(vm);
        }

        [HttpGet]
        public IActionResult MostViewedDistrictsData(int top = 5, string? dateFrom = null, string? dateTo = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));
            var vm = BuildMostViewedDistrictsVm(top, from, to);
            return PartialView("_MostViewedDistrictsDataResponse", vm);
        }

        public IActionResult ApartmentInteractivity(
            int top = 20,
            string? dateFrom = null,
            string? dateTo = null,
            decimal? priceMin = null,
            decimal? priceMax = null,
            decimal? priceBucketSize = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));
            var bucket = NormalizeBucket(priceBucketSize);

            var vm = BuildApartmentInteractivityVm(top, from, to, priceMin, priceMax, bucket);
            return View(vm);
        }

        [HttpGet]
        public IActionResult ApartmentInteractivityData(
            int top = 20,
            string? dateFrom = null,
            string? dateTo = null,
            decimal? priceMin = null,
            decimal? priceMax = null,
            decimal? priceBucketSize = null)
        {
            top = ClampTop(top);
            var (from, to) = NormalizePeriod(ParseDate(dateFrom), ParseDate(dateTo));
            var bucket = NormalizeBucket(priceBucketSize);

            var vm = BuildApartmentInteractivityVm(top, from, to, priceMin, priceMax, bucket);
            return PartialView("_ApartmentInteractivityDataResponse", vm);
        }

        private MostViewedDistrictsReportVm BuildMostViewedDistrictsVm(int top, DateTime fromDate, DateTime toDate)
        {
            var periodStart = fromDate.Date;
            var periodEnd = toDate.Date.AddDays(1);

            var viewsByApartment = _context.ApartmentViewLogs
                .AsNoTracking()
                .Where(v => v.ViewedAt >= periodStart && v.ViewedAt < periodEnd)
                .GroupBy(v => v.ApartmentId)
                .Select(g => new { ApartmentId = g.Key, Views = g.Count() })
                .ToList();

            var districtApartments = _context.Addresses
                .AsNoTracking()
                .Where(ad => ad.ApartmentId != null && ad.District != null && ad.District != "")
                .Select(ad => new { District = ad.District!, ApartmentId = ad.ApartmentId!.Value })
                .ToList();

            var viewCounts = viewsByApartment.ToDictionary(x => x.ApartmentId, x => x.Views);

            var items = districtApartments
                .GroupBy(x => x.District)
                .Select(g => new MostViewedDistrictsReportVm.Row
                {
                    District = g.Key,
                    TotalViews = g.Sum(x => viewCounts.TryGetValue(x.ApartmentId, out var c) ? c : 0),
                    ApartmentsCount = g.Select(x => x.ApartmentId).Distinct().Count()
                })
                .OrderByDescending(x => x.TotalViews)
                .Take(top)
                .ToList();

            return new MostViewedDistrictsReportVm
            {
                Top = top,
                DateFrom = fromDate,
                DateTo = toDate,
                Items = items
            };
        }

        private MostViewedApartmentsReportVm BuildMostViewedApartmentsVm(
            int top,
            DateTime fromDate,
            DateTime toDate,
            decimal? priceMin,
            decimal? priceMax,
            string? district)
        {
            var periodStart = fromDate.Date;
            var periodEnd = toDate.Date.AddDays(1);

            var viewsByApartment = _context.ApartmentViewLogs
                .AsNoTracking()
                .Where(v => v.ViewedAt >= periodStart && v.ViewedAt < periodEnd)
                .GroupBy(v => v.ApartmentId)
                .Select(g => new { ApartmentId = g.Key, Views = g.Count() })
                .ToList();

            var allViewedApartmentIds = viewsByApartment.Select(x => x.ApartmentId).ToList();
            if (allViewedApartmentIds.Count == 0)
            {
                return new MostViewedApartmentsReportVm
                {
                    Top = top,
                    DateFrom = fromDate,
                    DateTo = toDate,
                    SelectedDistrict = string.IsNullOrWhiteSpace(district) ? null : district.Trim(),
                    Items = new List<MostViewedApartmentsReportVm.Row>()
                };
            }

            // Список районов строим по всем квартирам, которые имели просмотры за период
            var districts = _context.Addresses
                .AsNoTracking()
                .Where(ad => ad.ApartmentId != null &&
                             allViewedApartmentIds.Contains(ad.ApartmentId.Value) &&
                             ad.District != null &&
                             ad.District != "")
                .Select(ad => ad.District!)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var selectedDistrict = string.IsNullOrWhiteSpace(district) ? null : district.Trim();

            var apartmentsQuery = _context.Apartments
                .AsNoTracking()
                .Where(a => allViewedApartmentIds.Contains(a.ApartmentId));

            if (priceMin.HasValue)
            {
                apartmentsQuery = apartmentsQuery.Where(a => a.Price == null || a.Price >= priceMin.Value);
            }
            if (priceMax.HasValue)
            {
                apartmentsQuery = apartmentsQuery.Where(a => a.Price == null || a.Price <= priceMax.Value);
            }
            if (!string.IsNullOrWhiteSpace(selectedDistrict))
            {
                apartmentsQuery = apartmentsQuery.Where(a => a.Addresses.Any(ad => ad.District != null && ad.District == selectedDistrict));
            }

            var apartments = apartmentsQuery
                .Select(a => new MostViewedApartmentsReportVm.Row
                {
                    ApartmentId = a.ApartmentId,
                    Views = 0,
                    Price = a.Price,
                    District = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.District).FirstOrDefault(),
                    City = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.City).FirstOrDefault(),
                    StreetAddress = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.StreetAddress).FirstOrDefault(),
                    BuildingNumber = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.BuildingNumber).FirstOrDefault(),
                    PhotoPath = a.Photos.OrderBy(p => p.PhotoId).Select(p => p.PhotoPath).FirstOrDefault()
                })
                .ToList();

            var viewCounts = viewsByApartment.ToDictionary(x => x.ApartmentId, x => x.Views);
            foreach (var row in apartments)
            {
                row.Views = viewCounts.TryGetValue(row.ApartmentId, out var c) ? c : 0;
            }
            var items = apartments
                .OrderByDescending(r => r.Views)
                .Take(top)
                .ToList();

            return new MostViewedApartmentsReportVm
            {
                Top = top,
                DateFrom = fromDate,
                DateTo = toDate,
                SelectedDistrict = selectedDistrict,
                Districts = districts,
                Items = items
            };
        }

        private ApartmentInteractivityReportVm BuildApartmentInteractivityVm(
            int top,
            DateTime fromDate,
            DateTime toDate,
            decimal? priceMin,
            decimal? priceMax,
            decimal priceBucketSize)
        {
            var periodStart = fromDate.Date;
            var periodEnd = toDate.Date.AddDays(1);

            var viewsByApartment = _context.ApartmentViewLogs
                .AsNoTracking()
                .Where(v => v.ViewedAt >= periodStart && v.ViewedAt < periodEnd)
                .GroupBy(v => v.ApartmentId)
                .Select(g => new { ApartmentId = g.Key, Views = g.Count() })
                .ToList();

            var inquiriesByApartment = _context.Appointments
                .AsNoTracking()
                .Where(a => a.ApartmentId != null && a.DateTime != null && a.DateTime >= periodStart && a.DateTime < periodEnd)
                .GroupBy(a => a.ApartmentId!.Value)
                .Select(g => new { ApartmentId = g.Key, Inquiries = g.Count() })
                .ToList();

            var favoritesByApartment = _context.Favorites
                .AsNoTracking()
                .Where(f => f.ApartmentId != null)
                .GroupBy(f => f.ApartmentId!.Value)
                .Select(g => new { ApartmentId = g.Key, Favorites = g.Count() })
                .ToList();

            var viewsDict = viewsByApartment.ToDictionary(x => x.ApartmentId, x => x.Views);
            var inqDict = inquiriesByApartment.ToDictionary(x => x.ApartmentId, x => x.Inquiries);
            var favDict = favoritesByApartment.ToDictionary(x => x.ApartmentId, x => x.Favorites);

            // Берём квартиры, у которых была хоть какая-то активность за период (просмотры или обращения)
            var activeApartmentIds = viewsDict.Keys
                .Union(inqDict.Keys)
                .Distinct()
                .ToList();

            if (activeApartmentIds.Count == 0)
            {
                return new ApartmentInteractivityReportVm
                {
                    Top = top,
                    DateFrom = fromDate,
                    DateTo = toDate,
                    PriceMin = priceMin,
                    PriceMax = priceMax,
                    PriceBucketSize = priceBucketSize,
                    Items = new List<ApartmentInteractivityReportVm.Row>()
                };
            }

            var apartmentsQuery = _context.Apartments
                .AsNoTracking()
                .Where(a => activeApartmentIds.Contains(a.ApartmentId));

            if (priceMin.HasValue)
                apartmentsQuery = apartmentsQuery.Where(a => a.Price == null || a.Price >= priceMin.Value);
            if (priceMax.HasValue)
                apartmentsQuery = apartmentsQuery.Where(a => a.Price == null || a.Price <= priceMax.Value);

            var rows = apartmentsQuery
                .Select(a => new ApartmentInteractivityReportVm.Row
                {
                    ApartmentId = a.ApartmentId,
                    Views = 0,
                    Inquiries = 0,
                    FavoritesTotal = 0,
                    ConversionRate = 0,

                    Price = a.Price,
                    Rooms = a.Rooms,
                    District = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.District).FirstOrDefault(),
                    City = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.City).FirstOrDefault(),
                    StreetAddress = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.StreetAddress).FirstOrDefault(),
                    BuildingNumber = a.Addresses.OrderBy(ad => ad.AddressId).Select(ad => ad.BuildingNumber).FirstOrDefault(),
                    PhotoPath = a.Photos.OrderBy(p => p.PhotoId).Select(p => p.PhotoPath).FirstOrDefault(),
                    DetailsUrl = Url.Action("Details", "Apartments", new { id = a.ApartmentId })
                })
                .ToList();

            foreach (var r in rows)
            {
                r.Views = viewsDict.TryGetValue(r.ApartmentId, out var v) ? v : 0;
                r.Inquiries = inqDict.TryGetValue(r.ApartmentId, out var i) ? i : 0;
                r.FavoritesTotal = favDict.TryGetValue(r.ApartmentId, out var f) ? f : 0;
                r.ConversionRate = r.Views > 0 ? (double)r.Inquiries / r.Views : 0;
            }

            // Топ по конверсии: чтобы не “обманывали” единичные просмотры, требуем минимум просмотров
            var items = rows
                .OrderByDescending(r => r.Views >= 10 ? r.ConversionRate : -1)
                .ThenByDescending(r => r.Inquiries)
                .ThenByDescending(r => r.Views)
                .Take(top)
                .ToList();

            var byPrice = rows
                .Where(r => r.Price.HasValue && r.Price.Value > 0)
                .GroupBy(r => PriceBucketKey(r.Price!.Value, priceBucketSize))
                .Select(g => BuildGroupRow(g.Key, g))
                .OrderByDescending(x => x.ConversionRate)
                .ToList();

            var byDistrict = rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.District) ? "—" : r.District!.Trim())
                .Select(g => BuildGroupRow(g.Key, g))
                .OrderByDescending(x => x.ConversionRate)
                .ToList();

            var byRooms = rows
                .GroupBy(r => RoomsKey(r.Rooms))
                .Select(g => BuildGroupRow(g.Key, g))
                .OrderByDescending(x => x.ConversionRate)
                .ToList();

            return new ApartmentInteractivityReportVm
            {
                Top = top,
                DateFrom = fromDate,
                DateTo = toDate,
                PriceMin = priceMin,
                PriceMax = priceMax,
                PriceBucketSize = priceBucketSize,
                Items = items,
                ByPriceRange = byPrice,
                ByDistrict = byDistrict,
                ByRooms = byRooms
            };
        }

        private static ApartmentInteractivityReportVm.GroupRow BuildGroupRow(
            string key,
            IEnumerable<ApartmentInteractivityReportVm.Row> rows)
        {
            var list = rows.ToList();
            var views = list.Sum(x => x.Views);
            var inq = list.Sum(x => x.Inquiries);

            return new ApartmentInteractivityReportVm.GroupRow
            {
                Key = key,
                ApartmentsCount = list.Count,
                Views = views,
                Inquiries = inq,
                ConversionRate = views > 0 ? (double)inq / views : 0
            };
        }

        private static string RoomsKey(int? rooms)
        {
            if (!rooms.HasValue) return "Не указано";
            if (rooms.Value <= 0) return "Не указано";
            if (rooms.Value >= 4) return "4+ комнат";
            return $"{rooms.Value} комнат";
        }

        private static string PriceBucketKey(decimal price, decimal bucketSize)
        {
            if (bucketSize <= 0) bucketSize = 1;
            var idx = (int)Math.Floor(price / bucketSize);
            var from = idx * bucketSize;
            var to = from + bucketSize;
            return $"{from:n0} – {to:n0}";
        }

        private static decimal NormalizeBucket(decimal? bucket)
        {
            if (!bucket.HasValue) return 1_000_000m;
            if (bucket.Value <= 0) return 1_000_000m;
            return bucket.Value;
        }

        private static (DateTime from, DateTime to) NormalizePeriod(DateTime? dateFrom, DateTime? dateTo)
        {
            var today = DateTime.Today;

            var to = (dateTo ?? today).Date;
            if (to > today) to = today;

            var from = (dateFrom ?? to.AddMonths(-1)).Date;

            if (from > to)
            {
                var tmp = from;
                from = to;
                to = tmp;
            }

            return (from, to);
        }

        private static DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            if (DateTime.TryParseExact(
                value.Trim(),
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
            {
                return parsed.Date;
            }

            return null;
        }

        private static int ClampTop(int top)
        {
            if (top <= 0) return 5;
            if (top > 50) return 50;
            return top;
        }
    }
}